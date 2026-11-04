using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Security;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

[ApiController]
[Route("api/analysis")]
public class AnalysisController : ControllerBase
{
    private readonly AnalysisService _analysis;
    private readonly AppDbContext _db;

    public AnalysisController(AnalysisService analysis, AppDbContext db)
    {
        _analysis = analysis;
        _db = db;
    }

    // ---------- План-фактный анализ по всей смете ----------

    /// <summary>Общее отклонение по смете с разбивкой по статьям.</summary>
    [HttpGet("deviation/{smetaId:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> GetDeviation(int smetaId, CancellationToken cancellationToken)
    {
        var smeta = await _db.Smetas
            .Include(s => s.ExpenseItems).ThenInclude(i => i.ActualExpenses)
            .FirstOrDefaultAsync(s => s.Id == smetaId, cancellationToken);

        if (smeta == null)
        {
            return NotFound(new { error = "Смета не найдена." });
        }

        var items = smeta.ExpenseItems.Select(i =>
        {
            var plan = i.PlanAmount;
            var fact = i.ActualExpenses.Sum(a => a.Amount);
            return new ExpenseItemDeviationDto
            {
                ExpenseItemId = i.Id,
                Name = i.Name,
                KosguCode = i.KosguCode,
                Plan = plan,
                Fact = fact,
                Absolute = _analysis.GetAbsoluteDeviation(fact, plan),
                RelativePercent = _analysis.GetRelativeDeviation(fact, plan)
            };
        }).ToList();

        var totalPlan = items.Sum(x => x.Plan);
        var totalFact = items.Sum(x => x.Fact);

        return Ok(new SmetaDeviationReportDto
        {
            SmetaId = smeta.Id,
            FormCode = smeta.FormCode,
            Year = smeta.Year,
            TotalPlan = totalPlan,
            TotalFact = totalFact,
            TotalAbsolute = _analysis.GetAbsoluteDeviation(totalFact, totalPlan),
            TotalRelativePercent = _analysis.GetRelativeDeviation(totalFact, totalPlan),
            Items = items
        });
    }

    // ---------- Факторный анализ «Материалы» ----------

    /// <summary>
    /// Расчёт по формулам (3)–(4) по переданным параметрам.
    /// Пример: /api/analysis/materials?qPlan=100&amp;qFact=110&amp;pPlan=1200&amp;pFact=1300
    /// </summary>
    [HttpGet("materials")]
    [AuthorizeRole]
    public IActionResult AnalyzeMaterialsByParams(
        [FromQuery] decimal qPlan,
        [FromQuery] decimal qFact,
        [FromQuery] decimal pPlan,
        [FromQuery] decimal pFact)
    {
        try
        {
            return Ok(_analysis.AnalyzeMaterials(qPlan, qFact, pPlan, pFact));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Факторный анализ материалов по статье из БД.
    /// План Q и P берутся из статьи, факт — сумма фактических расходов.
    /// При отсутствии отдельного учёта Q_факт предполагается равным Q_план,
    /// тогда P_факт = Факт / Q_план (средняя фактическая цена).
    /// </summary>
    [HttpGet("materials/{smetaId:int}/{expenseItemId:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> AnalyzeMaterialsFromDb(int smetaId, int expenseItemId, CancellationToken cancellationToken)
    {
        var item = await _db.ExpenseItems
            .Include(i => i.ActualExpenses)
            .FirstOrDefaultAsync(i => i.Id == expenseItemId && i.SmetaId == smetaId, cancellationToken);

        if (item == null)
        {
            return NotFound(new { error = "Статья не найдена в указанной смете." });
        }

        if (item.Quantity <= 0)
        {
            return BadRequest(new { error = "У статьи не задано количество (Quantity <= 0). Факторный анализ невозможен." });
        }

        var qPlan = item.Quantity;
        var pPlan = item.Price;

        // Упрощение курсовой: в модели ActualExpense нет отдельного поля ActualQuantity,
        // поэтому фактическое количество принимается равным плановому (qFact = qPlan),
        // а фактическая цена вычисляется как средняя: pFact = Σ(Факт) / Q_план.
        // В промышленной системе потребовалось бы поле ActualQuantity в ActualExpense
        // или отдельная таблица детализации расхода по количеству и цене.
        var qFact = qPlan;
        var factSum = item.ActualExpenses.Sum(a => a.Amount);
        var pFact = qFact > 0 ? Math.Round(factSum / qFact, 4) : 0m;

        return Ok(_analysis.AnalyzeMaterials(qPlan, qFact, pPlan, pFact));
    }

    // ---------- Факторный анализ «Фонд оплаты труда» ----------

    /// <summary>
    /// Расчёт по формулам (5)–(6) по переданным параметрам.
    /// Пример: /api/analysis/labor?chPlan=10&amp;chFact=11&amp;zPlan=50000&amp;zFact=55000
    /// </summary>
    [HttpGet("labor")]
    [AuthorizeRole]
    public IActionResult AnalyzeLaborByParams(
        [FromQuery] decimal chPlan,
        [FromQuery] decimal chFact,
        [FromQuery] decimal zPlan,
        [FromQuery] decimal zFact)
    {
        try
        {
            return Ok(_analysis.AnalyzeLabor(chPlan, chFact, zPlan, zFact));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Факторный анализ ФОТ по статье из БД. Аналогично материалам:
    /// Ч_факт предполагается равной Ч_план, З_факт = Факт / Ч_план.
    /// </summary>
    [HttpGet("labor/{smetaId:int}/{expenseItemId:int}")]
    [AuthorizeRole]
    public async Task<IActionResult> AnalyzeLaborFromDb(int smetaId, int expenseItemId, CancellationToken cancellationToken)
    {
        var item = await _db.ExpenseItems
            .Include(i => i.ActualExpenses)
            .FirstOrDefaultAsync(i => i.Id == expenseItemId && i.SmetaId == smetaId, cancellationToken);

        if (item == null)
        {
            return NotFound(new { error = "Статья не найдена в указанной смете." });
        }

        if (item.Quantity <= 0)
        {
            return BadRequest(new { error = "У статьи не задана численность (Quantity <= 0). Факторный анализ невозможен." });
        }

        var chPlan = item.Quantity;
        var zPlan = item.Price;

        // Упрощение курсовой: аналогично материалам — фактическая численность
        // принимается равной плановой (chFact = chPlan), а фактическая средняя
        // зарплата вычисляется как zFact = Σ(Факт) / Ч_план.
        // В промышленной системе нужна таблица табельного учёта (численность по месяцам).
        var chFact = chPlan;
        var factSum = item.ActualExpenses.Sum(a => a.Amount);
        var zFact = chFact > 0 ? Math.Round(factSum / chFact, 4) : 0m;

        return Ok(_analysis.AnalyzeLabor(chPlan, chFact, zPlan, zFact));
    }
}
