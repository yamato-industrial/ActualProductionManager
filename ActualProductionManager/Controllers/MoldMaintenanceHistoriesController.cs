using ActualProductionManager.Data;
using ActualProductionManager.Models.Databases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Controllers
{
    public class MoldMaintenanceHistoriesController : Controller
    {
        private readonly ActualProductionContext _context;
        private readonly ILogger<MoldMaintenanceHistoriesController> _logger;

        public MoldMaintenanceHistoriesController(
            ActualProductionContext context,
            ILogger<MoldMaintenanceHistoriesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 金型修理履歴データを検索・ページネーション表示します。
        /// </summary>
        /// <param name="searchMoldCode">検索用金型コード。</param>
        /// <param name="searchMoldName">検索用金型名称。</param>
        /// <param name="fromDate">検索開始日。</param>
        /// <param name="toDate">検索終了日。</param>
        /// <param name="pageSize">1ページあたりの表示件数。</param>
        /// <param name="page">表示ページ番号。</param>
        /// <returns>金型修理履歴一覧ビュー。</returns>
        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchMoldCode,
            string? searchMoldName,
            DateTime? fromDate,
            DateTime? toDate,
            string sortBy = "maintenanceDate",
            string sortOrder = "desc",
            int pageSize = 10,
            int page = 1)
        {
            try
            {
                var query = _context.MoldMaintenanceHistories.Join(
                    _context.Molds,
                    history => history.MoldCode,
                    mold => mold.Code,
                    (history, mold) => new MoldMaintenanceHistoryViewModel
                    {
                        Id = history.Id,
                        MoldCode = mold.Code,
                        MoldName = mold.Name,
                        MaintenanceDate = history.MaintenanceDate,
                        MaintenanceShots = history.MaintenanceShots,
                        Remarks = history.Remarks
                    });

                if (!string.IsNullOrWhiteSpace(searchMoldCode))
                {
                    query = query.Where(x => EF.Functions.ILike(x.MoldCode, $"%{searchMoldCode}%"));
                }

                if (!string.IsNullOrWhiteSpace(searchMoldName))
                {
                    query = query.Where(x => EF.Functions.ILike(x.MoldName, $"%{searchMoldName}%"));
                }

                var japanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

                if (fromDate.HasValue)
                {
                    var fromLocal = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified);
                    var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, japanTimeZone);
                    query = query.Where(x => x.MaintenanceDate >= fromUtc);
                }

                if (toDate.HasValue)
                {
                    var nextDateLocal = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
                    var nextDateUtc = TimeZoneInfo.ConvertTimeToUtc(nextDateLocal, japanTimeZone);
                    query = query.Where(x => x.MaintenanceDate < nextDateUtc);
                }

                query = sortBy switch
                {
                    "moldName" => sortOrder == "asc" ? query.OrderBy(x => x.MoldName) : query.OrderByDescending(x => x.MoldName),
                    _ => sortOrder == "asc" ? query.OrderByDescending(x => x.MaintenanceDate) : query.OrderBy(x => x.MaintenanceDate)
                };

                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Min(page, totalPages);

                var viewModels = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                ViewData["SearchMoldCode"] = searchMoldCode;
                ViewData["FromDate"] = fromDate;
                ViewData["ToDate"] = toDate;
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
                _logger.LogError(ex, "修理履歴取得エラー");
                TempData["ErrorMessage"] = $"データ取得エラー: {ex.Message}";
                return View(new List<MoldMaintenanceHistoryViewModel>());
            }
        }

        /// <summary>
        /// 指定された金型修理履歴を削除します。
        /// </summary>
        /// <param name="id">金型修理履歴ID。</param>
        /// <returns>一覧画面へのリダイレクト。</returns>
        [HttpGet]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var history = await _context.MoldMaintenanceHistories.FirstOrDefaultAsync(x => x.Id == id);
                if (history == null)
                {
                    TempData["ErrorMessage"] = "削除対象が見つかりません";
                    return RedirectToAction(nameof(Index));
                }

                _context.MoldMaintenanceHistories.Remove(history);
                await _context.SaveChangesAsync();

                _logger.LogInformation("修理履歴を削除しました: {Id}", id);
                TempData["SuccessMessage"] = "削除しました。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修理履歴削除エラー");
                TempData["ErrorMessage"] = $"削除エラー: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 金型修理履歴の新規登録画面を表示します。
        /// </summary>
        /// <returns>金型修理履歴登録画面ビュー。</returns>
        [HttpGet]
        public IActionResult Create()
        {
            return View(new MoldRegistrationViewModel());
        }

        /// <summary>
        /// 新しい金型修理履歴をデータベースへ登録します。
        /// </summary>
        /// <param name="model">金型修理履歴登録用のビュー・モデル。</param>
        /// <returns>登録成功時は一覧画面へ、失敗時は登録画面へリダイレクト。</returns>
        [HttpPost]
        public async Task<IActionResult> Store(MoldMaintenanceHistoryRegistrationViewModel model)
        {
            try
            {
                var exists = await _context.Molds.AnyAsync(x => x.Code == model.MoldCode);
                if (!exists)
                {
                    TempData["ErrorMessage"] = "金型コードが存在しません";
                    return RedirectToAction(nameof(Create));
                }

                var maintenanceDateUtc = DateTime.SpecifyKind(model.MaintenanceDate, DateTimeKind.Local).ToUniversalTime();
                var history = new MoldMaintenanceHistory
                {
                    MoldCode = model.MoldCode,
                    MaintenanceDate = maintenanceDateUtc,
                    MaintenanceShots = model.MaintenanceShots,
                    Remarks = model.Remarks,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.MoldMaintenanceHistories.Add(history);
                await _context.SaveChangesAsync();

                _logger.LogInformation("修理履歴を登録しました: {MoldCode}, {MaintenanceDate}", model.MoldCode, model.MaintenanceDate);
                TempData["SuccessMessage"] = "登録しました。";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修理履歴登録エラー");
                TempData["ErrorMessage"] = $"登録エラー: {ex.Message}";
                return RedirectToAction(nameof(Create));
            }
        }

        /// <summary>
        /// 指定された金型修理履歴の編集画面を表示します。</summary>
        /// <param name="id">金型修理履歴ID。</param>
        /// <returns>金型修理履歴編集画面ビュー。</returns>
        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            try
            {
                var history = await _context.MoldMaintenanceHistories.FirstOrDefaultAsync(x => x.Id == id);
                if (history == null)
                {
                    TempData["ErrorMessage"] = "対象の修理履歴が見つかりません";
                    return RedirectToAction(nameof(Index));
                }

                var mold = await _context.Molds.FirstOrDefaultAsync(x => x.Code == history.MoldCode);
                var model =
                    new MoldMaintenanceHistoryRegistrationViewModel
                    {
                        Id = history.Id,
                        MoldCode = history.MoldCode,
                        MoldName = mold?.Name ?? string.Empty,
                        MaintenanceDate = history.MaintenanceDate,
                        MaintenanceShots = history.MaintenanceShots,
                        Remarks = history.Remarks
                    };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修理履歴編集画面表示エラー: {id}", id);
                TempData["ErrorMessage"] = $"データ取得エラー: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// 指定された金型修理履歴を更新します。
        /// </summary>
        /// <param name="id">金型修理履歴ID。</param>
        /// <param name="maintenanceDate">修理実施日時。</param>
        /// <param name="maintenanceShots">修理実施時ショット数。</param>
        /// <param name="remarks">備考。</param>
        /// <returns>一覧画面へのリダイレクト。</returns>
        [HttpPost]
        public async Task<IActionResult> Update(
            long id,
            DateTime maintenanceDate,
            long? maintenanceShots,
            string? remarks)
        {
            try
            {
                var history = await _context.MoldMaintenanceHistories.FirstOrDefaultAsync(x => x.Id == id);
                if (history == null)
                {
                    TempData["ErrorMessage"] = "更新対象が見つかりません";
                    return RedirectToAction(nameof(Index));
                }

                history.MaintenanceDate = maintenanceDate;
                history.MaintenanceShots = maintenanceShots;
                history.Remarks = remarks;
                history.UpdatedAt = DateTime.UtcNow;

                _context.MoldMaintenanceHistories.Update(history);

                await _context.SaveChangesAsync();
                _logger.LogInformation("修理履歴を更新しました: {Id}", id);
                TempData["SuccessMessage"] = "更新しました。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修理履歴更新エラー: {Id}", id);
                TempData["ErrorMessage"] = $"更新エラー: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// 金型データを検索してJSON形式で返します。
        /// 主にSelect2による金型検索で使用されます。
        /// </summary>
        /// <param name="search">検索キーワード（金型コードまたは金型名称）。</param>
        /// <returns>JSON形式の金型データ。</returns>
        [HttpGet]
        public async Task<IActionResult> GetMolds(string search = "")
        {
            try
            {
                var molds = await _context.Molds.ToListAsync();
                if (!string.IsNullOrEmpty(search))
                {
                    molds = molds
                        .Where(x =>
                            x.Code.Contains(
                                search,
                                StringComparison.OrdinalIgnoreCase) ||
                            x.Name.Contains(
                                search,
                                StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var result = molds
                    .Select(x => new
                    {
                        id = x.Code,
                        text = $"{x.Code} - {x.Name}"
                    })
                    .OrderBy(x => x.id)
                    .ToList();

                return Json(new { results = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "金型検索エラー");
                return Json(new { results = new List<object>() });
            }
        }

        /// <summary>
        /// 指定された金型コードに対応する金型名称を取得します。
        /// </summary>
        /// <param name="code">金型コード。</param>
        /// <returns>JSON形式の金型名称。金型が見つからない場合は空文字列を返します。</returns>
        [HttpGet]
        public async Task<IActionResult> GetMoldName(string code)
        {
            try
            {
                var mold = await _context.Molds
                    .FirstOrDefaultAsync(
                        x => x.Code == code);

                return Json(new
                {
                    name = mold?.Name ?? string.Empty
                });
            }
            catch
            {
                return Json(new
                {
                    name = string.Empty
                });
            }
        }
    }

    /// <summary>
    /// 金型修理履歴画面で使用するビューモデル。
    /// 入力済みの金型修理履歴を表示するために使用します。
    /// </summary>
    public class MoldMaintenanceHistoryViewModel
    {
        public long Id { get; set; }
        public string MoldCode { get; set; } = string.Empty;
        public string MoldName { get; set; } = string.Empty;
        public DateTime MaintenanceDate { get; set; }
        public long? MaintenanceShots { get; set; }
        public string? Remarks { get; set; }
    }

    /// <summary>
    /// 金型修理履歴登録画面で使用するビューモデル。
    /// </summary>
    public class MoldMaintenanceHistoryRegistrationViewModel

    {
        public long? Id { get; set; }
        public string MoldCode { get; set; } = string.Empty;
        public string MoldName { get; set; } = string.Empty;
        public DateTime MaintenanceDate { get; set; } = DateTime.Now;
        public long? MaintenanceShots { get; set; }
        public string? Remarks { get; set; }
    }
}
