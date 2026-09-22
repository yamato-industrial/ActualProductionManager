namespace ActualProductionManager.Models.Databases;

/// <summary>
/// 金型ショット実績を表すエンティティクラス。
/// ActualProductionから連携されたショット実績を保持します。
/// </summary>
public partial class MoldShotTransaction
{
    public long Id { get; set; }
    public string MoldCode { get; set; } = null!;
    public long ShotCount { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}