namespace ActualProductionManager.Models.Databases;

/// <summary>
/// セットアップ時間情報を表すエンティティクラス。
/// 特定のラインで特定の製品に切り替える際に必要なセットアップ時間を定義します。
/// ラインコードと製品コードの組み合わせが複合主キーとなります。
/// </summary>
public partial class SetupTime
{
    public string LineCode { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public int TargetSetupTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
