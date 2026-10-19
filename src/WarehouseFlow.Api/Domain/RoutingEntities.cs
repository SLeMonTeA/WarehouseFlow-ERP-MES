namespace WarehouseFlow.Api.Domain;

public sealed class RoutingHeader
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Version { get; set; } = "V1.0";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public Product? Product { get; set; }
    public List<RoutingOperation> Operations { get; set; } = [];
}

public sealed class RoutingOperation
{
    public int Id { get; set; }
    public int RoutingHeaderId { get; set; }
    public int Sequence { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public decimal StandardMinutes { get; set; }
    public bool RequiresQuality { get; set; }
    public RoutingHeader? RoutingHeader { get; set; }
}
