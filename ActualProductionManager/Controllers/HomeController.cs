using System.Diagnostics;
using ActualProductionManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace ActualProductionManager.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration, IWebHostEnvironment env)
        {
            _logger = logger;
            _configuration = configuration;
            _env = env;
        }

        public IActionResult Index()
        {
            // ŠÂ‹«•Ï”‚ğæ“¾
            var environmentName = _env.EnvironmentName; // Development or Production
            var schema = _configuration["Database:Schema"];
            var connectionString = _configuration["Database:ConnectionString"];

            // ƒrƒ…[‚Éî•ñ‚ğ“n‚·
            ViewData["EnvironmentName"] = environmentName;
            ViewData["Schema"] = schema;
            ViewData["ConnectionString"] = connectionString;

            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
