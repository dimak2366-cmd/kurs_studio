namespace Server.Infrastructure;

/// <summary>
/// Конфигурация сервера (паттерн «Одиночка», ленивая инициализация через Lazy{T}).
/// </summary>
public sealed class AppConfig
{
    private static readonly Lazy<AppConfig> _instance =
        new(() => new AppConfig(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static AppConfig Instance => _instance.Value;

    private AppConfig() { }

    public string ConnectionString { get; private set; } = string.Empty;

    public string ServerAddress { get; private set; } = "0.0.0.0";
    public int ServerPort { get; private set; } = 5155;

    public int MaxFailedAttempts { get; private set; } = 3;
    public int LockoutMinutes { get; private set; } = 15;
    public int SessionTimeoutMinutes { get; private set; } = 60;
    public string AesKeyBase64 { get; private set; } = string.Empty;

    public string BackupPath { get; private set; } = "./Backups";
    public string PgDumpPath { get; private set; } = "pg_dump";
    public string PgRestorePath { get; private set; } = "pg_restore";

    public void Initialize(IConfiguration configuration)
    {
        ConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Не задана строка подключения DefaultConnection в appsettings.json.");

        ServerAddress = configuration["Server:Address"] ?? ServerAddress;
        ServerPort = ParseInt(configuration["Server:Port"], ServerPort);

        MaxFailedAttempts = ParseInt(configuration["Security:MaxFailedAttempts"], MaxFailedAttempts);
        LockoutMinutes = ParseInt(configuration["Security:LockoutMinutes"], LockoutMinutes);
        SessionTimeoutMinutes = ParseInt(configuration["Security:SessionTimeoutMinutes"], SessionTimeoutMinutes);
        AesKeyBase64 = configuration["Security:AesKeyBase64"] ?? AesKeyBase64;

        BackupPath = configuration["Backup:Path"] ?? BackupPath;
        PgDumpPath = configuration["Backup:PgDumpPath"] ?? PgDumpPath;
        PgRestorePath = configuration["Backup:PgRestorePath"] ?? PgRestorePath;
    }

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, out var value) ? value : fallback;
}
