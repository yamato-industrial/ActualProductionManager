using ActualProductionManager.Data;
using ActualProductionManager.Models.Databases;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ActualProductionManager.Controllers
{
    /// <summary>
    /// 金型マスタの管理機能を提供するコントローラー。
    /// 金型の一覧表示、登録、更新、削除、および CSV インポート機能を実装します。
    /// </summary>
    /// <param name="context">データベースコンテキスト。</param>
    /// <param name="logger">ロギングサービス。</param>
    public class MoldsController(ActualProductionContext context, ILogger<MoldsController> logger) : Controller
    {
        /// <summary>
        /// 金型データを検索・ソート・ページネーション表示します。
        /// </summary>
        /// <param name="searchMoldCode">検索用金型コード。</param>
        /// <param name="searchMoldName">検索用金型名称。</param>
        /// <param name="searchStorageLocation">検索用置場。</param>
        /// <param name="sortBy">ソート対象フィールド。</param>
        /// <param name="sortOrder">ソート順序。asc または desc。</param>
        /// <param name="pageSize">1ページあたりの表示件数。</param>
        /// <param name="page">表示ページ番号。</param>
        /// <returns>金型一覧ビュー。</returns>
        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchMoldCode,
            string? searchMoldName,
            string? searchStorageLocation,
            string sortBy = "storageLocation",
            string sortOrder = "asc",
            int pageSize = 10,
            int page = 1)
        {
            try
            {
                var query = context.Molds.AsQueryable();

                if (!string.IsNullOrEmpty(searchMoldCode))
                {
                    query = query.Where(m => EF.Functions.ILike(m.Code, $"%{searchMoldCode}%"));
                }

                if (!string.IsNullOrEmpty(searchMoldName))
                {
                    query = query.Where(m => EF.Functions.ILike(m.Name, $"%{searchMoldName}%"));
                }

                if (!string.IsNullOrEmpty(searchStorageLocation))
                {
                    query = query.Where(m => m.StorageLocation != null && EF.Functions.ILike(m.StorageLocation, $"%{searchStorageLocation}%"));
                }

                query = sortBy switch
                {
                    "moldName" => sortOrder == "asc" ? query.OrderBy(c => c.Name) : query.OrderByDescending(c => c.Name),
                    _ => sortOrder == "asc" ? query.OrderBy(c => c.StorageLocation) : query.OrderByDescending(c => c.StorageLocation)
                };

                var allowedPageSizes = new[] { 10, 25, 50, 100 };
                if (!allowedPageSizes.Contains(pageSize))
                {
                    pageSize = 10;
                }

                var totalCount = await query.CountAsync();
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                page = Math.Min(page, totalPages);

                var molds = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

                var viewModels = molds.Select(c => new MoldViewModel
                {
                    MoldCode = c.Code,
                    MoldName = c.Name,
                    StorageLocation = c.StorageLocation,
                    WarningShots = c.WarningShots,
                    ReplacementShots = c.ReplacementShots,
                    Remarks = c.Remarks
                }).ToList();

                ViewData["SearchMoldCode"] = searchMoldCode;
                ViewData["SearchMoldName"] = searchMoldName;
                ViewData["SearchStorageLocation"] = searchStorageLocation;
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
                logger.LogError(ex, "金型データ取得エラー");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// 指定された金型を更新します。
        /// 金型コードは実物に印字されたNanoIDのため変更しません。
        /// </summary>
        /// <param name="moldCode">金型コード。</param>
        /// <param name="storageLocation">置場。</param>
        /// <param name="warningShots">注意ショット数。</param>
        /// <param name="replacementShots">交換ショット数。</param>
        /// <param name="remarks">備考。</param>
        /// <returns>金型一覧画面へのリダイレクト。</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(
            string moldCode,
            string? storageLocation,
            long warningShots,
            long replacementShots,
            string? remarks)
        {
            try
            {
                if (warningShots >= replacementShots)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        new
                        {
                            success = false,
                            message = "注意ショット数は交換ショット数より小さい値を指定してください。"
                        });
                }

                var mold = await context.Molds.FirstAsync(m => m.Code == moldCode);
                mold.StorageLocation = storageLocation;
                mold.WarningShots = warningShots;
                mold.ReplacementShots = replacementShots;
                mold.Remarks = remarks;
                mold.UpdatedAt = DateTime.UtcNow;

                context.Molds.Update(mold);
                await context.SaveChangesAsync();

                logger.LogInformation("金型を更新しました: {Code}", moldCode);
                return Json(new
                {
                    success = true,
                    message = "更新しました。",
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "金型更新エラー: {Code}", moldCode);
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
        /// 指定された金型を削除します。
        /// </summary>
        /// <remarks>
        /// 金型ショット実績または金型修理履歴から参照されている場合、
        /// データベースの外部キー制約により削除できません。
        /// </remarks>
        /// <param name="moldCode">金型コード。</param>
        /// <returns>金型一覧画面へのリダイレクト。</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string moldCode)
        {
            try
            {
                var mold = await context.Molds.FirstAsync(m => m.Code == moldCode);
                context.Molds.Remove(mold);
                await context.SaveChangesAsync();

                logger.LogInformation("金型を削除しました: {Code}", moldCode);
                return Json(new
                {
                    success = true,
                    message = "削除しました。"
                });
            }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "参照データが存在するため金型を削除できません: {Code}", moldCode);
                return StatusCode(
                    StatusCodes.Status400BadRequest,
                    new
                    {
                        success = false,
                        message = "ショット実績または修理履歴から参照されているため、この金型は削除できません。"
                    });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "金型削除エラー: {Code}", moldCode);
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
        /// 金型の新規登録画面を表示します。
        /// </summary>
        /// <returns>金型新規登録ビュー。</returns>
        [HttpGet]
        public IActionResult Create()
        {
            return View(new MoldRegistrationViewModel());
        }

        /// <summary>
        /// 新しい金型をデータベースに登録します。
        /// </summary>
        /// <param name="model">金型登録要のビュー・モデル。</param>
        /// <returns>登録成功時は一覧ページへ、失敗時は登録画面へリダイレクト。</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Store(MoldRegistrationViewModel model)
        {
            try
            {
                var exists = await context.Molds.AnyAsync(m => m.Code == model.MoldCode);
                if (exists)
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        new
                        {
                            success = false,
                            message = "この金型コードは既に登録されています。"
                        });
                }

                if (!(model.ReplacementShots >= model.WarningShots))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        new
                        {
                            success = false,
                            message = "交換ショット数は注意ショット数以上を指定してください。"
                        });
                }

                var mold = new Mold
                {
                    Code = model.MoldCode,
                    Name = model.MoldName,
                    StorageLocation = model.StorageLocation,
                    WarningShots = model.WarningShots,
                    ReplacementShots = model.ReplacementShots,
                    Remarks = model.Remarks,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.Molds.Add(mold);
                await context.SaveChangesAsync();

                logger.LogInformation("金型を登録しました: {Code}", model.MoldCode);
                return Json(new
                {
                    success = true,
                    message = "登録しました。",
                    redirectUrl = Url.Action(nameof(Index))
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "金型登録エラー: {Code}", model.MoldCode);
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
        /// CSVファイルのインポート画面を表示します。
        /// </summary>
        /// <returns>インポート画面ビュー。</returns>
        [HttpGet]
        public IActionResult Import()
        {
            return View();
        }

        /// <summary>
        /// CSVファイルから金型マスタをインポートします。
        /// ファイル形式：
        /// 金型コード,金型名称,置場,注意ショット数,交換ショット数,備考
        /// </summary>
        /// <param name="csvFile">アップロードされたCSVファイル。</param>
        /// <param name="importMode">
        /// upsertの場合は既存データを更新し、新規データを追加します。
        /// </param>
        /// <returns>インポート結果をJSON形式で返します。</returns>
        [HttpPost]
        public async Task<IActionResult> ImportFile(IFormFile csvFile)
        {
            try
            {
                if (csvFile == null || csvFile.Length == 0)
                {
                    return Json(new { success = false, error = "CSVファイルを選択してください。" });
                }

                if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, error = "CSVファイルのみアップロード可能です。" });
                }

                var importedRecords = new List<MoldCsvImportRecord>();
                var errors = new List<string>();

                using var stream = new StreamReader(csvFile.OpenReadStream(), System.Text.Encoding.UTF8);

                var lineNumber = 0;
                while (!stream.EndOfStream)
                {
                    lineNumber++;
                    var line = await stream.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var columns = ParseCsvLine(line);

                    if (columns.Count != 6)
                    {
                        errors.Add($"{lineNumber}行目: 列の数が正しくありません（6列必須）");
                        continue;
                    }

                    var code = columns[0].Trim();
                    var name = columns[1].Trim();
                    var storageLocation = columns[2];
                    var remarks = columns[5];

                    if (string.IsNullOrWhiteSpace(code))
                    {
                        errors.Add($"{lineNumber}行目: 金型コードは必須です");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add($"{lineNumber}行目: 金型名称は必須です");
                        continue;
                    }

                    if (!long.TryParse(columns[3].Trim(), out var warningShots) || warningShots < 0)
                    {
                        errors.Add($"{lineNumber}行目: 注意ショット数は0以上の整数で指定してください");
                        continue;
                    }

                    if (!long.TryParse(columns[4].Trim(), out var replacementShots) || replacementShots <= 0)
                    {
                        errors.Add($"{lineNumber}行目: 交換ショット数は0より大きい整数で指定してください");
                        continue;
                    }

                    if (!(replacementShots >= warningShots))
                    {
                        errors.Add($"{lineNumber}行目: 交換ショット数は注意ショット数以上を指定してください");
                        continue;
                    }

                    importedRecords.Add(
                        new MoldCsvImportRecord
                        {
                            LineNumber = lineNumber,
                            Code = code,
                            Name = name,
                            StorageLocation = storageLocation,
                            WarningShots = warningShots,
                            ReplacementShots = replacementShots,
                            Remarks = remarks
                        });
                }

                var duplicateCodes = importedRecords
                    .GroupBy(
                        record => record.Code,
                        StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .ToList();

                foreach (var duplicate in duplicateCodes)
                {
                    var lineNumbers = string.Join(", ", duplicate.Select(record => record.LineNumber));
                    errors.Add($"金型コード '{duplicate.Key}' がCSV内で重複しています。対象行: {lineNumbers}");
                }

                if (errors.Count > 0)
                {
                    return CreateCsvErrorResponse(errors);
                }

                if (importedRecords.Count == 0)
                {
                    return Json(new { success = false, error = "有効なデータが1行以上必要です。" });
                }

                var importedCodes = importedRecords.Select(record => record.Code).ToList();
                var existingMolds = await context.Molds
                    .Where(mold => importedCodes.Contains(mold.Code))
                    .ToDictionaryAsync(mold => mold.Code);
                var now = DateTime.UtcNow;
                var insertCount = 0;
                var updateCount = 0;

                foreach (var record in importedRecords)
                {
                    if (existingMolds.TryGetValue(record.Code, out var existing))
                    {
                        existing.Name = record.Name;
                        existing.StorageLocation = record.StorageLocation;
                        existing.WarningShots = record.WarningShots;
                        existing.ReplacementShots = record.ReplacementShots;
                        existing.Remarks = record.Remarks;
                        existing.UpdatedAt = now;

                        updateCount++;
                    }
                    else
                    {
                        var mold = new Mold
                        {
                            Code = record.Code,
                            Name = record.Name,
                            StorageLocation = record.StorageLocation,
                            WarningShots = record.WarningShots,
                            ReplacementShots = record.ReplacementShots,
                            Remarks = record.Remarks,
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        context.Molds.Add(mold);
                        insertCount++;
                    }
                }

                await context.SaveChangesAsync();

                var successMessage = $"取込完了しました。追加: {insertCount}件、更新: {updateCount}件";
                logger.LogInformation("金型CSV取込完了。追加: {InsertCount}件、更新: {UpdateCount}件", insertCount, updateCount);

                return Json(new { success = true, message = successMessage, insertCount, updateCount });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "金型CSV取込エラー");
                return Json(new { success = false, error = $"取込処理中にエラーが発生しました: {ex.Message}" });
            }
        }

        /// <summary>
        /// CSVの1行を解析します。
        /// 二重引用符で囲まれたカンマを含む値に対応します。
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var columns = new List<string>();
            var value = new StringBuilder();
            var insideQuotes = false;

            for (var index = 0; index < line.Length; index++)
            {
                var current = line[index];

                if (current == '"')
                {
                    if (insideQuotes &&
                        index + 1 < line.Length &&
                        line[index + 1] == '"')
                    {
                        value.Append('"');
                        index++;
                    }
                    else
                    {
                        insideQuotes = !insideQuotes;
                    }

                    continue;
                }

                if (current == ',' && !insideQuotes)
                {
                    columns.Add(value.ToString());
                    value.Clear();
                    continue;
                }

                value.Append(current);
            }

            columns.Add(value.ToString());

            return columns;
        }

        /// <summary>
        /// CSV検証エラーのJSONレスポンスを作成します。
        /// </summary>
        private JsonResult CreateCsvErrorResponse(List<string> errors)
        {
            var displayErrors = errors.Take(10).ToList();

            var errorHtml =
                "<ul style='text-align: left;'>" +
                string.Join(
                    string.Empty,
                    displayErrors.Select(error =>
                        $"<li>{System.Net.WebUtility.HtmlEncode(error)}</li>")) +
                "</ul>";

            if (errors.Count > 10)
            {
                errorHtml +=
                    $"<p>他 {errors.Count - 10} 件のエラーがあります</p>";
            }

            return Json(new
            {
                success = false,
                error = "CSVファイルにエラーがあります",
                details = errorHtml
            });
        }

        /// <summary>
        /// CSV取込中に使用する内部データ。
        /// </summary>
        private sealed class MoldCsvImportRecord
        {
            public int LineNumber { get; init; }
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public string? StorageLocation { get; init; }
            public long WarningShots { get; init; }
            public long ReplacementShots { get; init; }
            public string? Remarks { get; init; }
        }
    }

    /// <summary>
    /// 金型一覧画面で使用するビューモデル。
    /// </summary>
    public class MoldViewModel
    {
        public string MoldCode { get; set; } = string.Empty;
        public string MoldName { get; set; } = string.Empty;
        public string? StorageLocation { get; set; }
        public long WarningShots { get; set; }
        public long ReplacementShots { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 金型登録画面で使用するビューモデル。
    /// </summary>
    public class MoldRegistrationViewModel
    {
        public string MoldCode { get; set; } = string.Empty;
        public string MoldName { get; set; } = string.Empty;
        public string? StorageLocation { get; set; }
        public long WarningShots { get; set; }
        public long ReplacementShots { get; set; }
        public string? Remarks { get; set; }
    }
}