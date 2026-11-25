using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Server.Data;
using Server.Models;
using Server.Security;

namespace Server.Infrastructure;

/// <summary>
/// Глобальная обработка исключений: логирование в Serilog + ErrorLog,
/// маппинг доменных исключений в корректные HTTP-коды, сокрытие деталей 500.
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

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, userMessage) = MapException(ex);

            // 5xx — логируем как Error с полным стеком; 4xx — Warning без стека.
            if (statusCode >= 500)
                _logger.LogError(ex, "Необработанная ошибка {Path}", context.Request.Path);
            else
                _logger.LogWarning("Ожидаемая ошибка {StatusCode} {Path}: {Message}",
                    statusCode, context.Request.Path, ex.Message);

            // Пишем в ErrorLog только серверные ошибки (>=500), чтобы не засорять БД
            // ожидаемыми 4xx.
            if (statusCode >= 500)
            {
                try
                {
                    db.ErrorLogs.Add(new ErrorLog
                    {
                        Timestamp  = DateTime.UtcNow,
                        Level      = "Error",
                        Message    = $"{ex.GetType().Name}: {ex.Message}",
                        StackTrace = ex.StackTrace
                    });
                    await db.SaveChangesAsync();
                }
                catch
                {
                    // Запись в ErrorLog не должна ронять обработчик
                }
            }

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode  = statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = userMessage }));
            }
        }
    }

    private static (int StatusCode, string UserMessage) MapException(Exception ex) => ex switch
    {
        // ── Доменные, которые мы бросаем осознанно ──────────────────────
        ConflictException ce           => (StatusCodes.Status409Conflict, ce.Message),
        ArgumentException ae           => (StatusCodes.Status400BadRequest, ae.Message),
        KeyNotFoundException knf       => (StatusCodes.Status404NotFound, knf.Message),
        ForbiddenException fe          => (StatusCodes.Status403Forbidden, fe.Message),
        UnauthorizedAccessException ua => (StatusCodes.Status401Unauthorized, ua.Message),
        InvalidOperationException io   => (StatusCodes.Status400BadRequest, io.Message),

        // ── Уникальные ограничения БД (гонки, которые не поймали явно) ───
        DbUpdateException { InnerException: PostgresException pg } => pg.SqlState switch
        {
            // 23505 unique_violation
            "23505" => (StatusCodes.Status409Conflict,
                        pg.ConstraintName switch
                        {
                            "ux_actual_expenses_doc" => "Документ с таким номером уже введён по этой статье в этом периоде.",
                            _                        => "Нарушено уникальное ограничение. Проверьте данные.",
                        }),
            // 23503 foreign_key_violation
            "23503" => (StatusCodes.Status400BadRequest, "Связанная запись не найдена или уже удалена."),
            // 23502 not_null_violation
            "23502" => (StatusCodes.Status400BadRequest, "Обязательное поле не заполнено."),
            _       => (StatusCodes.Status500InternalServerError, "Ошибка базы данных.")
        },

        DbUpdateException du when du.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
            => (StatusCodes.Status409Conflict, "Запись с такими данными уже существует."),

        // ── Всё остальное ────────────────────────────────────────────────
        _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера. Повторите позже.")
    };
}
