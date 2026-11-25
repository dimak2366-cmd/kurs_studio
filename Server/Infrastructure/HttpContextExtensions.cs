namespace Server.Infrastructure;
public static class HttpContextExtensions
{
    public static int CurrentUserId(this HttpContext ctx) =>
        ctx.Items["UserId"] is int id
            ? id
            : throw new UnauthorizedAccessException(
                "Сессия не определена. Проверьте порядок middleware (AuthorizeRole должен стоять до контроллера).");
}
