using ActualProductionManager.Data;
using ActualProductionManager.Models.Databases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Controllers
{
    /// <summary>
    /// 生産条件（ラインあたりの品目ごとのサイクルタイムと個数設定）の管理機能を提供するコントローラー。
    /// 生産条件の一覧表示、登録、更新、削除、および CSV インポート機能を実装します。
    /// </summary>
    public class ProductionConditionsController : Controller
    {
        private readonly ActualProductionContext _context;
        private readonly ILogger<ProductionConditionsController> _logger;

        /// <summary>
        /// ProductionConditionsController のコンストラクタ。
        /// </summary>
        /// <param name="context">データベースコンテキスト。</param>
        /// <param name="logger">ロギングサービス。</param>
        public ProductionConditionsController(
            ActualProductionContext context,
            ILogger<ProductionConditionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 生産条件データを検索・ソート・ページネーション表示します。
        /// </summary>
        /// <param name="searchLineCode">検索用ラインコード。</param>
        /// <param name="searchItemCode">検索用品目コード。</param>
        /// <param name="sortBy">ソート対象フィールド。"lineCode" または "targetCycleTime"。</param>
        /// <param name="sortOrder">ソート順序。"asc" (昇順) または "desc" (降順)。</param>
        /// <param name="pageSize">1 ページあたりの表示件数。</param>
        /// <param name="page">表示ページ番号。</param>
        /// <returns>生産条件一覧ビュー。</returns>
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

                if (!string.IsNullOrEmpty(searchLineCode))
                {
                    query = query.Where(c => EF.Functions.ILike(c.LineCode, $"%{searchLineCode}%"));
                }

                if (!string.IsNullOrEmpty(searchItemCode))
                {
                    query = query.Where(c => EF.Functions.ILike(c.ItemCode, $"%{searchItemCode}%"));
                }

                query = sortBy switch
                {
                    "lineCode" => sortOrder == "asc" ? query.OrderBy(c => c.LineCode) : query.OrderByDescending(c => c.LineCode),
                    "itemCode" => sortOrder == "asc" ? query.OrderBy(c => c.ItemCode) : query.OrderByDescending(c => c.ItemCode),
                    _ => sortOrder == "asc" ? query.OrderBy(c => c.LineCode).ThenBy(c => c.ItemCode) : query.OrderByDescending(c => c.LineCode).ThenByDescending(c => c.ItemCode),
                };

                var allowedPageSizes = new[] { 10, 25, 50, 100 };
                if (!allowedPageSizes.Contains(pageSize))
                {
                    pageSize = 10;
                }

                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Min(page, totalPages);

                var conditions = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                var lineCodes = conditions.Select(x => x.LineCode).Distinct().ToList();
                var itemCodes = conditions.Select(x => x.ItemCode).Distinct().ToList();

                var lineNames = await _context.Lines.Where(line => lineCodes.Contains(line.Code)).ToDictionaryAsync(line => line.Code, line => line.Name);
                var itemNames = await _context.Items.Where(item => itemCodes.Contains(item.Code)).ToDictionaryAsync(item => item.Code, item => item.Name);

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

        /// <summary>
        /// 指定されたラインコードと品目コードの生産条件を更新します。
        /// </summary>
        /// <param name="lineCode">ラインコード。</param>
        /// <param name="itemCode">品目コード。</param>
        /// <param name="targetCycleTime">目標サイクルタイム。</param>
        /// <param name="piecesPerCycle">サイクルあたりの個数。</param>
        /// <returns>一覧ページへのリダイレクト。</returns>
        [HttpPost]
        public async Task<IActionResult> Update(
            string lineCode,
            string itemCode,
            int? targetCycleTime,
            int? piecesPerCycle)
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

                condition.TargetCycleTime = targetCycleTime ?? condition.TargetCycleTime;
                condition.PiecesPerCycle = piecesPerCycle ?? condition.PiecesPerCycle;
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

        /// <summary>
        /// 指定されたラインコードと品目コードの生産条件を削除します。
        /// </summary>
        /// <param name="lineCode">ラインコード。</param>
        /// <param name="itemCode">品目コード。</param>
        /// <returns>一覧ページへのリダイレクト。</returns>
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

        /// <summary>
        /// 新規登録可能な生産条件の候補（未登録のラインコードと品目コードの組み合わせ）を表示します。
        /// </summary>
        /// <returns>生産条件新規登録ビュー。</returns>
        public IActionResult Create()
        {
            return View(new ProductionConditionRegistrationViewModel());
        }

        /// <summary>
        /// 新しい生産条件データをデータベースに登録します。
        /// </summary>
        /// <param name="model">生産条件登録用のビュー・モデル。</param>
        /// <returns>登録成功時は一覧ページへ、失敗時は登録画面へリダイレクト。</returns>
        [HttpPost]
        public async Task<IActionResult> Store(ProductionConditionRegistrationViewModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.LineCode) || string.IsNullOrEmpty(model.ItemCode))
                {
                    TempData["ErrorMessage"] = "ラインコードと品目コードは必須です";
                    return RedirectToAction(nameof(Create));
                }

                var existingCondition = await _context.ProductionConditions
                    .FirstOrDefaultAsync(c => c.LineCode == model.LineCode && c.ItemCode == model.ItemCode);

                if (existingCondition != null)
                {
                    TempData["ErrorMessage"] = "このラインコードと品目コードの組み合わせは既に登録されています";
                    return RedirectToAction(nameof(Create));
                }

                var condition = new ProductionCondition
                {
                    LineCode = model.LineCode,
                    ItemCode = model.ItemCode,
                    TargetCycleTime = model.TargetCycleTime,
                    PiecesPerCycle = model.PiecesPerCycle,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ProductionConditions.Add(condition);
                await _context.SaveChangesAsync();

                _logger.LogInformation("生産条件を登録しました: {LineCode}-{ItemCode}", model.LineCode, model.ItemCode);
                TempData["SuccessMessage"] = "登録しました。";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生産条件登録エラー");
                TempData["ErrorMessage"] = $"登録エラー: {ex.Message}";
                return RedirectToAction(nameof(Create));
            }
        }

        /// <summary>
        /// CSV ファイルのインポート画面を表示します。
        /// </summary>
        /// <returns>インポート画面ビュー。</returns>
        [HttpGet]
        public IActionResult Import()
        {
            return View();
        }

        /// <summary>
        /// CSV ファイルから生産条件データをインポートします。
        /// ファイル形式：ラインコード, 品目コード, 目標サイクルタイム, サイクルあたりの個数
        /// </summary>
        /// <param name="csvFile">アップロードされた CSV ファイル。</param>
        /// <param name="importMode">"upsert" (既存データは更新) または "replace" (既存データは削除)。</param>
        /// <returns>インポート成功時は一覧ページへ、失敗時はインポート画面へリダイレクト。</returns>
        [HttpPost]
        public async Task<IActionResult> ImportFile(IFormFile csvFile, string importMode = "upsert")
        {
            try
            {
                if (csvFile == null || csvFile.Length == 0)
                {
                    return Json(new { success = false, error = "CSVファイルを選択してください" });
                }

                if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, error = "CSVファイルのみアップロード可能です" });
                }

                var importedRecords = new List<(string LineCode, string ItemCode, int TargetCycleTime, int PiecesPerCycle)>();
                var errors = new List<string>();

                using var stream = new StreamReader(csvFile.OpenReadStream(), System.Text.Encoding.UTF8);

                int lineNumber = 0;
                while (!stream.EndOfStream)
                {
                    lineNumber++;
                    var line = await stream.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var columns = line.Split(',');

                    if (columns.Length != 4)
                    {
                        errors.Add($"{lineNumber}行目: 列の数が正しくありません（4列必須）");
                        continue;
                    }

                    var lineCode = columns[0].Trim();
                    var itemCode = columns[1].Trim();

                    if (!int.TryParse(columns[2].Trim(), out var targetCycleTime) || targetCycleTime <= 0)
                    {
                        errors.Add($"{lineNumber}行目: 目標サイクルタイムは0より大きい正の整数で指定してください");
                        continue;
                    }

                    if (!int.TryParse(columns[3].Trim(), out var piecesPerCycle) || piecesPerCycle <= 0)
                    {
                        errors.Add($"{lineNumber}行目: サイクルあたりの個数は0より大きい正の整数で指定してください");
                        continue;
                    }

                    var lineExists = await _context.Lines.AnyAsync(l => l.Code == lineCode);
                    var itemExists = await _context.Items.AnyAsync(i => i.Code == itemCode);

                    if (!lineExists)
                    {
                        errors.Add($"{lineNumber}行目: ラインコード '{lineCode}' が見つかりません");
                        continue;
                    }

                    if (!itemExists)
                    {
                        errors.Add($"{lineNumber}行目: 品目コード '{itemCode}' が見つかりません");
                        continue;
                    }

                    importedRecords.Add((lineCode, itemCode, targetCycleTime, piecesPerCycle));
                }

                if (errors.Count != 0)
                {
                    var errorMessages = errors.Take(10).ToList();
                    var errorHtml = "<ul style='text-align: left;'>" +
                        string.Join("", errorMessages.Select(e => $"<li>{e}</li>")) +
                        "</ul>";

                    if (errors.Count > 10)
                        errorHtml += $"<p>他 {errors.Count - 10} 件のエラーがあります</p>";

                    return Json(new { success = false, error = "CSVファイルにエラーがあります", details = errorHtml });
                }

                if (importedRecords.Count == 0)
                {
                    return Json(new { success = false, error = "有効なデータが1行以上必要です" });
                }

                if (importMode == "replace")
                {
                    var existingConditions = await _context.ProductionConditions.ToListAsync();
                    _context.ProductionConditions.RemoveRange(existingConditions);
                    await _context.SaveChangesAsync();
                }

                int insertCount = 0;
                int updateCount = 0;

                foreach (var (lineCode, itemCode, targetCycleTime, piecesPerCycle) in importedRecords)
                {
                    var existing = await _context.ProductionConditions
                        .FirstOrDefaultAsync(c => c.LineCode == lineCode && c.ItemCode == itemCode);

                    if (existing != null)
                    {
                        existing.TargetCycleTime = targetCycleTime;
                        existing.PiecesPerCycle = piecesPerCycle;
                        existing.UpdatedAt = DateTime.UtcNow;
                        _context.ProductionConditions.Update(existing);
                        updateCount++;
                    }
                    else
                    {
                        var newCondition = new ProductionCondition
                        {
                            LineCode = lineCode,
                            ItemCode = itemCode,
                            TargetCycleTime = targetCycleTime,
                            PiecesPerCycle = piecesPerCycle,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.ProductionConditions.Add(newCondition);
                        insertCount++;
                    }
                }

                await _context.SaveChangesAsync();

                var successMessage = $"取込完了しました。追加: {insertCount}件、更新: {updateCount}件";
                _logger.LogInformation(successMessage);

                return Json(new { success = true, message = successMessage, insertCount, updateCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV取込エラー");
                return Json(new { success = false, error = $"取込処理中にエラーが発生しました: {ex.Message}" });
            }
        }

        /// <summary>
        /// ラインデータを検索して JSON 形式で返します。
        /// 主に AJAX リクエストで使用され、オートコンプリート機能をサポートします。
        /// </summary>
        /// <param name="search">検索キーワード（ラインコードまたはライン名）。</param>
        /// <returns>JSON 形式のラインデータ。</returns>
        [HttpGet]
        public async Task<IActionResult> GetLines(string search = "")
        {
            try
            {
                _logger.LogInformation("GetLines: search parameter = '{Search}'", search);

                var lines = await _context.Lines.ToListAsync();

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

        /// <summary>
        /// 指定されたラインコードに対応するライン名を取得します。
        /// </summary>
        /// <param name="code">ラインコード。</param>
        /// <returns>JSON 形式のライン名。ラインが見つからない場合は空文字列。</returns>
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

        /// <summary>
        /// 品目データを検索して JSON 形式で返します。
        /// 主に AJAX リクエストで使用され、オートコンプリート機能をサポートします。
        /// </summary>
        /// <param name="search">検索キーワード（品目コードまたは品目名）。</param>
        /// <returns>JSON 形式の品目データ。</returns>
        [HttpGet]
        public async Task<IActionResult> GetItems(string search = "")
        {
            try
            {
                _logger.LogInformation("GetItems: search parameter = '{Search}'", search);

                var items = await _context.Items.ToListAsync();

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

        /// <summary>
        /// 指定された品目コードに対応する品目名を取得します。
        /// </summary>
        /// <param name="code">品目コード。</param>
        /// <returns>JSON 形式の品目名。品目が見つからない場合は空文字列。</returns>
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
    }

    /// <summary>
    /// 生産条件一覧画面で使用するビューモデル。
    /// 登録済みの生産条件情報を表示するために使用します。
    /// </summary>
    public class ProductionConditionViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int TargetCycleTime { get; set; }
        public int PiecesPerCycle { get; set; }
    }
    /// <summary>
    /// 生産条件登録画面で使用するビューモデル。
    /// 未登録のラインと品目の組み合わせ情報を表示するために使用します。
    /// </summary>
    public class ProductionConditionRegistrationViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public int TargetCycleTime { get; set; }
        public int PiecesPerCycle { get; set; }
    }
}