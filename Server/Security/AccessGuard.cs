using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Security;

/// <summary>
/// Проверки доступа к объектам, привязанным к отделу.
/// Вызывать ПОСЛЕ [AuthorizeRole]: к этому моменту Items["RoleName"] и Items["DepartmentId"]
/// уже заполнены фильтром авторизации (см. AuthorizeRoleAttribute).
/// Для всех ролей, кроме «Руководитель отдела», проверки — no-op.
/// </summary>
public static class AccessGuard
{
    private const string HeadRole = "Руководитель отдела";

    /// <summary>
    /// Для Head — проверяет, что смета принадлежит его отделу.
    /// Для остальных ролей — no-op.
    /// </summary>
    public static async Task EnsureSmetaAccessAsync(
        AppDbContext db, HttpContext ctx, int smetaId, CancellationToken ct = default)
    {
        if (!IsHead(ctx))
            return;

        var deptId = await db.Smetas
            .Where(s => s.Id == smetaId)
            .Select(s => (int?)s.Project.DepartmentId)
            .FirstOrDefaultAsync(ct);

        if (deptId is null)
            throw new KeyNotFoundException($"Смета #{smetaId} не найдена.");

        if (deptId.Value != GetCurrentDepartmentId(ctx))
            throw new ForbiddenException("Смета принадлежит другому отделу.");
    }

    /// <summary>
    /// Для использования в LINQ-запросах: возвращает Id смет, доступных пользователю.
    /// Для Head — только своего отдела, для остальных — все. Композится в EF-запрос.
    /// </summary>
    public static IQueryable<int> AccessibleSmetaIds(AppDbContext db, HttpContext ctx)
    {
        if (!IsHead(ctx))
            return db.Smetas.Select(s => s.Id);

        var deptId = GetCurrentDepartmentId(ctx);
        return db.Smetas
            .Where(s => s.Project.DepartmentId == deptId)
            .Select(s => s.Id);
    }

    /// <summary>
    /// Для Head — проверяет, что объект принадлежит его отделу (проект, отдельский отчёт).
    /// Для остальных ролей — no-op. Синхронный: departmentId известен вызывающей стороне.
    /// </summary>
    public static void EnsureDepartmentAccess(HttpContext ctx, int? departmentId)
    {
        if (!IsHead(ctx))
            return;

        if (departmentId is null || departmentId.Value != GetCurrentDepartmentId(ctx))
            throw new ForbiddenException("Объект принадлежит другому отделу.");
    }

    public static bool IsHead(HttpContext ctx) =>
        (ctx.Items["RoleName"] as string) == HeadRole;

    public static int GetCurrentDepartmentId(HttpContext ctx)
    {
        if (ctx.Items["DepartmentId"] is int id)
            return id;

        // Fallback: если AuthorizeRoleAttribute кладёт целиком SessionInfo.
        if (ctx.Items["Session"] is Server.Services.SessionInfo session
            && session.DepartmentId is int id2)
            return id2;

        throw new ForbiddenException(
            "Не удалось определить отдел пользователя. " +
            "AuthorizeRoleAttribute должен заполнить Items[\"DepartmentId\"] (или Items[\"Session\"]).");
    }
}
