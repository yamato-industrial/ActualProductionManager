namespace ActualProductionManager.Models.Databases;

public partial class Line
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
