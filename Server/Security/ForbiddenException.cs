namespace Server.Security;

/// <summary>
/// Бросается, когда пользователь аутентифицирован, но не имеет прав на конкретный объект
/// (например, Head запрашивает смету чужого отдела). Маппится в HTTP 403.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
