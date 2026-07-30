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
            string sortOrder = "asc",
            int pageSize = 10,
            int page = 1)
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

                var allowedPageSizes = new[] { 10, 25, 50, 100 };
                if (!allowedPageSizes.Contains(pageSize))
                {
                    pageSize = 10;
                }

                page = Math.Max(page, 1);
                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Min(page, totalPages);

                var conditions = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var lineCodes = conditions.Select(x => x.LineCode).Distinct().ToList();
                var itemCodes = conditions.Select(x => x.ItemCode).Distinct().ToList();
                var lineNames = await _context.Lines
                    .Where(line => lineCodes.Contains(line.Code))
                    .ToDictionaryAsync(line => line.Code, line => line.Name);
                var itemNames = await _context.Items
                    .Where(item => itemCodes.Contains(item.Code))
                    .ToDictionaryAsync(item => item.Code, item => item.Name);

                var viewModels = conditions.Select(c => new ProductionConditionViewModel
                {
                    LineCode = c.LineCode,
                    LineName = lineNames.GetValueOrDefault(c.LineCode, c.LineCode),
                    ItemCode = c.ItemCode,
                    ItemName = itemNames.GetValueOrDefault(c.ItemCode, c.ItemCode),
                    TargetCycleTime = c.TargetCycleTime,
                    PiecesPerCycle = c.PiecesPerCycle
                }).ToList();

                ViewData["SearchLineCode"] = searchLineCode;
                ViewData["SearchItemCode"] = searchItemCode;
                ViewData["SortBy"] = sortBy;
                ViewData["SortOrder"] = sortOrder;
                ViewData["PageSize"] = pageSize;
                ViewData["Page"] = page;
                ViewData["TotalCount"] = totalCount;
                ViewData["TotalPages"] = totalPages;

                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生産条件データ取得エラー");
                TempData["ErrorMessage"] = $"データ取得エラー: {ex.Message}";
                return View(new List<ProductionConditionViewModel>());
            }
        }

        public async Task<IActionResult> Create(int pageSize = 10, int page = 1)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var allowedPageSizes = new[] { 10, 25, 50, 100 };
                if (!allowedPageSizes.Contains(pageSize))
                {
                    pageSize = 10;
                }

                page = Math.Max(page, 1);

                var registeredKeys = _context.ProductionConditions
                    .Select(x => new { x.LineCode, x.ItemCode });

                var query =
                    from line in _context.Lines
                    from item in _context.Items
                    join registered in registeredKeys
                        on new { LineCode = line.Code, ItemCode = item.Code }
                        equals new { registered.LineCode, registered.ItemCode } into registeredGroup
                    from registered in registeredGroup.DefaultIfEmpty()
                    where registered == null
                    orderby line.Code, item.Code
                    select new ProductionConditionRegistrationViewModel
                    {
                        LineCode = line.Code,
                        LineName = line.Name,
                        ItemCode = item.Code,
                        ItemName = item.Name
                    };

                _logger.LogInformation(query.ToQueryString());

                _logger.LogInformation("Count開始");

                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Min(page, totalPages);

                _logger.LogInformation("Count終了 {Time} ms", sw.ElapsedMilliseconds);

                var viewModels = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("一覧取得終了 {Time} ms", sw.ElapsedMilliseconds);

                ViewData["PageSize"] = pageSize;
                ViewData["Page"] = page;
                ViewData["TotalCount"] = totalCount;
                ViewData["TotalPages"] = totalPages;

                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生産条件登録候補データ取得エラー");
                TempData["ErrorMessage"] = $"データ取得エラー: {ex.Message}";
                return View(new List<ProductionConditionRegistrationViewModel>());
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

    public class ProductionConditionRegistrationViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
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