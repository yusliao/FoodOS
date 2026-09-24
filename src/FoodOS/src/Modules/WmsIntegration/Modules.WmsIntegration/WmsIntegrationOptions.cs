namespace FSH.Modules.WmsIntegration;

public sealed class WmsIntegrationOptions
{
    public const string SectionName = "WmsIntegration";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string Tenant { get; set; } = "root";
    public string BaseUrl { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 15;
    public int ReplayWindowSeconds { get; set; } = 300;

    public bool IsConfigured => Enabled
        && Uri.TryCreate(BaseUrl, UriKind.Absolute, out _)
        && !string.IsNullOrWhiteSpace(Provider)
        && !string.IsNullOrWhiteSpace(ConnectionId)
        && !string.IsNullOrWhiteSpace(WarehouseId)
        && !string.IsNullOrWhiteSpace(SigningSecret);
}
