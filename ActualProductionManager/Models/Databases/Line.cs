namespace ActualProductionManager.Models.Databases;

/// <summary>
/// 生産ライン情報を表すエンティティクラス。
/// 工場内の各生産ラインの情報を管理します。
/// </summary>
public partial class Line
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
