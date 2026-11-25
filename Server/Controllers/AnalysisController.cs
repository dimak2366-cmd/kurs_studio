using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;
using Shared.Dtos;

namespace Server.Controllers;

/// <summary>
/// План-фактный и факторный анализ. Закрывает фронтовые вызовы:
///   GET /api/analysis/deviation/{smetaId}
///   GET /api/analysis/materials[?smetaId=N]  и  GET /api/analysis/materials/{smetaId}
///   GET /api/analysis/labor[?smetaId=N]      и  GET /api/analysis/labor/{smetaId}
/// </summary>
[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AnalysisService _analysis;

    public AnalysisController(AppDbContext db, AnalysisService analysis)
    {
        _db = db;
        _analysis = analysis;
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/analysis/deviation/{smetaId}
    // ──────────────────────────────────────────────────────────────
    [HttpGet("deviation/{smetaId:int}")]
    public async Task<ActionResult<SmetaDeviationReportDto>> GetDeviation(
        int smetaId, CancellationToken ct)
    {
        var smeta = await _db.Smetas
            .AsNoTracking()
            .Include(s => s.ExpenseItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == smetaId, ct);

        if (smeta is null)
            return NotFound(new { message = $"Смета {smetaId} не найдена" });

        var itemIds = smeta.ExpenseItems.Select(i => i.Id).ToList();

        var factByItem = itemIds.Count == 0
            ? new Dictionary<int, decimal>()
            : await _db.ActualExpenses
                .Where(a => itemIds.Contains(a.ExpenseItemId))
                .GroupBy(a => a.ExpenseItemId)
                .Select(g => new { ItemId = g.Key, Sum = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.ItemId, x => x.Sum, ct);

        var rows = smeta.ExpenseItems
            .OrderBy(i => i.KosguCode).ThenBy(i => i.Name)
            .Select(i =>
            {
                var fact = factByItem.TryGetValue(i.Id, out var s) ? s : 0m;
                return new ExpenseItemDeviationDto
                {
                    ExpenseItemId   = i.Id,
                    Name            = i.Name,
                    KosguCode       = i.KosguCode,
                    Plan            = i.PlanAmount,
                    Fact            = fact,
                    Absolute        = _analysis.GetAbsoluteDeviation(fact, i.PlanAmount),
                    RelativePercent = _analysis.GetRelativeDeviation(fact, i.PlanAmount),
                };
            })
            .ToList();

        var totalPlan = rows.Sum(r => r.Plan);
        var totalFact = rows.Sum(r => r.Fact);

        return Ok(new SmetaDeviationReportDto
        {
            SmetaId              = smeta.Id,
            FormCode             = smeta.FormCode,
            Year                 = smeta.Year,
            TotalPlan            = totalPlan,
            TotalFact            = totalFact,
            TotalAbsolute        = totalFact - totalPlan,
            TotalRelativePercent = _analysis.GetRelativeDeviation(totalFact, totalPlan),
            Items                = rows,
        });
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/analysis/materials  (query) и /materials/{smetaId} (path)
    // ──────────────────────────────────────────────────────────────
    [HttpGet("materials")]
    public Task<ActionResult<List<MaterialAnalysisResult>>> GetMaterials(
        [FromQuery] int? smetaId, CancellationToken ct)
        => GetMaterialsCore(smetaId, ct);

    [HttpGet("materials/{smetaId:int}")]
    public Task<ActionResult<List<MaterialAnalysisResult>>> GetMaterialsBySmeta(
        [FromRoute] int smetaId, CancellationToken ct)
        => GetMaterialsCore(smetaId, ct);

    private async Task<ActionResult<List<MaterialAnalysisResult>>> GetMaterialsCore(
        int? smetaId, CancellationToken ct)
    {
        var q = _db.ExpenseItems.AsNoTracking().Where(i => i.Name == "Материалы");
        if (smetaId.HasValue) q = q.Where(i => i.SmetaId == smetaId.Value);
        var items = await q.ToListAsync(ct);

        if (items.Count == 0)
            return Ok(new List<MaterialAnalysisResult>());

        var itemIds = items.Select(i => i.Id).ToList();
        var factByItem = await _db.ActualExpenses
            .Where(a => itemIds.Contains(a.ExpenseItemId))
            .GroupBy(a => a.ExpenseItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                Amount = g.Sum(x => x.Amount),
                Qty    = g.Sum(x => x.Quantity),
            })
            .ToDictionaryAsync(x => x.ItemId, x => x, ct);

        var result = new List<MaterialAnalysisResult>(items.Count);
        foreach (var i in items)
        {
            factByItem.TryGetValue(i.Id, out var f);
            var qFact = f?.Qty ?? 0m;
            var pFact = qFact > 0m ? Math.Round((f?.Amount ?? 0m) / qFact, 2) : 0m;
            result.Add(_analysis.AnalyzeMaterials(
                qPlan: i.Quantity, qFact: qFact,
                pPlan: i.Price,    pFact: pFact));
        }
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/analysis/labor  (query) и /labor/{smetaId} (path)
    // ──────────────────────────────────────────────────────────────
    [HttpGet("labor")]
    public Task<ActionResult<List<LaborAnalysisResult>>> GetLabor(
        [FromQuery] int? smetaId, CancellationToken ct)
        => GetLaborCore(smetaId, ct);

    [HttpGet("labor/{smetaId:int}")]
    public Task<ActionResult<List<LaborAnalysisResult>>> GetLaborBySmeta(
        [FromRoute] int smetaId, CancellationToken ct)
        => GetLaborCore(smetaId, ct);

    private async Task<ActionResult<List<LaborAnalysisResult>>> GetLaborCore(
        int? smetaId, CancellationToken ct)
    {
        var q = _db.ExpenseItems.AsNoTracking().Where(i => i.Name == "Фонд оплаты труда");
        if (smetaId.HasValue) q = q.Where(i => i.SmetaId == smetaId.Value);
        var items = await q.ToListAsync(ct);

        if (items.Count == 0)
            return Ok(new List<LaborAnalysisResult>());

        var itemIds = items.Select(i => i.Id).ToList();
        var factByItem = await _db.ActualExpenses
            .Where(a => itemIds.Contains(a.ExpenseItemId))
            .GroupBy(a => a.ExpenseItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                Amount = g.Sum(x => x.Amount),
                Qty    = g.Sum(x => x.Quantity),
            })
            .ToDictionaryAsync(x => x.ItemId, x => x, ct);

        var result = new List<LaborAnalysisResult>(items.Count);
        foreach (var i in items)
        {
            factByItem.TryGetValue(i.Id, out var f);
            var chFact = f?.Qty ?? 0m;
            var zFact = chFact > 0m ? Math.Round((f?.Amount ?? 0m) / chFact, 2) : 0m;
            result.Add(_analysis.AnalyzeLabor(
                chPlan: i.Quantity, chFact: chFact,
                zPlan: i.Price,     zFact: zFact));
        }
        return Ok(result);
    }
}
