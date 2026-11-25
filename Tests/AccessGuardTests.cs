using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Security;
using Xunit;

public class AccessGuardTests
{
    private static AppDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("guard_" + Guid.NewGuid())
            .Options;
        return new AppDbContext(opts);
    }

    private static HttpContext Ctx(string role, int? deptId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["RoleName"] = role;
        if (deptId.HasValue) ctx.Items["DepartmentId"] = deptId.Value;
        return ctx;
    }

    private static async Task<(AppDbContext db, int smeta1Id, int smeta2Id)> SeedTwoDepartmentsAsync()
    {
        var db = NewDb();
        var dep1 = new Department { Name = "ИТ" };
        var dep2 = new Department { Name = "Экономика" };
        db.Departments.AddRange(dep1, dep2);
        var p1 = new Project { Code = "P1", Name = "P1", Department = dep1,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow };
        var p2 = new Project { Code = "P2", Name = "P2", Department = dep2,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow };
        db.Projects.AddRange(p1, p2);
        var s1 = new Smeta { Project = p1, FormCode = "2", Year = 2026,
            Status = "Утверждена", CreatedAt = DateTime.UtcNow };
        var s2 = new Smeta { Project = p2, FormCode = "2", Year = 2026,
            Status = "Утверждена", CreatedAt = DateTime.UtcNow };
        db.Smetas.AddRange(s1, s2);
        await db.SaveChangesAsync();
        return (db, s1.Id, s2.Id);
    }

    [Fact]
    public async Task Head_OwnDepartmentSmeta_NoException()
    {
        var (db, smeta1, _) = await SeedTwoDepartmentsAsync();
        await AccessGuard.EnsureSmetaAccessAsync(db, Ctx("Руководитель отдела", 1), smeta1);
    }

    [Fact]
    public async Task Head_ForeignSmeta_Throws()
    {
        var (db, _, smeta2) = await SeedTwoDepartmentsAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AccessGuard.EnsureSmetaAccessAsync(db, Ctx("Руководитель отдела", 1), smeta2));
    }

    [Fact]
    public async Task Economist_ForeignSmeta_NoException()
    {
        var (db, _, smeta2) = await SeedTwoDepartmentsAsync();
        await AccessGuard.EnsureSmetaAccessAsync(db, Ctx("Главный экономист", 1), smeta2);
    }

    [Fact]
    public async Task Head_WithoutDepartmentId_Throws()
    {
        var (db, smeta1, _) = await SeedTwoDepartmentsAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AccessGuard.EnsureSmetaAccessAsync(db, Ctx("Руководитель отдела", null), smeta1));
    }

    [Fact]
    public async Task AccessibleSmetaIds_Head_ReturnsOwnOnly()
    {
        var (db, smeta1, _) = await SeedTwoDepartmentsAsync();
        var ids = await AccessGuard.AccessibleSmetaIds(db, Ctx("Руководитель отдела", 1)).ToListAsync();
        Assert.Contains(smeta1, ids);
        Assert.Single(ids);
    }
}
