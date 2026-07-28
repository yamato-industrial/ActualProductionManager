using Microsoft.AspNetCore.Mvc;

namespace ActualProductionManager.Controllers
{
    public class SetupTimesController : Controller
    {
        // サンプルデータ
        private static List<SetupTimeViewModel> _setupTimes = new()
        {
            new SetupTimeViewModel { LineCode = "L001", LineName = "ライン1", ItemCode = "ITEM001", ItemName = "品目A", TargetSetupTime = 45 },
            new SetupTimeViewModel { LineCode = "L001", LineName = "ライン1", ItemCode = "ITEM002", ItemName = "品目B", TargetSetupTime = 50 },
            new SetupTimeViewModel { LineCode = "L002", LineName = "ライン2", ItemCode = "ITEM001", ItemName = "品目A", TargetSetupTime = 40 },
        };

        public IActionResult Index()
        {
            return View(_setupTimes);
        }

        [HttpPost]
        public IActionResult Update(string lineCode, string itemCode, int targetSetupTime)
        {
            var setupTime = _setupTimes.FirstOrDefault(s => s.LineCode == lineCode && s.ItemCode == itemCode);
            if (setupTime != null)
            {
                setupTime.TargetSetupTime = targetSetupTime;
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(string lineCode, string itemCode)
        {
            var setupTime = _setupTimes.FirstOrDefault(s => s.LineCode == lineCode && s.ItemCode == itemCode);
            if (setupTime != null)
            {
                _setupTimes.Remove(setupTime);
            }
            return RedirectToAction(nameof(Index));
        }
    }

    public class SetupTimeViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int TargetSetupTime { get; set; }
    }
}