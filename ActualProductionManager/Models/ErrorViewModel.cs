namespace ActualProductionManager.Models
{
    /// <summary>
    /// エラー情報を表示するためのビューモデル。
    /// </summary>
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
