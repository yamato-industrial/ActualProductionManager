namespace ActualProductionManager.Models.Databases;

public partial class SetupTime
{
    public string LineCode { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public int TargetSetupTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
