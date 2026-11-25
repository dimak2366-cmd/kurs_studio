using Microsoft.EntityFrameworkCore;
using Npgsql;
using Server.Data;
using Xunit;

namespace Server.Tests.Concurrency;

/// <summary>
/// Одноразовая тестовая БД: создаётся при старте класса, дропается в конце.
/// Строка подключения — замени на свой PostgreSQL, отдельный от dev.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    // ⚠️ Подставь свои креды. БД должна называться иначе, чем рабочая.
    public const string TestConnectionString =
        "Host=localhost;Port=5432;Database=budgetcontrol_test;Username=postgres;Password=1234";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }

    public async Task DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Готовит «чистую» статью с заданным лимитом и утверждённым статусом,
    /// а также открытый период на указанный месяц. Возвращает (itemId, periodId).
    /// </summary>
    public async Task<(int ItemId, int PeriodId)> CreateFreshItemAsync(
        decimal limitAmount, int year = 2027, int month = 1,
        string limitStatus = "Утверждён")
    {
        await using var db = CreateContext();

        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);

        var role = new Server.Models.Role { Name = $"TestRole_{suffix}" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new Server.Models.User
        {
            Login = $"test_{suffix}",
            FullName = "Test",
            PasswordHash = "x",
            PasswordSalt = "y",
            RoleId = role.Id
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dept = new Server.Models.Department { Name = $"TestDept_{suffix}" };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        var project = new Server.Models.Project
        {
            Code = $"P_{suffix}",
            Name = "Test Project",
            DepartmentId = dept.Id
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var smeta = new Server.Models.Smeta
        {
            ProjectId = project.Id,
            FormCode = "2-смета",
            Year = year,
            Status = "Утверждена",
            CreatedAt = DateTime.UtcNow
        };
        db.Smetas.Add(smeta);
        await db.SaveChangesAsync();

        var item = new Server.Models.ExpenseItem
        {
            SmetaId = smeta.Id,
            Name = "Тестовая статья",
            KosguCode = "211",
            Quantity = 1m,
            Price = limitAmount,
            PlanAmount = limitAmount,
            LimitAmount = limitAmount,
            LimitStatus = limitStatus
        };
        db.ExpenseItems.Add(item);
        await db.SaveChangesAsync(); // Сохраняем item отдельно, чтобы гарантированно получить Id

        // Удаляем существующий период и его расходы для изоляции теста
        var existingPeriod = await db.Periods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month);
        if (existingPeriod != null)
        {
            var expenses = await db.ActualExpenses.Where(a => a.PeriodId == existingPeriod.Id).ToListAsync();
            db.ActualExpenses.RemoveRange(expenses);
            db.Periods.Remove(existingPeriod);
            await db.SaveChangesAsync();
        }

        var period = new Server.Models.Period { Year = year, Month = month, IsClosed = false };
        db.Periods.Add(period);
        await db.SaveChangesAsync();

        return (item.Id, period.Id);
    }
}
