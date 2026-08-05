namespace ActualProductionManager.Models.Databases;

/// <summary>
/// 生産条件情報を表すエンティティクラス。
/// 特定のラインにおける特定の製品の生産条件を定義します。
/// ラインコードと製品コードの組み合わせが複合主キーとなります。
/// </summary>
public partial class ProductionCondition
{
    public string LineCode { get; set; } = null!;
    public string ItemCode { get; set; } = null!;
    public int TargetCycleTime { get; set; }
    public int PiecesPerCycle { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
