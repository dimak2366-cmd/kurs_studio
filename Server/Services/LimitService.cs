using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Dtos;

namespace Server.Services;

public sealed class LimitService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public LimitService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<ExpenseItemDto>> GetLimitsAsync(int smetaId, CancellationToken cancellationToken)
    {
        var smeta = await LoadSmetaWithItemsAsync(smetaId, cancellationToken);
        return smeta.ExpenseItems.Select(i => new ExpenseItemDto
        {
            Id = i.Id,
            Name = i.Name,
            KosguCode = i.KosguCode,
            PlanAmount = i.PlanAmount,
            LimitAmount = i.LimitAmount,
            Quantity = i.Quantity,
            Price = i.Price,
            FactAmount = i.ActualExpenses.Sum(a => a.Amount),
            LimitStatus = i.LimitStatus,
            LimitReturnComment = i.LimitReturnComment
        }).ToList();
    }

    public async Task<List<ExpenseItemDto>> SetLimitsAsync(
        int smetaId, List<SetLimitItemRequest> items, int userId, CancellationToken cancellationToken)
    {
        var smeta = await LoadSmetaWithItemsAsync(smetaId, cancellationToken);

        if (smeta.Status == "Утверждена" || smeta.Status == "Закрыта")
            throw new InvalidOperationException("Нельзя менять лимиты утверждённой или закрытой сметы.");

        var dict = smeta.ExpenseItems.ToDictionary(i => i.Id);
        foreach (var item in items)
        {
            if (!dict.TryGetValue(item.ExpenseItemId, out var dbItem))
                throw new ArgumentException($"Статья с id {item.ExpenseItemId} не принадлежит смете #{smetaId}.");
            if (item.LimitAmount < 0)
                throw new ArgumentException("Лимит не может быть отрицательным.");

            // Любое изменение суммы лимита требует повторного согласования директором.
            if (dbItem.LimitAmount != item.LimitAmount)
            {
                dbItem.LimitStatus = "Предложен";
                dbItem.LimitReturnComment = null;
            }
            dbItem.LimitAmount = item.LimitAmount;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(userId, "SetLimits", "Smeta", smetaId.ToString(),
            $"Обновлены лимиты по смете #{smetaId} ({items.Count} статей)", cancellationToken);

        return await GetLimitsAsync(smetaId, cancellationToken);
    }

    public async Task<LimitCheckDto> CheckExcessAsync(int smetaId, CancellationToken cancellationToken)
    {
        var smeta = await LoadSmetaWithItemsAsync(smetaId, cancellationToken);

        var items = smeta.ExpenseItems.Select(i =>
        {
            var fact = i.ActualExpenses.Sum(a => a.Amount);
            var remaining = i.LimitAmount - fact;
            var exceeds = fact > i.LimitAmount;
            return new LimitCheckItemDto
            {
                ExpenseItemId = i.Id,
                Name = i.Name,
                KosguCode = i.KosguCode,
                Quantity = i.Quantity,
                Price = i.Price,
                PlanAmount = i.PlanAmount,
                LimitAmount = i.LimitAmount,
                FactAmount = fact,
                Remaining = remaining,
                Exceeds = exceeds,
                ExcessAmount = exceeds ? fact - i.LimitAmount : 0m,
                LimitStatus = i.LimitStatus,
                LimitReturnComment = i.LimitReturnComment
            };
        }).ToList();

        return new LimitCheckDto
        {
            SmetaId = smetaId,
            // Учитываем превышение только по утверждённым лимитам:
            // «Предложен»/«Возвращён» ещё не вступили в силу.
            HasExcess = items.Any(i => i.LimitStatus == "Утверждён" && i.Exceeds),
            Items = items
        };
    }

    public async Task<LimitActionResultDto> ApproveAsync(
        int smetaId, int userId, CancellationToken cancellationToken)
    {
        var smetaExists = await _db.Smetas.AnyAsync(s => s.Id == smetaId, cancellationToken);
        if (!smetaExists) throw new KeyNotFoundException($"Смета #{smetaId} не найдена.");

        var proposed = await _db.ExpenseItems
            .Where(i => i.SmetaId == smetaId && i.LimitStatus == "Предложен")
            .ToListAsync(cancellationToken);

        if (proposed.Count == 0)
            throw new InvalidOperationException("Нет лимитов в статусе «Предложен» для утверждения.");

        foreach (var item in proposed)
        {
            item.LimitStatus = "Утверждён";
            item.LimitReturnComment = null;
        }
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "ApproveLimits", "Smeta", smetaId.ToString(),
            $"Утверждены лимиты по {proposed.Count} статьям сметы #{smetaId}", cancellationToken);

        return new LimitActionResultDto
        {
            Success = true,
            Message = $"Утверждено {proposed.Count} лимит(ов).",
            Check = await CheckExcessAsync(smetaId, cancellationToken)
        };
    }

    public async Task<LimitActionResultDto> ReturnAsync(
        int smetaId, string comment, int userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Комментарий при возврате обязателен.");

        var smetaExists = await _db.Smetas.AnyAsync(s => s.Id == smetaId, cancellationToken);
        if (!smetaExists) throw new KeyNotFoundException($"Смета #{smetaId} не найдена.");

        var proposed = await _db.ExpenseItems
            .Where(i => i.SmetaId == smetaId && i.LimitStatus == "Предложен")
            .ToListAsync(cancellationToken);

        if (proposed.Count == 0)
            throw new InvalidOperationException("Нет лимитов в статусе «Предложен» для возврата.");

        foreach (var item in proposed)
        {
            item.LimitStatus = "Возвращён";
            item.LimitReturnComment = comment.Trim();
        }
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "ReturnLimits", "Smeta", smetaId.ToString(),
            $"Возвращены на доработку лимиты по {proposed.Count} статьям сметы #{smetaId}. Причина: {comment}",
            cancellationToken);

        return new LimitActionResultDto
        {
            Success = true,
            Message = $"Возвращено {proposed.Count} лимит(ов).",
            Check = await CheckExcessAsync(smetaId, cancellationToken)
        };
    }

    private async Task<Smeta> LoadSmetaWithItemsAsync(int id, CancellationToken cancellationToken)
    {
        var smeta = await _db.Smetas
            .Include(s => s.ExpenseItems).ThenInclude(i => i.ActualExpenses)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return smeta ?? throw new KeyNotFoundException("Смета не найдена.");
    }
}
