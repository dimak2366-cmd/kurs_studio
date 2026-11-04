using System.Diagnostics;
using Npgsql;
using Server.Infrastructure;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Сервис резервного копирования: запускает pg_dump и pg_restore через Process.Start,
/// передавая пароль в переменной окружения PGPASSWORD.
/// </summary>
public sealed class BackupService
{
    public async Task<BackupFileDto> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var backupDir = AppConfig.Instance.BackupPath;
        Directory.CreateDirectory(backupDir);

        var fileName = $"budget_institute_{DateTime.UtcNow:yyyyMMdd_HHmmss}.backup";
        var fullPath = Path.Combine(backupDir, fileName);

        var cs = new NpgsqlConnectionStringBuilder(AppConfig.Instance.ConnectionString);

        var psi = new ProcessStartInfo
        {
            FileName = AppConfig.Instance.PgDumpPath,
            Arguments = $"-h {cs.Host} -p {cs.Port} -U {cs.Username} -F c -b -v -f \"{fullPath}\" {cs.Database}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.Environment["PGPASSWORD"] = cs.Password;

        var (exitCode, stderr) = await RunProcessAsync(psi, cancellationToken);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"pg_dump завершился с кодом {exitCode}: {stderr}");
        }

        var info = new FileInfo(fullPath);
        return new BackupFileDto
        {
            FileName = fileName,
            FullPath = fullPath,
            SizeBytes = info.Length,
            CreatedAt = info.CreationTimeUtc
        };
    }

    public async Task RestoreAsync(string fileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new ArgumentException("Недопустимое имя файла резервной копии.");
        }

        var fullPath = Path.Combine(AppConfig.Instance.BackupPath, fileName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Файл резервной копии не найден.", fullPath);
        }

        var cs = new NpgsqlConnectionStringBuilder(AppConfig.Instance.ConnectionString);

        var psi = new ProcessStartInfo
        {
            FileName = AppConfig.Instance.PgRestorePath,
            Arguments = $"-h {cs.Host} -p {cs.Port} -U {cs.Username} -d {cs.Database} --clean --if-exists -v \"{fullPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.Environment["PGPASSWORD"] = cs.Password;

        var (exitCode, stderr) = await RunProcessAsync(psi, cancellationToken);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"pg_restore завершился с кодом {exitCode}: {stderr}");
        }
    }

    public List<BackupFileDto> ListBackups()
    {
        var dir = AppConfig.Instance.BackupPath;
        if (!Directory.Exists(dir))
        {
            return new List<BackupFileDto>();
        }

        return Directory.GetFiles(dir, "*.backup")
            .Select(f =>
            {
                var info = new FileInfo(f);
                return new BackupFileDto
                {
                    FileName = info.Name,
                    FullPath = info.FullName,
                    SizeBytes = info.Length,
                    CreatedAt = info.CreationTimeUtc
                };
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    private static async Task<(int ExitCode, string StdErr)> RunProcessAsync(ProcessStartInfo psi, CancellationToken cancellationToken)
    {
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Не удалось запустить {psi.FileName}.");

        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);
        var stderr = await stderrTask;
        return (process.ExitCode, stderr);
    }
}
