using ActualProductionManager.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Controllers
{
    public class ProductionConditionsController : Controller
    {
        private readonly ActualProductionContext _context;
        private readonly ILogger<ProductionConditionsController> _logger;

        public ProductionConditionsController(
            ActualProductionContext context,
            ILogger<ProductionConditionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(
            string? searchLineCode,
            string? searchItemCode,
            string sortBy = "lineCode",
            string sortOrder = "asc")
        {
            try
            {
                var query = _context.ProductionConditions.AsQueryable();

                // フィルタ処理
                if (!string.IsNullOrEmpty(searchLineCode))
                {
                    query = query.Where(c => c.LineCode.Contains(searchLineCode));
                }

                if (!string.IsNullOrEmpty(searchItemCode))
                {
                    query = query.Where(c => c.ItemCode.Contains(searchItemCode));
                }

                // ソート処理
                query = sortBy switch
                {
                    "targetCycleTime" => sortOrder == "asc"
                        ? query.OrderBy(c => c.TargetCycleTime)
                        : query.OrderByDescending(c => c.TargetCycleTime),
                    _ => sortOrder == "asc"
                        ? query.OrderBy(c => c.LineCode).ThenBy(c => c.ItemCode)
                        : query.OrderByDescending(c => c.LineCode).ThenByDescending(c => c.ItemCode),
                };

                var conditions = await query.ToListAsync();

                var viewModels = conditions.Select(c => new ProductionConditionViewModel
                {
                    LineCode = c.LineCode,
                    LineName = c.LineCode,
                    ItemCode = c.ItemCode,
                    ItemName = c.ItemCode,
                    TargetCycleTime = c.TargetCycleTime,
                    PiecesPerCycle = c.PiecesPerCycle
                }).ToList();

                ViewData["SearchLineCode"] = searchLineCode;
                ViewData["SearchItemCode"] = searchItemCode;
                ViewData["SortBy"] = sortBy;
                ViewData["SortOrder"] = sortOrder;

                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生産条件データ取得エラー");
                TempData["ErrorMessage"] = $"データ取得エラー: {ex.Message}";
                return View(new List<ProductionConditionViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Update(
            string lineCode,
            string itemCode,
            int targetCycleTime,
            int piecesPerCycle)
        {
            try
            {
                var condition = await _context.ProductionConditions
                    .FirstOrDefaultAsync(c => c.LineCode == lineCode && c.ItemCode == itemCode);

                if (condition == null)
                {
                    _logger.LogWarning("更新対象の生産条件が見つかりません: {LineCode}-{ItemCode}", lineCode, itemCode);
                    TempData["ErrorMessage"] = "更新対象のデータが見つかりません";
                    return RedirectToAction(nameof(Index));
                }

                condition.TargetCycleTime = targetCycleTime;
                condition.PiecesPerCycle = piecesPerCycle;
                condition.UpdatedAt = DateTime.UtcNow;

                _context.ProductionConditions.Update(condition);
                await _context.SaveChangesAsync();

                _logger.LogInformation("生産条件を更新しました: {LineCode}-{ItemCode}", lineCode, itemCode);
                TempData["SuccessMessage"] = "更新しました。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生産条件更新エラー: {LineCode}-{ItemCode}", lineCode, itemCode);
                TempData["ErrorMessage"] = $"更新エラー: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string lineCode, string itemCode)
        {
            try
            {
                var condition = await _context.ProductionConditions
                    .FirstOrDefaultAsync(c => c.LineCode == lineCode && c.ItemCode == itemCode);

                if (condition == null)
                {
                    _logger.LogWarning("削除対象の生産条件が見つかりません: {LineCode}-{ItemCode}", lineCode, itemCode);
                    TempData["ErrorMessage"] = "削除対象のデータが見つかりません";
                    return RedirectToAction(nameof(Index));
                }

                _context.ProductionConditions.Remove(condition);
                await _context.SaveChangesAsync();

                _logger.LogInformation("生産条件を削除しました: {LineCode}-{ItemCode}", lineCode, itemCode);
                TempData["SuccessMessage"] = "削除しました。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生産条件削除エラー: {LineCode}-{ItemCode}", lineCode, itemCode);
                TempData["ErrorMessage"] = $"削除エラー: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }

    public class ProductionConditionViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int TargetCycleTime { get; set; }
        public int PiecesPerCycle { get; set; }
    }
}