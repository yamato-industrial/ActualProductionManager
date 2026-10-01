using ActualProductionManager.Data;
using ActualProductionManager.Models.Databases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ActualProductionManager.Controllers
{
    /// <summary>
    /// 金型修理履歴管理機能を提供するコントローラーです。
    /// 金型修理履歴の一覧表示、検索、登録、編集、および削除処理を管理します。
    /// </summary>
    /// <param name="context">データベースコンテキスト。</param>
    /// <param name="logger">ロギングサービス。</param>
    public class MoldMaintenanceHistoriesController(ActualProductionContext context, ILogger<MoldMaintenanceHistoriesController> logger) : Controller
    {
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
            DateOnly? fromDate,
            DateOnly? toDate,
            string sortBy = "maintenanceDate",
            string sortOrder = "desc",
            int pageSize = 10,
            int page = 1)
        {
            try
            {
                var query = context.MoldMaintenanceHistories.Join(
                    context.Molds,
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

                if (fromDate.HasValue)
                {
                    query = query.Where(x => x.MaintenanceDate >= fromDate);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(x => x.MaintenanceDate <= toDate);
                }

                query = sortBy switch
                {
                    "moldName" => sortOrder == "asc" ? query.OrderBy(x => x.MoldName) : query.OrderByDescending(x => x.MoldName),
                    _ => sortOrder == "asc" ? query.OrderBy(x => x.MaintenanceDate) : query.OrderByDescending(x => x.MaintenanceDate)
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
                logger.LogError(ex, "修理履歴取得エラー");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// 指定された金型修理履歴を削除します。
        /// </summary>
        /// <param name="id">金型修理履歴ID。</param>
        /// <returns>Json結果。</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var history = await context.MoldMaintenanceHistories.FirstAsync(x => x.Id == id);
                context.MoldMaintenanceHistories.Remove(history);
                await context.SaveChangesAsync();

                logger.LogInformation("修理履歴を削除しました: {Id}", id);

                return Json(new
                {
                    success = true,
                    message = "削除しました。"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "修理履歴削除エラー");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "削除処理中にエラーが発生しました。"
                    });
            }
        }

        /// <summary>
        /// 金型修理履歴の新規登録画面を表示します。
        /// </summary>
        /// <returns>金型修理履歴登録画面ビュー。</returns>
        [HttpGet]
        public IActionResult Create()
        {
            return View(new MoldMaintenanceHistoryRegistrationViewModel());
        }

        /// <summary>
        /// 新しい金型修理履歴をデータベースへ登録します。
        /// </summary>
        /// <param name="model">金型修理履歴登録用のビューモデル。</param>
        /// <returns>登録結果をJSON形式で返します。</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Store(MoldMaintenanceHistoryRegistrationViewModel model)
        {
            try
            {
                var history = new MoldMaintenanceHistory
                {
                    MoldCode = model.MoldCode,
                    MaintenanceDate = model.MaintenanceDate,
                    MaintenanceShots = model.MaintenanceShots,
                    Remarks = model.Remarks,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.MoldMaintenanceHistories.Add(history);
                await context.SaveChangesAsync();

                logger.LogInformation("修理履歴を登録しました: {Id}, {MoldCode}, {MaintenanceDate}", history.Id, model.MoldCode, model.MaintenanceDate);

                return Json(new
                {
                    success = true,
                    message = "登録しました。",
                    id = history.Id,
                    redirectUrl = Url.Action(nameof(Index))
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "修理履歴登録エラー: {MoldCode}", model.MoldCode);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "登録処理中にエラーが発生しました。"
                    });
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
                var history = await context.MoldMaintenanceHistories.FirstAsync(x => x.Id == id);
                var mold = await context.Molds.FirstOrDefaultAsync(x => x.Code == history.MoldCode);
                var model = new MoldMaintenanceHistoryRegistrationViewModel
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
                logger.LogError(ex, "修理履歴編集画面表示エラー: {id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// 指定された金型修理履歴を更新します。
        /// </summary>
        /// <param name="model">/// 金型修理履歴更新用のビューモデル。/// </param>
        /// <returns>更新結果をJSON形式で返します。</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(MoldMaintenanceHistoryRegistrationViewModel model)
        {
            try
            {
                var history = await context.MoldMaintenanceHistories.FirstAsync(x => x.Id == model.Id);
                history.MaintenanceDate = model.MaintenanceDate;
                history.MaintenanceShots = model.MaintenanceShots;
                history.Remarks = model.Remarks;
                history.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync();

                logger.LogInformation("修理履歴を更新しました: {Id}", model.Id);

                return Json(new
                {
                    success = true,
                    message = "更新しました。",
                    redirectUrl = Url.Action(nameof(Index))
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "修理履歴更新エラー: {Id}", model.Id);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        success = false,
                        message = "更新処理中にエラーが発生しました。"
                    });
            }
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
                var molds = await context.Molds.ToListAsync();
                if (!string.IsNullOrEmpty(search))
                {
                    molds = [.. molds
                        .Where(x =>
                            x.Code.Contains(search,StringComparison.OrdinalIgnoreCase) ||
                            x.Name.Contains(search,StringComparison.OrdinalIgnoreCase))];
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
                logger.LogError(ex, "金型検索エラー");
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
                var mold = await context.Molds.FirstOrDefaultAsync(x => x.Code == code);
                return Json(new { name = mold?.Name ?? string.Empty });
            }
            catch
            {
                return Json(new { name = string.Empty });
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
        public DateOnly MaintenanceDate { get; set; }
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
        public DateOnly MaintenanceDate { get; set; }
        public long? MaintenanceShots { get; set; }
        public string? Remarks { get; set; }
    }
}
