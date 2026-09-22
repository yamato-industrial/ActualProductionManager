namespace ActualProductionManager.Models.Databases;

/// <summary>
/// 金型修理履歴を表すエンティティクラス。
/// 金型に対して実施した修理履歴を保持します。
/// </summary>
public partial class MoldMaintenanceHistory
{
    public long Id { get; set; }
    public string MoldCode { get; set; } = null!;
    public DateTime MaintenanceDate { get; set; }
    public long? MaintenanceShots { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}