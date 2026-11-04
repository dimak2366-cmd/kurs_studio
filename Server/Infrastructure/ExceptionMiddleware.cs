using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;

namespace Server.Infrastructure;

/// <summary>
/// Глобальный обработчик исключений. Логирует ошибку в ErrorLog и возвращает
/// клиенту корректный HTTP-статус (400/401/404/500).
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необработанное исключение: {Message}", ex.Message);
            await PersistErrorAsync(scopeFactory, ex);

            if (context.Response.HasStarted)
            {
                return;
            }

            var statusCode = ex switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                error = ex.Message,
                status = statusCode
            });
        }
    }

    private async Task PersistErrorAsync(IServiceScopeFactory scopeFactory, Exception ex)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ErrorLogs.Add(new ErrorLog
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                Message = ex.Message,
                StackTrace = ex.StackTrace
            });
            await db.SaveChangesAsync();
        }
        catch (Exception inner)
        {
            _logger.LogError(inner, "Не удалось сохранить запись об ошибке в ErrorLog.");
        }
    }
}
