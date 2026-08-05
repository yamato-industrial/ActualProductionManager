namespace ActualProductionManager.Models.Databases;

/// <summary>
/// 製品アイテム情報を表すエンティティクラス。
/// 生産管理システムで扱う各製品の基本情報を保持します。
/// </summary>
public partial class Item
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
}
