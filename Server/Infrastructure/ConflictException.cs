namespace Server.Infrastructure;

/// <summary>
/// Доменное исключение для случаев, когда запрос валиден, но конфликтует
/// с текущим состоянием БД (например, дубль уникального ключа).
/// Мапится в HTTP 409 Conflict в ExceptionMiddleware.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
    public ConflictException(string message, Exception inner) : base(message, inner) { }
}
