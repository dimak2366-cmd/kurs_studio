namespace Shared.Dtos;

public class BackupFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BackupResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public BackupFileDto? File { get; set; }
}

public class RestoreRequest
{
    public string FileName { get; set; } = string.Empty;
}
