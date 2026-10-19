namespace WarehouseFlow.Api.Domain;

public enum QualityInspectionStatus { Draft, Submitted, Completed, Cancelled }
public enum QualityInspectionResult { Pending, Passed, Failed, Rework }

public sealed class QualityInspection
{
    public int Id { get; set; }
    public string InspectionNo { get; set; } = string.Empty;
    public string SourceInboundOrderNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public QualityInspectionStatus Status { get; set; } = QualityInspectionStatus.Draft;
    public string Inspector { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<QualityInspectionLine> Lines { get; set; } = [];
}

public sealed class QualityInspectionLine
{
    public int Id { get; set; }
    public int QualityInspectionId { get; set; }
    public int ProductId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal QualifiedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public QualityInspectionResult Result { get; set; } = QualityInspectionResult.Pending;
    public string Remark { get; set; } = string.Empty;
    public QualityInspection? QualityInspection { get; set; }
    public Product? Product { get; set; }
}
