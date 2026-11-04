using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Бизнес-логика лимитов: установка лимитов (пока смета не утверждена),
/// контроль превышения фактических расходов над лимитами.
/// </summary>
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
            FactAmount = i.ActualExpenses.Sum(a => a.Amount)
        }).ToList();
    }

    public async Task<List<ExpenseItemDto>> SetLimitsAsync(int smetaId, List<SetLimitItemRequest> items, int userId, CancellationToken cancellationToken)
    {
        var smeta = await LoadSmetaWithItemsAsync(smetaId, cancellationToken);

        if (smeta.Status == "Утверждена" || smeta.Status == "Закрыта")
        {
            throw new InvalidOperationException("Нельзя менять лимиты утверждённой или закрытой сметы.");
        }

        var dict = smeta.ExpenseItems.ToDictionary(i => i.Id);
        foreach (var item in items)
        {
            if (!dict.TryGetValue(item.ExpenseItemId, out var dbItem))
            {
                throw new ArgumentException($"Статья с id {item.ExpenseItemId} не принадлежит смете #{smetaId}.");
            }

            if (item.LimitAmount < 0)
            {
                throw new ArgumentException("Лимит не может быть отрицательным.");
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
                PlanAmount = i.PlanAmount,
                LimitAmount = i.LimitAmount,
                FactAmount = fact,
                Remaining = remaining,
                Exceeds = exceeds,
                ExcessAmount = exceeds ? fact - i.LimitAmount : 0m
            };
        }).ToList();

        return new LimitCheckDto
        {
            SmetaId = smetaId,
            HasExcess = items.Any(i => i.Exceeds),
            Items = items
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
