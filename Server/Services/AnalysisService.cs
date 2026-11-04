using Shared.Dtos;

namespace Server.Services;

/// <summary>
/// Математическое ядро: план-фактный анализ и факторный анализ методом цепных подстановок.
/// Все методы чистые (без обращения к БД) — легко покрываются unit-тестами.
/// </summary>
public sealed class AnalysisService
{
    private const decimal Tolerance = 0.01m;

    /// <summary>Формула (1): абсолютное отклонение Δ = Факт − План.</summary>
    public decimal GetAbsoluteDeviation(decimal fact, decimal plan) => fact - plan;

    /// <summary>
    /// Формула (2): относительное отклонение Δ% = (Факт − План) / План × 100.
    /// Если план равен нулю — возвращает 0 (защита от деления на ноль).
    /// </summary>
    public decimal GetRelativeDeviation(decimal fact, decimal plan)
    {
        if (plan == 0m)
        {
            return 0m;
        }
        return (fact - plan) / plan * 100m;
    }

    /// <summary>
    /// Факторный анализ по статье «Материалы»:
    /// Стоимость = Количество × Цена.
    /// ΔQ = (Q_факт − Q_план) × P_план — влияние количества.
    /// ΔP = Q_факт × (P_факт − P_план) — влияние цены.
    /// </summary>
    public MaterialAnalysisResult AnalyzeMaterials(decimal qPlan, decimal qFact, decimal pPlan, decimal pFact)
    {
        if (qPlan < 0 || qFact < 0 || pPlan < 0 || pFact < 0)
        {
            throw new ArgumentException("Количество и цена не могут быть отрицательными.");
        }

        var planTotal = qPlan * pPlan;
        var factTotal = qFact * pFact;
        var totalDeviation = factTotal - planTotal;
        var qImpact = (qFact - qPlan) * pPlan;
        var pImpact = qFact * (pFact - pPlan);

        return new MaterialAnalysisResult
        {
            QPlan = qPlan,
            QFact = qFact,
            PPlan = pPlan,
            PFact = pFact,
            PlanTotal = Math.Round(planTotal, 2),
            FactTotal = Math.Round(factTotal, 2),
            TotalDeviation = Math.Round(totalDeviation, 2),
            QImpact = Math.Round(qImpact, 2),
            PImpact = Math.Round(pImpact, 2),
            CheckPassed = CheckSum(totalDeviation, qImpact, pImpact)
        };
    }

    /// <summary>
    /// Факторный анализ по статье «Фонд оплаты труда»:
    /// ФОТ = Численность × Средняя зарплата.
    /// ΔЧ = (Ч_факт − Ч_план) × З_план — влияние численности.
    /// ΔЗ = Ч_факт × (З_факт − З_план) — влияние средней зарплаты.
    /// </summary>
    public LaborAnalysisResult AnalyzeLabor(decimal chPlan, decimal chFact, decimal zPlan, decimal zFact)
    {
        if (chPlan < 0 || chFact < 0 || zPlan < 0 || zFact < 0)
        {
            throw new ArgumentException("Численность и зарплата не могут быть отрицательными.");
        }

        var planTotal = chPlan * zPlan;
        var factTotal = chFact * zFact;
        var totalDeviation = factTotal - planTotal;
        var chImpact = (chFact - chPlan) * zPlan;
        var zImpact = chFact * (zFact - zPlan);

        return new LaborAnalysisResult
        {
            ChPlan = chPlan,
            ChFact = chFact,
            ZPlan = zPlan,
            ZFact = zFact,
            PlanTotal = Math.Round(planTotal, 2),
            FactTotal = Math.Round(factTotal, 2),
            TotalDeviation = Math.Round(totalDeviation, 2),
            ChImpact = Math.Round(chImpact, 2),
            ZImpact = Math.Round(zImpact, 2),
            CheckPassed = CheckSum(totalDeviation, chImpact, zImpact)
        };
    }

    /// <summary>
    /// Формула (7): проверка суммы факторных влияний.
    /// Сумма вкладов факторов должна равняться общему отклонению (с точностью 0,01).
    /// </summary>
    public bool CheckSum(decimal totalDeviation, params decimal[] factorDeviations)
    {
        var sum = factorDeviations.Sum();
        return Math.Abs(totalDeviation - sum) <= Tolerance;
    }
}
