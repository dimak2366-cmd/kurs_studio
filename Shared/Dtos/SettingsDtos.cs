namespace Shared.Dtos;

public class ServerSettingsDto
{
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public int MaxFailedAttempts { get; set; }
    public int LockoutMinutes { get; set; }
    public int SessionTimeoutMinutes { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string PgDumpPath { get; set; } = string.Empty;
    public string PgRestorePath { get; set; } = string.Empty;
}

public class UpdateSettingsRequest
{
    public int MaxFailedAttempts { get; set; }
    public int LockoutMinutes { get; set; }
    public int SessionTimeoutMinutes { get; set; }
    public string BackupPath { get; set; } = string.Empty;
}
