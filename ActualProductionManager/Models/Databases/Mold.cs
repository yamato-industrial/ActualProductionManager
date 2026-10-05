namespace ActualProductionManager.Models.Databases;

/// <summary>
/// 金型情報を表すエンティティクラス。
/// 金型の基本情報およびショット数通知閾値を定義します。
/// 金型コード（NanoID）が主キーとなります。
/// </summary>
public partial class Mold
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? StorageLocation { get; set; }
    public long WarningShots { get; set; }
    public long ReplacementShots { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}