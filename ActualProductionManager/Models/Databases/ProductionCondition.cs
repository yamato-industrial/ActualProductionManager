namespace ActualProductionManager.Models.Databases;

public partial class ProductionCondition
{
    public string LineCode { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public int TargetCycleTime { get; set; }
    public int PiecesPerCycle { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
