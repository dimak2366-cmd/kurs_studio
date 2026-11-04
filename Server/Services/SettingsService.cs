using System.Text.Json;
using System.Text.Json.Nodes;
using Server.Infrastructure;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Сервис настроек сервера: читает текущие значения из AppConfig,
/// обновляет appsettings.json и переинициализирует одиночку AppConfig.
/// </summary>
public sealed class SettingsService
{
    private readonly IWebHostEnvironment _env;

    public SettingsService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public ServerSettingsDto Get()
    {
        return new ServerSettingsDto
        {
            Address = AppConfig.Instance.ServerAddress,
            Port = AppConfig.Instance.ServerPort,
            MaxFailedAttempts = AppConfig.Instance.MaxFailedAttempts,
            LockoutMinutes = AppConfig.Instance.LockoutMinutes,
            SessionTimeoutMinutes = AppConfig.Instance.SessionTimeoutMinutes,
            BackupPath = AppConfig.Instance.BackupPath,
            PgDumpPath = AppConfig.Instance.PgDumpPath,
            PgRestorePath = AppConfig.Instance.PgRestorePath
        };
    }

    public async Task<ServerSettingsDto> UpdateAsync(UpdateSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MaxFailedAttempts < 1 || request.MaxFailedAttempts > 10)
        {
            throw new ArgumentException("MaxFailedAttempts должен быть в диапазоне от 1 до 10.");
        }

        if (request.LockoutMinutes < 1 || request.LockoutMinutes > 1440)
        {
            throw new ArgumentException("LockoutMinutes должен быть в диапазоне от 1 до 1440.");
        }

        if (request.SessionTimeoutMinutes < 5 || request.SessionTimeoutMinutes > 1440)
        {
            throw new ArgumentException("SessionTimeoutMinutes должен быть в диапазоне от 5 до 1440.");
        }

        var path = Path.Combine(_env.ContentRootPath, "appsettings.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Не найден appsettings.json.", path);
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var root = JsonNode.Parse(json) ?? throw new InvalidOperationException("Не удалось разобрать appsettings.json.");

        var security = root["Security"] ??= new JsonObject();
        security["MaxFailedAttempts"] = request.MaxFailedAttempts;
        security["LockoutMinutes"] = request.LockoutMinutes;
        security["SessionTimeoutMinutes"] = request.SessionTimeoutMinutes;

        var backup = root["Backup"] ??= new JsonObject();
        if (!string.IsNullOrWhiteSpace(request.BackupPath))
        {
            backup["Path"] = request.BackupPath;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        await File.WriteAllTextAsync(path, root.ToJsonString(options), cancellationToken);

        // Перечитываем конфигурацию с диска и обновляем одиночку.
        var freshConfig = new ConfigurationBuilder()
            .SetBasePath(_env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
        AppConfig.Instance.Initialize(freshConfig);

        return Get();
    }
}
