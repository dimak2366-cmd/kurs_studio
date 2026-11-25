using Microsoft.EntityFrameworkCore;
using Server.Infrastructure;
using Server.Services;
using Shared.Dtos;
using Xunit;

namespace Server.Tests.Concurrency;

[Collection("Database")]
public class ConcurrencyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public ConcurrencyTests(DatabaseFixture fixture) => _fixture = fixture;

    // Каждый параллельный вызов создаёт СВОЙ DbContext — DbContext не потокобезопасен.
    private ExpenseService NewExpenseService() => new(
        _fixture.CreateContext(),
        new AuditService(_fixture.CreateContext()));

    private static CreateActualExpenseRequest Req(int itemId, decimal amount, string docNum) => new()
    {
        ExpenseItemId  = itemId,
        Amount         = amount,
        Date           = new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        DocumentType   = "Платёжное поручение",
        DocumentNumber = docNum,
        DocumentDate   = new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc)
    };

    // ─── Тест 1: две параллельные вставки по одной статье → только одна проходит ───
    [Fact]
    public async Task ParallelCreateWithinLimit_OnlyOneSucceeds()
    {
        var (itemId, _) = await _fixture.CreateFreshItemAsync(limitAmount: 35_000m);

        var req1 = Req(itemId, 20_000m, "DOC-1");
        var req2 = Req(itemId, 20_000m, "DOC-2");

        var t1 = Task.Run(() => NewExpenseService().CreateAsync(req1, userId: 1, CancellationToken.None));
        var t2 = Task.Run(() => NewExpenseService().CreateAsync(req2, userId: 1, CancellationToken.None));

        var results = await Task.WhenAll(Results(t1), Results(t2));

        var succeeded = results.Count(r => r.Ok);
        var failed    = results.Count(r => !r.Ok);

        Assert.Equal(1, succeeded);
        Assert.Equal(1, failed);
        Assert.Contains(results.Where(r => !r.Ok), r =>
            r.Error != null && r.Error.Contains("превысит утверждённый лимит"));

        // В БД осталась ровно одна запись с суммой 20 000
        await using var db = _fixture.CreateContext();
        var fact = await db.ActualExpenses
            .Where(a => a.ExpenseItemId == itemId)
            .SumAsync(a => (decimal?)a.Amount) ?? 0m;
        Assert.Equal(20_000m, fact);
    }

    // ─── Тест 2: две параллельные вставки в НОВЫЙ месяц → обе проходят, период один ───
    [Fact]
    public async Task ParallelCreateIntoNewPeriod_BothSucceed_OnePeriodRow()
    {
        var (itemId, _) = await _fixture.CreateFreshItemAsync(limitAmount: 1_000_000m);

        // Удалим созданный период, чтобы оба потока гонялись за его создание
        await using (var db = _fixture.CreateContext())
        {
            var p = await db.Periods.FirstAsync(x => x.Year == 2027 && x.Month == 1);
            db.Periods.Remove(p);
            await db.SaveChangesAsync();
        }

        var t1 = Task.Run(() => NewExpenseService().CreateAsync(
            Req(itemId, 100m, "P-1"), userId: 1, CancellationToken.None));
        var t2 = Task.Run(() => NewExpenseService().CreateAsync(
            Req(itemId, 100m, "P-2"), userId: 1, CancellationToken.None));

        var results = await Task.WhenAll(Results(t1), Results(t2));
        Assert.All(results, r => Assert.True(r.Ok, r.Error));

        await using var check = _fixture.CreateContext();
        var periodCount = await check.Periods.CountAsync(p => p.Year == 2027 && p.Month == 1);
        Assert.Equal(1, periodCount);
    }

    // ─── Тест 3: повторный DocumentNumber по той же статье/периоду → конфликт ───
    [Fact]
    public async Task DuplicateDocumentNumber_ReturnsConflict()
    {
        var (itemId, _) = await _fixture.CreateFreshItemAsync(limitAmount: 100_000m);

        var service = NewExpenseService();
        await service.CreateAsync(Req(itemId, 1_000m, "DOC-DUP"), userId: 1, CancellationToken.None);

        var second = NewExpenseService();
        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            second.CreateAsync(Req(itemId, 2_000m, "DOC-DUP"), userId: 1, CancellationToken.None));

        // Ожидаем либо ConflictException (явная обёртка), либо DbUpdateException (unique index).
        // В обоих случаях наружу это не должно доходить как 500 — ExceptionMiddleware смапит в 409.
        Assert.True(
            ex is ConflictException
            || ex is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" } },
            $"Ожидался ConflictException или DbUpdateException/23505, получено: {ex.GetType().Name}: {ex.Message}");
    }

    // ─── Тест 4: параллельное закрытие периода и вставка расхода — без утечки ───
    [Fact]
    public async Task ClosePeriodWhileInserting_NeverLeaksIntoClosedPeriod()
    {
        var (itemId, periodId) = await _fixture.CreateFreshItemAsync(limitAmount: 100_000m);

        // Сделаем период «готовым к закрытию»: пока расходов нет — unverified = 0.
        var periodService = new PeriodService(
            _fixture.CreateContext(),
            new AuditService(_fixture.CreateContext()));

        var closeTask  = Task.Run(() => periodService.CloseAsync(periodId, userId: 1, CancellationToken.None));
        var insertTask = Task.Run(() => NewExpenseService().CreateAsync(
            Req(itemId, 1_000m, "RACE-DOC"), userId: 1, CancellationToken.None));

        var both = await Task.WhenAll(Results(closeTask), Results(insertTask));

        // После гонки: проверяем инвариант — не существует расхода в закрытом периоде.
        await using var db = _fixture.CreateContext();
        var leaked = await db.ActualExpenses
            .Where(a => a.PeriodId == periodId && a.Period.IsClosed)
            .CountAsync();

        Assert.Equal(0, leaked);

        // Допустимы два исхода:
        //  (a) закрытие успешно, вставка упала «период закрыт»;
        //  (b) вставка успешна, закрытие упало «несверенных записей» (расход «Введён»).
        // Любой другой исход — баг.
        var closeOk  = both[0].Ok;
        var insertOk = both[1].Ok;
        Assert.True(closeOk ^ insertOk,
            $"Ровно одна из операций должна пройти. closeOk={closeOk}, insertOk={insertOk}, " +
            $"closeErr={both[0].Error}, insertErr={both[1].Error}");
    }

    // ─── helper: превратить Task в безопасный результат, не роняя Task.WhenAll ───
    private static async Task<OpResult> Results<T>(Task<T> task)
    {
        try { await task; return OpResult.Success(); }
        catch (Exception ex) { return OpResult.Failure(ex); }
    }

    private sealed record OpResult(bool Ok, string? Error)
    {
        public static OpResult Success() => new(true, null);
        public static OpResult Failure(Exception ex) => new(false, $"{ex.GetType().Name}: {ex.Message}");
    }
}
