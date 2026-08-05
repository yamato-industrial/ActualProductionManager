using ActualProductionManager.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ActualProductionManager.Controllers
{
    /// <summary>
    /// ホームページの表示と例外処理を担当するコントローラークラス。
    /// アプリケーションのトップページおよびエラーページのレンダリングを処理します。
    /// </summary>
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// HomeControllerのコンストラクタ。
        /// </summary>
        /// <param name="logger">ロギングサービス。</param>
        /// <param name="configuration">アプリケーション設定。</param>
        /// <param name="env">ウェブホスト環境情報。</param>
        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IWebHostEnvironment env)
        {
            _logger = logger;
            _configuration = configuration;
            _env = env;
        }

        /// <summary>
        /// ホームページ (インデックスページ) を表示します。
        /// 現在の環境情報とデータベース接続情報をビューに渡します。
        /// </summary>
        /// <returns>ホームページビュー。</returns>
        public IActionResult Index()
        {
            // 実行環境名（Development または Production）を取得
            var environmentName = _env.EnvironmentName;
            // 設定ファイルからスキーマ名を取得
            var schema = _configuration["Database:Schema"];
            // 設定ファイルから接続文字列を取得
            var connectionString = _configuration["Database:ConnectionString"];

            // ビューで使用するデータを ViewData に格納
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
