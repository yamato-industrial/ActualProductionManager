using ActualProductionManager.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ActualProductionManager.Controllers
{
    /// <summary>
    /// ホームページの表示と例外処理を担当するコントローラークラス。
    /// アプリケーションのトップページおよびエラーページのレンダリングを処理します。
    /// </summary>
    /// <param name="logger">ロギングサービス。</param>
    /// <param name="configuration">アプリケーション設定。</param>
    /// <param name="env">ウェブホスト環境情報。</param>
    public class HomeController(IConfiguration configuration, IWebHostEnvironment env) : Controller
    {

        /// <summary>
        /// ホームページ (インデックスページ) を表示します。
        /// 現在の環境情報とデータベース接続情報をビューに渡します。
        /// </summary>
        /// <returns>ホームページビュー。</returns>
        [HttpGet]
        public IActionResult Index()
        {
            var environmentName = env.EnvironmentName;
            var schema = configuration["Database:Schema"];
            var connectionString = configuration["Database:ConnectionString"];

            ViewData["EnvironmentName"] = environmentName;
            ViewData["Schema"] = schema;
            ViewData["ConnectionString"] = connectionString;

            return View();
        }

        /// <summary>
        /// エラーページを表示します。
        /// レスポンスキャッシュを無効化し、毎回最新の状態で表示します。
        /// </summary>
        /// <returns>エラーページビュー。</returns>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
