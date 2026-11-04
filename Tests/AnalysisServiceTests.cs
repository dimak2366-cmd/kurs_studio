using Server.Services;
using Xunit;

namespace Tests;

/// <summary>
/// Unit-тесты математического ядра (формулы 1–7).
/// Проверяют корректность расчёта отклонений и факторного анализа
/// методом цепных подстановок, а также устойчивость к делению на ноль
/// и отрицательным аргументам.
/// </summary>
public class AnalysisServiceTests
{
    private readonly AnalysisService _svc = new();

    // ---------- Формула (1): Δ = Факт − План ----------

    [Fact(DisplayName = "Формула (1): положительное отклонение")]
    public void GetAbsoluteDeviation_ReturnsFactMinusPlan()
    {
        Assert.Equal(500m, _svc.GetAbsoluteDeviation(1500m, 1000m));
    }

    [Fact(DisplayName = "Формула (1): отрицательное отклонение")]
    public void GetAbsoluteDeviation_HandlesNegative()
    {
        Assert.Equal(-250m, _svc.GetAbsoluteDeviation(750m, 1000m));
    }

    [Fact(DisplayName = "Формула (1): нулевое отклонение")]
    public void GetAbsoluteDeviation_ZeroWhenEqual()
    {
        Assert.Equal(0m, _svc.GetAbsoluteDeviation(1000m, 1000m));
    }

    // ---------- Формула (2): Δ% = (Факт − План)/План × 100 ----------

    [Fact(DisplayName = "Формула (2): 50% при факте 1500 и плане 1000")]
    public void GetRelativeDeviation_ReturnsPercent()
    {
        Assert.Equal(50m, _svc.GetRelativeDeviation(1500m, 1000m));
    }

    [Fact(DisplayName = "Формула (2): отрицательное отклонение −25%")]
    public void GetRelativeDeviation_HandlesNegative()
    {
        Assert.Equal(-25m, _svc.GetRelativeDeviation(750m, 1000m));
    }

    [Fact(DisplayName = "Формула (2): деление на ноль защищено (возвращает 0)")]
    public void GetRelativeDeviation_ZeroPlan_ReturnsZero()
    {
        Assert.Equal(0m, _svc.GetRelativeDeviation(100m, 0m));
    }

    // ---------- Формулы (3)–(4): Материалы ----------
    // Стоимость = Q × P; ΔQ = (Q_факт − Q_план) × P_план; ΔP = Q_факт × (P_факт − P_план)

    [Fact(DisplayName = "Формулы (3)–(4): корректное распределение влияний (100→110, 1200→1300)")]
    public void AnalyzeMaterials_CorrectImpactSplit()
    {
        var r = _svc.AnalyzeMaterials(qPlan: 100m, qFact: 110m, pPlan: 1200m, pFact: 1300m);

        Assert.Equal(120000m, r.PlanTotal);   // 100 × 1200
        Assert.Equal(143000m, r.FactTotal);   // 110 × 1300
        Assert.Equal(23000m, r.TotalDeviation);
        Assert.Equal(12000m, r.QImpact);      // (110−100) × 1200
        Assert.Equal(11000m, r.PImpact);      // 110 × (1300−1200)
        Assert.True(r.CheckPassed);
    }

    [Fact(DisplayName = "Формулы (3)–(4): нулевое количество — нулевое отклонение")]
    public void AnalyzeMaterials_ZeroQuantity_Works()
    {
        var r = _svc.AnalyzeMaterials(0m, 0m, 1000m, 1500m);
        Assert.Equal(0m, r.PlanTotal);
        Assert.Equal(0m, r.FactTotal);
        Assert.Equal(0m, r.TotalDeviation);
        Assert.True(r.CheckPassed);
    }

    [Fact(DisplayName = "Формулы (3)–(4): отрицательное количество — исключение")]
    public void AnalyzeMaterials_NegativeQuantity_Throws()
    {
        Assert.Throws<ArgumentException>(() => _svc.AnalyzeMaterials(-1m, 1m, 1m, 1m));
    }

    [Fact(DisplayName = "Формулы (3)–(4): отрицательная цена — исключение")]
    public void AnalyzeMaterials_NegativePrice_Throws()
    {
        Assert.Throws<ArgumentException>(() => _svc.AnalyzeMaterials(1m, 1m, -1m, 1m));
    }

    // ---------- Формулы (5)–(6): ФОТ ----------
    // ФОТ = Ч × З; ΔЧ = (Ч_факт − Ч_план) × З_план; ΔЗ = Ч_факт × (З_факт − З_план)

    [Fact(DisplayName = "Формулы (5)–(6): корректное распределение влияний (10→11, 50000→55000)")]
    public void AnalyzeLabor_CorrectImpactSplit()
    {
        var r = _svc.AnalyzeLabor(chPlan: 10m, chFact: 11m, zPlan: 50000m, zFact: 55000m);

        Assert.Equal(500000m, r.PlanTotal);   // 10 × 50000
        Assert.Equal(605000m, r.FactTotal);   // 11 × 55000
        Assert.Equal(105000m, r.TotalDeviation);
        Assert.Equal(50000m, r.ChImpact);     // (11−10) × 50000
        Assert.Equal(55000m, r.ZImpact);      // 11 × (55000−50000)
        Assert.True(r.CheckPassed);
    }

    [Fact(DisplayName = "Формулы (5)–(6): отрицательная численность — исключение")]
    public void AnalyzeLabor_NegativeStaff_Throws()
    {
        Assert.Throws<ArgumentException>(() => _svc.AnalyzeLabor(-1m, 1m, 1m, 1m));
    }

    // ---------- Формула (7): проверка суммы ----------

    [Fact(DisplayName = "Формула (7): точное совпадение → true")]
    public void CheckSum_ExactMatch_ReturnsTrue()
    {
        Assert.True(_svc.CheckSum(23000m, 12000m, 11000m));
    }

    [Fact(DisplayName = "Формула (7): погрешность 0.004 < 0.01 → true")]
    public void CheckSum_WithinTolerance_ReturnsTrue()
    {
        Assert.True(_svc.CheckSum(100m, 60.004m, 40m));
    }

    [Fact(DisplayName = "Формула (7): погрешность 0.02 > 0.01 → false")]
    public void CheckSum_OutsideTolerance_ReturnsFalse()
    {
        Assert.False(_svc.CheckSum(100m, 60.02m, 40m));
    }
}
