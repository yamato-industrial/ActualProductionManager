using ActualProductionManager.Data;
using ActualProductionManager.Models.Databases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Controllers
{
    public class SetupTimesController : Controller
    {
        private readonly ActualProductionContext _context;
        private readonly ILogger<SetupTimesController> _logger;

        public SetupTimesController(
            ActualProductionContext context,
            ILogger<SetupTimesController> logger)
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
                var query = _context.SetupTimes.AsQueryable();

                // フィルタ処理
                if (!string.IsNullOrEmpty(searchLineCode))
                {
                    query = query.Where(s => s.LineCode.Contains(searchLineCode, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(searchItemCode))
                {
                    query = query.Where(s => s.ItemCode.Contains(searchItemCode, StringComparison.OrdinalIgnoreCase));
                }

                // ソート処理
                query = sortBy switch
                {
                    "targetSetupTime" => sortOrder == "asc"
                        ? query.OrderBy(s => s.TargetSetupTime)
                        : query.OrderByDescending(s => s.TargetSetupTime),
                    _ => sortOrder == "asc"
                        ? query.OrderBy(s => s.LineCode).ThenBy(s => s.ItemCode)
                        : query.OrderByDescending(s => s.LineCode).ThenByDescending(s => s.ItemCode),
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

                var setupTimes = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var lineCodes = setupTimes.Select(x => x.LineCode).Distinct().ToList();
                var itemCodes = setupTimes.Select(x => x.ItemCode).Distinct().ToList();
                var lineNames = await _context.Lines
                    .Where(line => lineCodes.Contains(line.Code))
                    .ToDictionaryAsync(line => line.Code, line => line.Name);
                var itemNames = await _context.Items
                    .Where(item => itemCodes.Contains(item.Code))
                    .ToDictionaryAsync(item => item.Code, item => item.Name);

                var viewModels = setupTimes.Select(s => new SetupTimeViewModel
                {
                    LineCode = s.LineCode,
                    LineName = lineNames.GetValueOrDefault(s.LineCode, s.LineCode),
                    ItemCode = s.ItemCode,
                    ItemName = itemNames.GetValueOrDefault(s.ItemCode, s.ItemCode),
                    TargetSetupTimeSeconds = s.TargetSetupTime,
                    TargetSetupTimeMinutes = s.TargetSetupTime / 60
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
                _logger.LogError(ex, "段取り時間データ取得エラー");
                TempData["ErrorMessage"] = $"データ取得エラー: {ex.Message}";
                return View(new List<SetupTimeViewModel>());
            }
        }

        public async Task<IActionResult> Create(int pageSize = 10, int page = 1)
        {
            try
            {
                var allowedPageSizes = new[] { 10, 25, 50, 100 };
                if (!allowedPageSizes.Contains(pageSize))
                {
                    pageSize = 10;
                }

                page = Math.Max(page, 1);

                var registeredKeys = _context.SetupTimes
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
                    select new SetupTimeRegistrationViewModel
                    {
                        LineCode = line.Code,
                        LineName = line.Name,
                        ItemCode = item.Code,
                        ItemName = item.Name
                    };

                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Min(page, totalPages);

                var viewModels = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewData["PageSize"] = pageSize;
                ViewData["Page"] = page;
                ViewData["TotalCount"] = totalCount;
                ViewData["TotalPages"] = totalPages;

                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "段取り時間登録候補データ取得エラー");
                TempData["ErrorMessage"] = $"データ取得エラー: {ex.Message}";
                return View(new List<SetupTimeRegistrationViewModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Update(
            string lineCode,
            string itemCode,
            int targetSetupTimeMinutes)
        {
            try
            {
                var setupTime = await _context.SetupTimes
                    .FirstOrDefaultAsync(s => s.LineCode == lineCode && s.ItemCode == itemCode);

                if (setupTime == null)
                {
                    _logger.LogWarning("更新対象の段取り時間が見つかりません: {LineCode}-{ItemCode}", lineCode, itemCode);
                    TempData["ErrorMessage"] = "更新対象のデータが見つかりません";
                    return RedirectToAction(nameof(Index));
                }

                // 分を秒に変換してデータベースに保存
                setupTime.TargetSetupTime = targetSetupTimeMinutes * 60;
                setupTime.UpdatedAt = DateTime.UtcNow;

                _context.SetupTimes.Update(setupTime);
                await _context.SaveChangesAsync();

                _logger.LogInformation("段取り時間を更新しました: {LineCode}-{ItemCode} ({Minutes}分)", lineCode, itemCode, targetSetupTimeMinutes);
                TempData["SuccessMessage"] = "更新しました。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "段取り時間更新エラー: {LineCode}-{ItemCode}", lineCode, itemCode);
                TempData["ErrorMessage"] = $"更新エラー: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string lineCode, string itemCode)
        {
            try
            {
                var setupTime = await _context.SetupTimes
                    .FirstOrDefaultAsync(s => s.LineCode == lineCode && s.ItemCode == itemCode);

                if (setupTime == null)
                {
                    _logger.LogWarning("削除対象の段取り時間が見つかりません: {LineCode}-{ItemCode}", lineCode, itemCode);
                    TempData["ErrorMessage"] = "削除対象のデータが見つかりません";
                    return RedirectToAction(nameof(Index));
                }

                _context.SetupTimes.Remove(setupTime);
                await _context.SaveChangesAsync();

                _logger.LogInformation("段取り時間を削除しました: {LineCode}-{ItemCode}", lineCode, itemCode);
                TempData["SuccessMessage"] = "削除しました。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "段取り時間削除エラー: {LineCode}-{ItemCode}", lineCode, itemCode);
                TempData["ErrorMessage"] = $"削除エラー: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetLines(string search = "")
        {
            try
            {
                _logger.LogInformation("GetLines: search parameter = '{Search}'", search);

                // まずすべてのデータを取得
                var lines = await _context.Lines.ToListAsync();

                // クライアント側でフィルタリング
                if (!string.IsNullOrEmpty(search))
                {
                    lines = [.. lines.Where(l => l.Code.Contains(search, StringComparison.OrdinalIgnoreCase) || l.Name.Contains(search, StringComparison.OrdinalIgnoreCase))];
                }

                var result = lines
                    .Select(l => new { id = l.Code, text = $"{l.Code} - {l.Name}" })
                    .OrderBy(l => l.id)
                    .ToList();

                _logger.LogInformation("GetLines: returning {Count} results", result.Count);

                return Json(new { results = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ラインデータ取得エラー");
                return Json(new { results = new List<object>(), error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLineName(string code)
        {
            try
            {
                var line = await _context.Lines.FirstOrDefaultAsync(l => l.Code == code);
                if (line == null)
                {
                    return Json(new { name = string.Empty });
                }

                return Json(new { name = line.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ライン名取得エラー: {Code}", code);
                return Json(new { name = string.Empty, error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetItems(string search = "")
        {
            try
            {
                _logger.LogInformation("GetItems: search parameter = '{Search}'", search);

                // まずすべてのデータを取得
                var items = await _context.Items.ToListAsync();

                // クライアント側でフィルタリング
                if (!string.IsNullOrEmpty(search))
                {
                    items = [.. items.Where(i => i.Code.Contains(search, StringComparison.OrdinalIgnoreCase) || i.Name.Contains(search, StringComparison.OrdinalIgnoreCase))];
                }

                var result = items
                    .Select(i => new { id = i.Code, text = $"{i.Code} - {i.Name}" })
                    .OrderBy(i => i.id)
                    .ToList();

                _logger.LogInformation("GetItems: returning {Count} results", result.Count);

                return Json(new { results = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "品目データ取得エラー");
                return Json(new { results = new List<object>(), error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetItemName(string code)
        {
            try
            {
                var item = await _context.Items.FirstOrDefaultAsync(i => i.Code == code);
                if (item == null)
                {
                    return Json(new { name = string.Empty });
                }

                return Json(new { name = item.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "品目名取得エラー: {Code}", code);
                return Json(new { name = string.Empty, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Store(
            string lineCode,
            string itemCode,
            int targetSetupTimeMinutes)
        {
            try
            {
                if (string.IsNullOrEmpty(lineCode) || string.IsNullOrEmpty(itemCode))
                {
                    TempData["ErrorMessage"] = "ラインコードと品目コードは必須です";
                    return RedirectToAction(nameof(Create));
                }

                var existingSetupTime = await _context.SetupTimes
                    .FirstOrDefaultAsync(s => s.LineCode == lineCode && s.ItemCode == itemCode);

                if (existingSetupTime != null)
                {
                    TempData["ErrorMessage"] = "このラインコードと品目コードの組み合わせは既に登録されています";
                    return RedirectToAction(nameof(Create));
                }

                // 分を秒に変換してデータベースに保存
                var setupTime = new SetupTime
                {
                    LineCode = lineCode,
                    ItemCode = itemCode,
                    TargetSetupTime = targetSetupTimeMinutes * 60,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.SetupTimes.Add(setupTime);
                await _context.SaveChangesAsync();

                _logger.LogInformation("段取り時間を登録しました: {LineCode}-{ItemCode} ({Minutes}分)", lineCode, itemCode, targetSetupTimeMinutes);
                TempData["SuccessMessage"] = "登録しました。";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "段取り時間登録エラー");
                TempData["ErrorMessage"] = $"登録エラー: {ex.Message}";
                return RedirectToAction(nameof(Create));
            }
        }
    }

    public class SetupTimeRegistrationViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
    }

    public class SetupTimeViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int TargetSetupTimeSeconds { get; set; }
        public int TargetSetupTimeMinutes { get; set; }
    }
}