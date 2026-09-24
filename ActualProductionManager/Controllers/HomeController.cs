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
        [HttpGet]
        public IActionResult Index()
        {
            var environmentName = _env.EnvironmentName;
            var schema = _configuration["Database:Schema"];
            var connectionString = _configuration["Database:ConnectionString"];

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
