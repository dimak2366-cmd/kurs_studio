using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Server.Services;

namespace Server.Security;

/// <summary>
/// Атрибут авторизации по роли. Проверяет заголовок Authorization: Bearer {token},
/// достаёт сессию из TokenService, проверяет срок действия и принадлежность роли.
/// При отсутствии/просрочке токена возвращает 401, при недостатке прав — 403.
/// Применение без аргументов означает «требуется любой аутентифицированный пользователь».
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizeRoleAttribute : ActionFilterAttribute
{
    private readonly string[] _roles;

    public AuthorizeRoleAttribute(params string[] roles)
    {
        _roles = roles;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var tokenService = context.HttpContext.RequestServices.GetRequiredService<TokenService>();
        var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Требуется заголовок Authorization: Bearer <token>." });
            return;
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        var session = tokenService.GetSession(token);
        if (session == null)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Сессия не найдена или истекла." });
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(session.RoleName))
        {
            context.Result = new ObjectResult(new { error = $"Недостаточно прав. Требуется роль: {string.Join(", ", _roles)}." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        context.HttpContext.Items["Session"] = session;
        context.HttpContext.Items["UserId"] = session.UserId;
        context.HttpContext.Items["RoleName"] = session.RoleName;
        context.HttpContext.Items["DepartmentId"] = session.DepartmentId;
        base.OnActionExecuting(context);
    }
}
