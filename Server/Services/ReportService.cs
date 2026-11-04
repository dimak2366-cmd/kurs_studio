using System.Text;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Аналитические отчёты по смете: формирование CSV (Главный экономист),
/// утверждение (Директор), выдача содержимого / файла.
/// </summary>
public sealed class ReportService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public ReportService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<List<ReportDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var list = await _db.AnalyticReports
            .Include(r => r.Smeta)
            .Include(r => r.CreatedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        return list.Select(ToDto).ToList();
    }

    public async Task<ReportDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var r = await _db.AnalyticReports
            .Include(x => x.Smeta)
            .Include(x => x.CreatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Отчёт не найден.");
        return ToDetailDto(r);
    }

    public async Task<ReportDetailDto> CreateAsync(int smetaId, int userId, CancellationToken cancellationToken)
    {
        var smeta = await _db.Smetas
            .Include(s => s.Project)
            .Include(s => s.ExpenseItems).ThenInclude(i => i.ActualExpenses)
            .FirstOrDefaultAsync(s => s.Id == smetaId, cancellationToken)
            ?? throw new KeyNotFoundException("Смета не найдена.");

        var csv = BuildCsv(smeta);

        var entity = new AnalyticReport
        {
            SmetaId = smeta.Id,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Status = "На утверждении",
            Content = csv
        };
        _db.AnalyticReports.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Create", "AnalyticReport", entity.Id.ToString(),
            $"Сформирован аналитический отчёт по смете #{smetaId}", cancellationToken);

        entity.Smeta = smeta;
        entity.CreatedByUser = await _db.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        return ToDetailDto(entity);
    }

    public async Task<ReportDto> ApproveAsync(int id, int userId, CancellationToken cancellationToken)
    {
        var entity = await _db.AnalyticReports
            .Include(r => r.Smeta)
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Отчёт не найден.");

        if (entity.Status != "На утверждении")
        {
            throw new InvalidOperationException($"Утвердить можно только отчёт в статусе «На утверждении». Текущий статус: «{entity.Status}».");
        }

        entity.Status = "Утверждён";
        await _db.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(userId, "Approve", "AnalyticReport", id.ToString(),
            $"Утверждён отчёт #{id}", cancellationToken);

        return ToDto(entity);
    }

    public static string BuildFileName(AnalyticReport report) =>
        $"report_smeta{report.SmetaId}_{report.CreatedAt:yyyyMMdd_HHmmss}.csv";

    private static string BuildCsv(Smeta smeta)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Отчёт по смете;{smeta.FormCode};за {smeta.Year} год;проект;{smeta.Project?.Code ?? ""}");
        sb.AppendLine("Статья;КОСГУ;План, BYN;Факт, BYN;Отклонение, BYN;Отклонение, %");

        decimal totalPlan = 0, totalFact = 0;
        foreach (var item in smeta.ExpenseItems)
        {
            var plan = item.PlanAmount;
            var fact = item.ActualExpenses.Sum(a => a.Amount);
            var dev = fact - plan;
            var rel = plan != 0 ? dev / plan * 100m : 0m;
            totalPlan += plan;
            totalFact += fact;
            sb.AppendLine($"{Escape(item.Name)};{item.KosguCode};{plan:F2};{fact:F2};{dev:F2};{rel:F2}");
        }

        var totalDev = totalFact - totalPlan;
        var totalRel = totalPlan != 0 ? totalDev / totalPlan * 100m : 0m;
        sb.AppendLine($"Итого;;{totalPlan:F2};{totalFact:F2};{totalDev:F2};{totalRel:F2}");
        return sb.ToString();
    }

    private static string Escape(string value) =>
        value.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");

    public static ReportDto ToDto(AnalyticReport r) => new()
    {
        Id = r.Id,
        SmetaId = r.SmetaId,
        SmetaFormCode = r.Smeta?.FormCode ?? string.Empty,
        SmetaYear = r.Smeta?.Year ?? 0,
        CreatedBy = r.CreatedBy,
        CreatedByLogin = r.CreatedByUser?.Login ?? string.Empty,
        CreatedAt = r.CreatedAt,
        Status = r.Status,
        ContentLength = r.Content?.Length ?? 0
    };

    public static ReportDetailDto ToDetailDto(AnalyticReport r)
    {
        var dto = new ReportDetailDto();
        var baseDto = ToDto(r);
        dto.Id = baseDto.Id;
        dto.SmetaId = baseDto.SmetaId;
        dto.SmetaFormCode = baseDto.SmetaFormCode;
        dto.SmetaYear = baseDto.SmetaYear;
        dto.CreatedBy = baseDto.CreatedBy;
        dto.CreatedByLogin = baseDto.CreatedByLogin;
        dto.CreatedAt = baseDto.CreatedAt;
        dto.Status = baseDto.Status;
        dto.ContentLength = baseDto.ContentLength;
        dto.Content = r.Content;
        return dto;
    }
}
