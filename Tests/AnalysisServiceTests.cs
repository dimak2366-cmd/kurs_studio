using Server.Services;
using Xunit;

public class AnalysisServiceTests
{
    private readonly AnalysisService _svc = new();

    // Пример из таблицы 1 главы: 100 × 50 → 120 × 60
    [Fact]
    public void AnalyzeMaterials_Table1Example_ReturnsExpectedImpacts()
    {
        var r = _svc.AnalyzeMaterials(qPlan: 100m, qFact: 120m, pPlan: 50m, pFact: 60m);

        Assert.Equal(5_000m,  r.PlanTotal);
        Assert.Equal(7_200m,  r.FactTotal);
        Assert.Equal(2_200m,  r.TotalDeviation);
        Assert.Equal(1_000m,  r.QImpact);
        Assert.Equal(1_200m,  r.PImpact);
        Assert.True(r.CheckPassed);
    }

    // Пример из таблицы 1 главы: 10 × 3000 → 11 × 3200
    [Fact]
    public void AnalyzeLabor_Table1Example_ReturnsExpectedImpacts()
    {
        var r = _svc.AnalyzeLabor(chPlan: 10m, chFact: 11m, zPlan: 3_000m, zFact: 3_200m);

        Assert.Equal(30_000m, r.PlanTotal);
        Assert.Equal(35_200m, r.FactTotal);
        Assert.Equal(5_200m,  r.TotalDeviation);
        Assert.Equal(3_000m,  r.ChImpact);
        Assert.Equal(2_200m,  r.ZImpact);
        Assert.True(r.CheckPassed);
    }

    [Fact]
    public void AnalyzeMaterials_SumsMatchTotalDeviation_ForArbitraryInputs()
    {
        var r = _svc.AnalyzeMaterials(qPlan: 37m, qFact: 41m, pPlan: 1_237m, pFact: 1_198m);
        Assert.True(r.CheckPassed);
        Assert.True(Math.Abs(r.TotalDeviation - (r.QImpact + r.PImpact)) <= 0.01m);
    }

    [Fact]
    public void AnalyzeMaterials_ZeroPlanQuantity_DoesNotCrash()
    {
        var r = _svc.AnalyzeMaterials(qPlan: 0m, qFact: 5m, pPlan: 100m, pFact: 80m);
        Assert.True(r.CheckPassed);
        Assert.Equal(500m, r.QImpact);      // (5−0)·100
        Assert.Equal(-100m, r.PImpact);     // 5·(80−100)
    }

    [Fact]
    public void AnalyzeLabor_ZeroPlanHeadcount_DoesNotCrash()
    {
        var r = _svc.AnalyzeLabor(chPlan: 0m, chFact: 2m, zPlan: 50_000m, zFact: 45_000m);
        Assert.True(r.CheckPassed);
    }

    [Fact]
    public void AnalyzeMaterials_NegativeInput_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            _svc.AnalyzeMaterials(qPlan: -1m, qFact: 1m, pPlan: 1m, pFact: 1m));
    }
}
