using Microsoft.EntityFrameworkCore;
using Server.Models;
using Server.Security;

namespace Server.Data;

/// <summary>
/// Начальное заполнение базы данных: роли, администратор, отделы, проекты,
/// сметы, статьи расходов, отчётные периоды и фактические расходы.
/// Повторный запуск не создаёт дубликатов — если в таблице Roles есть записи, метод выходит.
/// Все даты задаются в UTC: Npgsql 6+ для колонок timestamp with time zone
/// принимает только DateTimeKind.Utc.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Roles.AnyAsync())
        {
            return;
        }

        // ---------- Роли (5) ----------
        var roles = new[]
        {
            new Role { Name = "Администратор" },
            new Role { Name = "Директор" },
            new Role { Name = "Главный экономист" },
            new Role { Name = "Руководитель отдела" },
            new Role { Name = "Бухгалтер" }
        };
        db.Roles.AddRange(roles);
        await db.SaveChangesAsync();

        var roleAdmin = roles[0];
        var roleDirector = roles[1];
        var roleChiefEconomist = roles[2];
        var roleHead = roles[3];
        var roleAccountant = roles[4];

        // ---------- Отделы (2) ----------
        var departments = new[]
        {
            new Department { Name = "Отдел информационных технологий" },
            new Department { Name = "Отдел экономики и финансов" }
        };
        db.Departments.AddRange(departments);
        await db.SaveChangesAsync();

        var depIt = departments[0];
        var depEconomy = departments[1];

        // ---------- Пользователи (по одному на роль + администратор) ----------
        var adminSalt = PasswordHasher.GenerateSalt();
        var admin = new User
        {
            Login = "admin",
            PasswordSalt = adminSalt,
            PasswordHash = PasswordHasher.ComputeHash("Admin123!", adminSalt),
            FullName = "Администратор системы",
            RoleId = roleAdmin.Id,
            DepartmentId = depEconomy.Id,
            IsBlocked = false,
            FailedAttempts = 0
        };

        var directorSalt = PasswordHasher.GenerateSalt();
        var director = new User
        {
            Login = "director",
            PasswordSalt = directorSalt,
            PasswordHash = PasswordHasher.ComputeHash("Director123!", directorSalt),
            FullName = "Иванов Иван Иванович",
            RoleId = roleDirector.Id,
            DepartmentId = depEconomy.Id,
            IsBlocked = false,
            FailedAttempts = 0
        };

        var chiefSalt = PasswordHasher.GenerateSalt();
        var chief = new User
        {
            Login = "economist",
            PasswordSalt = chiefSalt,
            PasswordHash = PasswordHasher.ComputeHash("Economist123!", chiefSalt),
            FullName = "Петрова Анна Сергеевна",
            RoleId = roleChiefEconomist.Id,
            DepartmentId = depEconomy.Id,
            IsBlocked = false,
            FailedAttempts = 0
        };

        var headSalt = PasswordHasher.GenerateSalt();
        var head = new User
        {
            Login = "head",
            PasswordSalt = headSalt,
            PasswordHash = PasswordHasher.ComputeHash("Head123!", headSalt),
            FullName = "Сидоров Пётр Алексеевич",
            RoleId = roleHead.Id,
            DepartmentId = depIt.Id,
            IsBlocked = false,
            FailedAttempts = 0
        };

        var accountantSalt = PasswordHasher.GenerateSalt();
        var accountant = new User
        {
            Login = "accountant",
            PasswordSalt = accountantSalt,
            PasswordHash = PasswordHasher.ComputeHash("Accountant123!", accountantSalt),
            FullName = "Кузнецова Мария Викторовна",
            RoleId = roleAccountant.Id,
            DepartmentId = depEconomy.Id,
            IsBlocked = false,
            FailedAttempts = 0
        };

        db.Users.AddRange(admin, director, chief, head, accountant);
        await db.SaveChangesAsync();

        // ---------- Проекты (3) ----------
        var projects = new[]
        {
            new Project
            {
                Code = "НИР-2026-01",
                Name = "Разработка системы мониторинга сетевой инфраструктуры",
                DepartmentId = depIt.Id,
                StartDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 20, 0, 0, 0, DateTimeKind.Utc)
            },
            new Project
            {
                Code = "НИР-2026-02",
                Name = "Исследование алгоритмов обработки больших данных",
                DepartmentId = depIt.Id,
                StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc)
            },
            new Project
            {
                Code = "НИР-2026-03",
                Name = "Экономический анализ эффективности бюджетных расходов",
                DepartmentId = depEconomy.Id,
                StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc)
            }
        };
        db.Projects.AddRange(projects);
        await db.SaveChangesAsync();

        // ---------- Сметы (2) ----------
        var now = DateTime.UtcNow;
        var smeta1 = new Smeta
        {
            ProjectId = projects[0].Id,
            FormCode = "2-смета",
            Year = 2026,
            Status = "Утверждена",
            CreatedAt = now.AddDays(-40),
            ApprovedAt = now.AddDays(-35)
        };
        var smeta2 = new Smeta
        {
            ProjectId = projects[1].Id,
            FormCode = "2-смета",
            Year = 2026,
            Status = "Утверждена",
            CreatedAt = now.AddDays(-30),
            ApprovedAt = now.AddDays(-25)
        };
        db.Smetas.AddRange(smeta1, smeta2);
        await db.SaveChangesAsync();

        // ---------- Статьи расходов (по 5 в каждой смете) ----------
        var expenseItems = new List<ExpenseItem>();
        foreach (var smeta in new[] { smeta1, smeta2 })
        {
            expenseItems.Add(new ExpenseItem
            {
                SmetaId = smeta.Id,
                Name = "Фонд оплаты труда",
                KosguCode = "211",
                PlanAmount = 500_000m,
                LimitAmount = 550_000m,
                Quantity = 10m,
                Price = 50_000m
            });
            expenseItems.Add(new ExpenseItem
            {
                SmetaId = smeta.Id,
                Name = "Материалы",
                KosguCode = "340",
                PlanAmount = 120_000m,
                LimitAmount = 130_000m,
                Quantity = 100m,
                Price = 1_200m
            });
            expenseItems.Add(new ExpenseItem
            {
                SmetaId = smeta.Id,
                Name = "Услуги сторонних организаций",
                KosguCode = "226",
                PlanAmount = 80_000m,
                LimitAmount = 90_000m,
                Quantity = 4m,
                Price = 20_000m
            });
            expenseItems.Add(new ExpenseItem
            {
                SmetaId = smeta.Id,
                Name = "Командировочные расходы",
                KosguCode = "212",
                PlanAmount = 30_000m,
                LimitAmount = 35_000m,
                Quantity = 6m,
                Price = 5_000m
            });
            expenseItems.Add(new ExpenseItem
            {
                SmetaId = smeta.Id,
                Name = "Накладные расходы",
                KosguCode = "290",
                PlanAmount = 40_000m,
                LimitAmount = 45_000m,
                Quantity = 1m,
                Price = 40_000m
            });
        }
        db.ExpenseItems.AddRange(expenseItems);
        await db.SaveChangesAsync();

        // ---------- Отчётные периоды ----------
        // Требование — «1 закрытый период». Дополнительно создаём открытый,
        // чтобы на этапе 7 можно было проверять POST /api/expenses (в закрытый ввод запрещён).
        var closedPeriod = new Period
        {
            Year = 2026,
            Month = 9,
            IsClosed = true,
            ClosedAt = now.AddDays(-5)
        };
        var openPeriod = new Period
        {
            Year = 2026,
            Month = 10,
            IsClosed = false,
            ClosedAt = null
        };
        db.Periods.AddRange(closedPeriod, openPeriod);
        await db.SaveChangesAsync();

        // ---------- 10 фактических расходов в закрытом периоде ----------
        var actualExpenses = new List<ActualExpense>();
        for (var i = 0; i < 10; i++)
        {
            var item = expenseItems[i % expenseItems.Count];
            var day = 3 + i;
            var expenseDate = new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc);
            actualExpenses.Add(new ActualExpense
            {
                ExpenseItemId = item.Id,
                Date = expenseDate,
                Amount = Math.Round(item.PlanAmount * 0.08m, 2),
                DocumentType = "Платёжное поручение",
                DocumentNumber = $"ПП-2026-{900 + i:000}",
                DocumentDate = expenseDate,
                Status = "Введён",
                PeriodId = closedPeriod.Id
            });
        }
        db.ActualExpenses.AddRange(actualExpenses);
        await db.SaveChangesAsync();
    }
}
