namespace Shared.Dtos;

/// <summary>Результат план-фактного анализа одной статьи или всей сметы.</summary>
public class DeviationResult
{
    public decimal Plan { get; set; }
    public decimal Fact { get; set; }
    public decimal Absolute { get; set; }
    public decimal RelativePercent { get; set; }
}

/// <summary>Результат факторного анализа по статье «Материалы».</summary>
public class MaterialAnalysisResult
{
    public decimal QPlan { get; set; }
    public decimal QFact { get; set; }
    public decimal PPlan { get; set; }
    public decimal PFact { get; set; }
    public decimal PlanTotal { get; set; }
    public decimal FactTotal { get; set; }
    public decimal TotalDeviation { get; set; }
    public decimal QImpact { get; set; }
    public decimal PImpact { get; set; }
    public bool CheckPassed { get; set; }
}

/// <summary>Результат факторного анализа по статье «Фонд оплаты труда».</summary>
public class LaborAnalysisResult
{
    public decimal ChPlan { get; set; }
    public decimal ChFact { get; set; }
    public decimal ZPlan { get; set; }
    public decimal ZFact { get; set; }
    public decimal PlanTotal { get; set; }
    public decimal FactTotal { get; set; }
    public decimal TotalDeviation { get; set; }
    public decimal ChImpact { get; set; }
    public decimal ZImpact { get; set; }
    public bool CheckPassed { get; set; }
}

/// <summary>Полный отчёт по смете: общее отклонение + постатейная разбивка.</summary>
public class SmetaDeviationReportDto
{
    public int SmetaId { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal TotalPlan { get; set; }
    public decimal TotalFact { get; set; }
    public decimal TotalAbsolute { get; set; }
    public decimal TotalRelativePercent { get; set; }
    public List<ExpenseItemDeviationDto> Items { get; set; } = new();
}

public class ExpenseItemDeviationDto
{
    public int ExpenseItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KosguCode { get; set; } = string.Empty;
    public decimal Plan { get; set; }
    public decimal Fact { get; set; }
    public decimal Absolute { get; set; }
    public decimal RelativePercent { get; set; }
}
