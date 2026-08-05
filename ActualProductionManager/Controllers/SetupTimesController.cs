using ActualProductionManager.Data;
using ActualProductionManager.Models.Databases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Controllers
{
    /// <summary>
    /// 段取り時間（セットアップタイム）の管理機能を提供するコントローラー。
    /// 段取り時間の一覧表示、登録、更新、削除、および CSV インポート機能を実装します。
    /// </summary>
    public class SetupTimesController : Controller
    {
        private readonly ActualProductionContext _context;
        private readonly ILogger<SetupTimesController> _logger;

        /// <summary>
        /// SetupTimesController のコンストラクタ。
        /// </summary>
        /// <param name="context">データベースコンテキスト。</param>
        /// <param name="logger">ロギングサービス。</param>
        public SetupTimesController(
            ActualProductionContext context,
            ILogger<SetupTimesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 段取り時間データを検索・ソート・ページネーション表示します。
        /// </summary>
        /// <param name="searchLineCode">検索用ラインコード。</param>
        /// <param name="searchItemCode">検索用品目コード。</param>
        /// <param name="sortBy">ソート対象フィールド。"lineCode" または "targetSetupTime"。</param>
        /// <param name="sortOrder">ソート順序。"asc" (昇順) または "desc" (降順)。</param>
        /// <param name="pageSize">1 ページあたりの表示件数。</param>
        /// <param name="page">表示ページ番号。</param>
        /// <returns>段取り時間一覧ビュー。</returns>
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

                // フィルタ処理：ラインコードで検索
                if (!string.IsNullOrEmpty(searchLineCode))
                {
                    query = query.Where(s => s.LineCode.Contains(searchLineCode, StringComparison.OrdinalIgnoreCase));
                }

                // フィルタ処理：品目コードで検索
                if (!string.IsNullOrEmpty(searchItemCode))
                {
                    query = query.Where(s => s.ItemCode.Contains(searchItemCode, StringComparison.OrdinalIgnoreCase));
                }

                // ソート処理：指定されたフィールドと順序でソート
                query = sortBy switch
                {
                    "targetSetupTime" => sortOrder == "asc"
                        ? query.OrderBy(s => s.TargetSetupTime)
                        : query.OrderByDescending(s => s.TargetSetupTime),
                    _ => sortOrder == "asc"
                        ? query.OrderBy(s => s.LineCode).ThenBy(s => s.ItemCode)
                        : query.OrderByDescending(s => s.LineCode).ThenByDescending(s => s.ItemCode),
                };

                // ページサイズの検証：許可された値のみを受け入れる
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

                // ラインコードと品目コードを抽出
                var lineCodes = setupTimes.Select(x => x.LineCode).Distinct().ToList();
                var itemCodes = setupTimes.Select(x => x.ItemCode).Distinct().ToList();

                // 対応するラインと品目の名前をデータベースから取得してマッピング
                var lineNames = await _context.Lines
                    .Where(line => lineCodes.Contains(line.Code))
                    .ToDictionaryAsync(line => line.Code, line => line.Name);
                var itemNames = await _context.Items
                    .Where(item => itemCodes.Contains(item.Code))
                    .ToDictionaryAsync(item => item.Code, item => item.Name);

                // ビューモデルを構築
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

        /// <summary>
        /// 新規登録可能な段取り時間の候補（未登録のラインコードと品目コードの組み合わせ）を表示します。
        /// </summary>
        /// <param name="pageSize">1 ページあたりの表示件数。</param>
        /// <param name="page">表示ページ番号。</param>
        /// <returns>登録候補一覧ビュー。</returns>
        public async Task<IActionResult> Create(int pageSize = 10, int page = 1)
        {
            try
            {
                // ページサイズの検証
                var allowedPageSizes = new[] { 10, 25, 50, 100 };
                if (!allowedPageSizes.Contains(pageSize))
                {
                    pageSize = 10;
                }

                page = Math.Max(page, 1);

                // 既に登録済みのキーセットを取得
                var registeredKeys = _context.SetupTimes
                    .Select(x => new { x.LineCode, x.ItemCode });

                // 登録されていないラインと品目の組み合わせを取得
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

        /// <summary>
        /// 指定されたラインコードと品目コードの段取り時間を更新します。
        /// </summary>
        /// <param name="lineCode">ラインコード。</param>
        /// <param name="itemCode">品目コード。</param>
        /// <param name="targetSetupTimeMinutes">目標段取り時間（分単位）。</param>
        /// <returns>一覧ページへのリダイレクト。</returns>
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

        /// <summary>
        /// 指定されたラインコードと品目コードの段取り時間を削除します。
        /// </summary>
        /// <param name="lineCode">ラインコード。</param>
        /// <param name="itemCode">品目コード。</param>
        /// <returns>一覧ページへのリダイレクト。</returns>
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

                // データベースからすべてのラインデータを取得
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

                // データベースからすべての品目データを取得
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

        /// <summary>
        /// 新しい段取り時間データをデータベースに登録します。
        /// </summary>
        /// <param name="lineCode">ラインコード。</param>
        /// <param name="itemCode">品目コード。</param>
        /// <param name="targetSetupTimeMinutes">目標段取り時間（分単位）。</param>
        /// <returns>登録成功時は一覧ページへ、失敗時は登録画面へリダイレクト。</returns>
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
        /// CSV ファイルから段取り時間データをインポートします。
        /// ファイル形式：ラインコード, 品目コード, 目標段取り時間（分）
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
                    TempData["ErrorMessage"] = "CSVファイルを選択してください";
                    return RedirectToAction(nameof(Import));
                }

                if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "CSVファイルのみアップロード可能です";
                    return RedirectToAction(nameof(Import));
                }

                var importedRecords = new List<(string LineCode, string ItemCode, int Minutes)>();
                var errors = new List<string>();

                using (var stream = new StreamReader(csvFile.OpenReadStream()))
                {
                    int lineNumber = 0;
                    while (!stream.EndOfStream)
                    {
                        lineNumber++;
                        var line = await stream.ReadLineAsync();

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var columns = line.Split(',');

                        if (columns.Length != 3)
                        {
                            errors.Add($"{lineNumber}行目: 列の数が正しくありません（3列必須）");
                            continue;
                        }

                        var lineCode = columns[0].Trim();
                        var itemCode = columns[1].Trim();

                        if (!int.TryParse(columns[2].Trim(), out var minutes) || minutes <= 0)
                        {
                            errors.Add($"{lineNumber}行目: 目標段取り時間(分)は0より大きい正の整数で指定してください");
                            continue;
                        }

                        // ラインコードと品目コードが存在するか確認
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

                        importedRecords.Add((lineCode, itemCode, minutes));
                    }
                }

                if (errors.Count != 0)
                {
                    TempData["ErrorMessage"] = $"CSVファイルにエラーがあります：<br>{string.Join("<br>", errors.Take(10))}";
                    if (errors.Count > 10)
                        TempData["ErrorMessage"] += $"<br>他 {errors.Count - 10} 件のエラーがあります";
                    return RedirectToAction(nameof(Import));
                }

                if (importedRecords.Count == 0)
                {
                    TempData["ErrorMessage"] = "有効なデータが1行も見つかりませんでした";
                    return RedirectToAction(nameof(Import));
                }

                // インポートモードに応じてデータベースに取込
                if (importMode == "replace")
                {
                    // replace モード：既存データを削除
                    var existingSetupTimes = await _context.SetupTimes.ToListAsync();
                    _context.SetupTimes.RemoveRange(existingSetupTimes);
                    await _context.SaveChangesAsync();
                }

                int insertCount = 0;
                int updateCount = 0;

                foreach (var (lineCode, itemCode, minutes) in importedRecords)
                {
                    var existing = await _context.SetupTimes
                        .FirstOrDefaultAsync(s => s.LineCode == lineCode && s.ItemCode == itemCode);

                    if (existing != null)
                    {
                        existing.TargetSetupTime = minutes * 60;
                        existing.UpdatedAt = DateTime.UtcNow;
                        _context.SetupTimes.Update(existing);
                        updateCount++;
                    }
                    else
                    {
                        var newSetupTime = new SetupTime
                        {
                            LineCode = lineCode,
                            ItemCode = itemCode,
                            TargetSetupTime = minutes * 60,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.SetupTimes.Add(newSetupTime);
                        insertCount++;
                    }
                }

                await _context.SaveChangesAsync();

                var successMessage = $"取込完了しました。追加: {insertCount}件、更新: {updateCount}件";
                _logger.LogInformation(successMessage);
                TempData["SuccessMessage"] = successMessage;

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV取込エラー");
                TempData["ErrorMessage"] = $"取込処理中にエラーが発生しました: {ex.Message}";
                return RedirectToAction(nameof(Import));
            }
        }
    }

    /// <summary>
    /// 段取り時間登録画面で使用するビューモデル。
    /// 未登録のラインと品目の組み合わせ情報を表示するために使用します。
    /// </summary>
    public class SetupTimeRegistrationViewModel
    {
        /// <summary>ラインコード。</summary>
        public string LineCode { get; set; } = string.Empty;

        /// <summary>ラインの表示名。</summary>
        public string LineName { get; set; } = string.Empty;

        /// <summary>品目コード。</summary>
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>品目の表示名。</summary>
        public string ItemName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 段取り時間一覧画面で使用するビューモデル。
    /// 登録済みの段取り時間情報を表示するために使用します。
    /// </summary>
    public class SetupTimeViewModel
    {
        /// <summary>ラインコード。</summary>
        public string LineCode { get; set; } = string.Empty;

        /// <summary>ラインの表示名。</summary>
        public string LineName { get; set; } = string.Empty;

        /// <summary>品目コード。</summary>
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>品目の表示名。</summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>目標段取り時間（秒単位）。</summary>
        public int TargetSetupTimeSeconds { get; set; }

        /// <summary>目標段取り時間（分単位）。</summary>
        public int TargetSetupTimeMinutes { get; set; }
    }
}