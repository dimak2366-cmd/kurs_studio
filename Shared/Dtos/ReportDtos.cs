namespace Shared.Dtos;

public class ReportDto
{
    public int Id { get; set; }
    public int SmetaId { get; set; }
    public string SmetaFormCode { get; set; } = string.Empty;
    public int SmetaYear { get; set; }
    public int CreatedBy { get; set; }
    public string CreatedByLogin { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ContentLength { get; set; }
}

public class ReportDetailDto : ReportDto
{
    public string Content { get; set; } = string.Empty;
}

public class CreateReportRequest
{
    public int SmetaId { get; set; }
}
