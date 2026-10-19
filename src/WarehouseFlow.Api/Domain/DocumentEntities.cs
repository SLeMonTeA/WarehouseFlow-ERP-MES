namespace WarehouseFlow.Api.Domain;

public sealed class DocumentAudit
{
    public long Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DocumentRelation
{
    public long Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceNo { get; set; } = string.Empty;
    public long? SourceLineId { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string TargetNo { get; set; } = string.Empty;
    public long? TargetLineId { get; set; }
    public decimal Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
