using Microsoft.EntityFrameworkCore;
using Server.Models;
using Server.Security;

namespace Server.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Roles.AnyAsync()) return;

        // ---------- Роли ----------
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

        var roleAdmin = roles[0]; var roleDirector = roles[1]; var roleChiefEconomist = roles[2];
        var roleHead = roles[3];  var roleAccountant = roles[4];

        // ---------- Отделы ----------
        var departments = new[]
        {
            new Department { Name = "Отдел информационных технологий" },
            new Department { Name = "Отдел экономики и финансов" }
        };
        db.Departments.AddRange(departments);
        await db.SaveChangesAsync();
        var depIt = departments[0]; var depEconomy = departments[1];

        // ---------- Пользователи ----------
        var adminSalt = PasswordHasher.GenerateSalt();
        var admin = new User {
            Login = "admin", PasswordSalt = adminSalt,
            PasswordHash = PasswordHasher.ComputeHash("Admin123!", adminSalt),
            FullName = "Администратор системы", RoleId = roleAdmin.Id,
            DepartmentId = depEconomy.Id, IsBlocked = false, FailedAttempts = 0 };

        var directorSalt = PasswordHasher.GenerateSalt();
        var director = new User {
            Login = "director", PasswordSalt = directorSalt,
            PasswordHash = PasswordHasher.ComputeHash("Director123!", directorSalt),
            FullName = "Иванов Иван Иванович", RoleId = roleDirector.Id,
            DepartmentId = depEconomy.Id, IsBlocked = false, FailedAttempts = 0 };

        var chiefSalt = PasswordHasher.GenerateSalt();
        var chief = new User {
            Login = "economist", PasswordSalt = chiefSalt,
            PasswordHash = PasswordHasher.ComputeHash("Economist123!", chiefSalt),
            FullName = "Петрова Анна Сергеевна", RoleId = roleChiefEconomist.Id,
            DepartmentId = depEconomy.Id, IsBlocked = false, FailedAttempts = 0 };

        var headSalt = PasswordHasher.GenerateSalt();
        var head = new User {
            Login = "head", PasswordSalt = headSalt,
            PasswordHash = PasswordHasher.ComputeHash("Head123!", headSalt),
            FullName = "Сидоров Пётр Алексеевич", RoleId = roleHead.Id,
            DepartmentId = depIt.Id, IsBlocked = false, FailedAttempts = 0 };

        var accountantSalt = PasswordHasher.GenerateSalt();
        var accountant = new User {
            Login = "accountant", PasswordSalt = accountantSalt,
            PasswordHash = PasswordHasher.ComputeHash("Accountant123!", accountantSalt),
            FullName = "Кузнецова Мария Викторовна", RoleId = roleAccountant.Id,
            DepartmentId = depEconomy.Id, IsBlocked = false, FailedAttempts = 0 };

        db.Users.AddRange(admin, director, chief, head, accountant);
        await db.SaveChangesAsync();

        // ---------- Проекты ----------
        var projects = new[]
        {
            new Project { Code = "НИР-2026-01",
                Name = "Разработка системы мониторинга сетевой инфраструктуры",
                DepartmentId = depIt.Id,
                StartDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 12, 20, 0, 0, 0, DateTimeKind.Utc) },
            new Project { Code = "НИР-2026-02",
                Name = "Исследование алгоритмов обработки больших данных",
                DepartmentId = depIt.Id,
                StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc) },
            new Project { Code = "НИР-2026-03",
                Name = "Экономический анализ эффективности бюджетных расходов",
                DepartmentId = depEconomy.Id,
                StartDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc) }
        };
        db.Projects.AddRange(projects);
        await db.SaveChangesAsync();

        // ---------- Сметы ----------
        var now = DateTime.UtcNow;
        var smeta1 = new Smeta { ProjectId = projects[0].Id, FormCode = "2-смета", Year = 2026,
            Status = "Утверждена", CreatedAt = now.AddDays(-40), ApprovedAt = now.AddDays(-35) };
        var smeta2 = new Smeta { ProjectId = projects[1].Id, FormCode = "2-смета", Year = 2026,
            Status = "Утверждена", CreatedAt = now.AddDays(-30), ApprovedAt = now.AddDays(-25) };
        var smeta3 = new Smeta { ProjectId = projects[2].Id, FormCode = "2-смета", Year = 2026,
            Status = "Утверждена", CreatedAt = now.AddDays(-20), ApprovedAt = now.AddDays(-15) };
        db.Smetas.AddRange(smeta1, smeta2, smeta3);
        await db.SaveChangesAsync();

        // ---------- Статьи ----------
        var expenseItems = new List<ExpenseItem>();

        // smeta1 — все лимиты утверждены.
        expenseItems.Add(new ExpenseItem {
            SmetaId = smeta1.Id, Name = "Фонд оплаты труда", KosguCode = "211",
            PlanAmount = 30_000m, LimitAmount = 35_000m, Quantity = 10m, Price = 3_000m,
            LimitStatus = "Утверждён" });
        expenseItems.Add(new ExpenseItem {
            SmetaId = smeta1.Id, Name = "Материалы", KosguCode = "340",
            PlanAmount = 5_000m, LimitAmount = 6_000m, Quantity = 100m, Price = 50m,
            LimitStatus = "Утверждён" });
        expenseItems.Add(new ExpenseItem {
            SmetaId = smeta1.Id, Name = "Услуги сторонних организаций", KosguCode = "226",
            PlanAmount = 80_000m, LimitAmount = 90_000m, Quantity = 4m, Price = 20_000m,
            LimitStatus = "Утверждён" });
        expenseItems.Add(new ExpenseItem {
            SmetaId = smeta1.Id, Name = "Командировочные расходы", KosguCode = "212",
            PlanAmount = 30_000m, LimitAmount = 35_000m, Quantity = 6m, Price = 5_000m,
            LimitStatus = "Утверждён" });
        expenseItems.Add(new ExpenseItem {
            SmetaId = smeta1.Id, Name = "Накладные расходы", KosguCode = "290",
            PlanAmount = 40_000m, LimitAmount = 45_000m, Quantity = 1m, Price = 40_000m,
            LimitStatus = "Утверждён" });

        // smeta3 — отдел экономики (демо для Head 403).
        // Первая статья утверждена, вторая — на согласовании у директора.
        expenseItems.Add(new ExpenseItem {
            SmetaId = smeta3.Id, Name = "Фонд оплаты труда", KosguCode = "211",
            PlanAmount = 200_000m, LimitAmount = 220_000m, Quantity = 4m, Price = 50_000m,
            LimitStatus = "Утверждён" });
        expenseItems.Add(new ExpenseItem {
            SmetaId = smeta3.Id, Name = "Материалы", KosguCode = "340",
            PlanAmount = 20_000m, LimitAmount = 25_000m, Quantity = 200m, Price = 100m,
            LimitStatus = "Предложен" });

        // smeta2 — все утверждены.
        foreach (var item in new[]
        {
            ("Фонд оплаты труда", "211", 500_000m, 550_000m, 10m, 50_000m),
            ("Материалы", "340", 120_000m, 130_000m, 100m, 1_200m),
            ("Услуги сторонних организаций", "226", 80_000m, 90_000m, 4m, 20_000m),
            ("Командировочные расходы", "212", 30_000m, 35_000m, 6m, 5_000m),
            ("Накладные расходы", "290", 40_000m, 45_000m, 1m, 40_000m)
        })
        {
            expenseItems.Add(new ExpenseItem {
                SmetaId = smeta2.Id, Name = item.Item1, KosguCode = item.Item2,
                PlanAmount = item.Item3, LimitAmount = item.Item4,
                Quantity = item.Item5, Price = item.Item6,
                LimitStatus = "Утверждён" });
        }

        db.ExpenseItems.AddRange(expenseItems);
        await db.SaveChangesAsync();

        // ---------- Справочник ----------
        var catalogPairs = expenseItems.Select(i => (i.Name, i.KosguCode)).Distinct().ToList();
        foreach (var pair in catalogPairs)
        {
            if (!await db.CatalogExpenseItems.AnyAsync(c =>
                c.Name == pair.Name && c.KosguCode == pair.KosguCode))
            {
                db.CatalogExpenseItems.Add(new CatalogExpenseItem {
                    Name = pair.Name, KosguCode = pair.KosguCode, IsActive = true });
            }
        }
        await db.SaveChangesAsync();

        // ---------- Периоды ----------
        var closedPeriod = new Period { Year = 2026, Month = 9, IsClosed = true, ClosedAt = now.AddDays(-5) };
        var openPeriod = new Period { Year = 2026, Month = 10, IsClosed = false };
        db.Periods.AddRange(closedPeriod, openPeriod);
        await db.SaveChangesAsync();

        // ---------- Фактические расходы ----------
        var actualExpenses = new List<ActualExpense>();
        var demoMaterials = expenseItems.First(i => i.SmetaId == smeta1.Id && i.Name == "Материалы");
        var demoFot = expenseItems.First(i => i.SmetaId == smeta1.Id && i.Name == "Фонд оплаты труда");
        var demoDate = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);

        actualExpenses.Add(new ActualExpense {
            ExpenseItemId = demoMaterials.Id, Date = demoDate, Amount = 7_200m, Quantity = 120m,
            DocumentType = "Платёжное поручение", DocumentNumber = "ПП-2026-DEMO-001",
            DocumentDate = demoDate, Status = "Сверен", PeriodId = closedPeriod.Id });
        actualExpenses.Add(new ActualExpense {
            ExpenseItemId = demoFot.Id, Date = demoDate.AddDays(1), Amount = 34_000m, Quantity = 11m,
            DocumentType = "Платёжное поручение", DocumentNumber = "ПП-2026-DEMO-002",
            DocumentDate = demoDate.AddDays(1), Status = "Сверен", PeriodId = closedPeriod.Id });

        var others = expenseItems
            .Where(i => i.Id != demoMaterials.Id && i.Id != demoFot.Id).ToList();
        for (var i = 0; i < others.Count; i++)
        {
            var item = others[i];
            var expenseDate = new DateTime(2026, 9, 3 + i, 0, 0, 0, DateTimeKind.Utc);
            actualExpenses.Add(new ActualExpense {
                ExpenseItemId = item.Id, Date = expenseDate,
                Amount = Math.Round(item.PlanAmount * 1.08m, 2), Quantity = item.Quantity,
                DocumentType = "Платёжное поручение",
                DocumentNumber = $"ПП-2026-{900 + i:000}",
                DocumentDate = expenseDate, Status = "Введён", PeriodId = closedPeriod.Id });
        }
        db.ActualExpenses.AddRange(actualExpenses);
        await db.SaveChangesAsync();
    }
}
