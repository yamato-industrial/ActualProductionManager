using Microsoft.AspNetCore.Mvc;

namespace ActualProductionManager.Controllers
{
    public class ProductionConditionsController : Controller
    {
        // サンプルデータ
        private static List<ProductionConditionViewModel> _conditions = new()
        {
            new ProductionConditionViewModel { LineCode = "L001", LineName = "ライン1", ItemCode = "ITEM001", ItemName = "品目A", TargetCycleTime = 60, PiecesPerCycle = 10 },
            new ProductionConditionViewModel { LineCode = "L001", LineName = "ライン1", ItemCode = "ITEM002", ItemName = "品目B", TargetCycleTime = 75, PiecesPerCycle = 8 },
            new ProductionConditionViewModel { LineCode = "L002", LineName = "ライン2", ItemCode = "ITEM001", ItemName = "品目A", TargetCycleTime = 65, PiecesPerCycle = 12 },
        };

        public IActionResult Index()
        {
            return View(_conditions);
        }

        [HttpPost]
        public IActionResult Update(string lineCode, string itemCode, int targetCycleTime, int piecesPerCycle)
        {
            var condition = _conditions.FirstOrDefault(c => c.LineCode == lineCode && c.ItemCode == itemCode);
            if (condition != null)
            {
                condition.TargetCycleTime = targetCycleTime;
                condition.PiecesPerCycle = piecesPerCycle;
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(string lineCode, string itemCode)
        {
            var condition = _conditions.FirstOrDefault(c => c.LineCode == lineCode && c.ItemCode == itemCode);
            if (condition != null)
            {
                _conditions.Remove(condition);
            }
            return RedirectToAction(nameof(Index));
        }
    }

    public class ProductionConditionViewModel
    {
        public string LineCode { get; set; } = string.Empty;
        public string LineName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int TargetCycleTime { get; set; }
        public int PiecesPerCycle { get; set; }
    }
}