using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Управление отчётными периодами: список, закрытие.
/// Закрытие атомарно: строка Period блокируется FOR UPDATE на время транзакции,
/// поэтому параллельные вставки расходов в этот период сериализуются с закрытием.
/// </summary>
public sealed class PeriodService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public PeriodService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<PeriodDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var periods = await _db.Periods
            .Include(p => p.ActualExpenses)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
            .ToListAsync(cancellationToken);

        return periods.Select(ToDto).ToList();
    }

    public async Task<PeriodDto> CloseAsync(int id, int userId, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // 1. Блокируем строку периода FOR UPDATE.
        //    Параллельные CreateAsync/UpdateAsync по этому же периоду
        //    тоже блокируются на этой строке (см. ExpenseService).
        var period = await _db.Periods
            .FromSqlInterpolated($"SELECT * FROM periods WHERE \"Id\" = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Отчётный период не найден.");

        await _db.Entry(period).Collection(p => p.ActualExpenses).LoadAsync(cancellationToken);

        if (period.IsClosed)
            throw new InvalidOperationException($"Период {period.Year:D4}/{period.Month:D2} уже закрыт.");

        var unverified = period.ActualExpenses.Count(a => a.Status != "Сверен");
        if (unverified > 0)
        {
            throw new InvalidOperationException(
                $"В периоде {unverified} несверенных записей. Все расходы должны быть сверены перед закрытием.");
        }

        var totalCount  = period.ActualExpenses.Count;
        var totalAmount = period.ActualExpenses.Sum(a => a.Amount);

        period.IsClosed = true;
        period.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        await _audit.LogAsync(userId, "Close", "Period", period.Id.ToString(),
            $"Закрыт отчётный период {period.Year:D4}/{period.Month:D2} " +
            $"({totalCount} записей, {totalAmount:N2} BYN)", cancellationToken);

        return ToDto(period);
    }

    private static PeriodDto ToDto(Period p) => new()
    {
        Id = p.Id,
        Year = p.Year,
        Month = p.Month,
        IsClosed = p.IsClosed,
        ClosedAt = p.ClosedAt,
        ExpenseCount = p.ActualExpenses.Count,
        UnverifiedCount = p.ActualExpenses.Count(a => a.Status != "Сверен"),
        TotalAmount = p.ActualExpenses.Sum(a => a.Amount)
    };
}
