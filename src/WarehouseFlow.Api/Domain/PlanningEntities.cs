namespace WarehouseFlow.Api.Domain;

public enum PlanningDocumentStatus { Draft, Submitted, Confirmed, Approved, Released, Rejected, InProgress, PartiallyCompleted, Completed, Cancelled, Closed }
public enum ProductionQualityStatus { Pending, Passed, Failed, Rework }
public enum MrpSuggestionType { Production, Purchase }
public interface IPlanningDocument
{
    PlanningDocumentStatus Status { get; set; }
}

public sealed class BomHeader
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Version { get; set; } = "V1.0";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public bool IsActive { get; set; } = true;
    public Product? Product { get; set; }
    public List<BomLine> Lines { get; set; } = [];
}

public sealed class BomLine
{
    public int Id { get; set; }
    public int BomHeaderId { get; set; }
    public int ComponentProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ScrapRate { get; set; }
    public BomHeader? BomHeader { get; set; }
    public Product? ComponentProduct { get; set; }
}

public sealed class SalesOrder : IPlanningDocument
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public PlanningDocumentStatus Status { get; set; } = PlanningDocumentStatus.Draft;
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RequiredAt { get; set; }
    public List<SalesOrderLine> Lines { get; set; } = [];
}

public sealed class SalesOrderLine
{
    public int Id { get; set; }
    public int SalesOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal PushedQuantity { get; set; }
    public decimal FulfilledQuantity { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public Product? Product { get; set; }
}

public sealed class DeliveryNotice : IPlanningDocument
{
    public int Id { get; set; }
    public string NoticeNo { get; set; } = string.Empty;
    public int? SourceSalesOrderId { get; set; }
    public string SourceDocumentNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public PlanningDocumentStatus Status { get; set; } = PlanningDocumentStatus.Draft;
    public DateTimeOffset ExpectedShipAt { get; set; }
    public List<DeliveryNoticeLine> Lines { get; set; } = [];
    public SalesOrder? SourceSalesOrder { get; set; }
}

public sealed class DeliveryNoticeLine
{
    public int Id { get; set; }
    public int DeliveryNoticeId { get; set; }
    public int ProductId { get; set; }
    public int? SourceSalesOrderLineId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal PushedQuantity { get; set; }
    public decimal FulfilledQuantity { get; set; }
    public DeliveryNotice? DeliveryNotice { get; set; }
    public Product? Product { get; set; }
    public SalesOrderLine? SourceSalesOrderLine { get; set; }
}

public sealed class ReceiptNotice : IPlanningDocument
{
    public int Id { get; set; }
    public string NoticeNo { get; set; } = string.Empty;
    public string SourceDocumentNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierCode { get; set; } = string.Empty;
    public PlanningDocumentStatus Status { get; set; } = PlanningDocumentStatus.Draft;
    public DateTimeOffset ExpectedAt { get; set; }
    public List<ReceiptNoticeLine> Lines { get; set; } = [];
}

public sealed class PurchaseOrder : IPlanningDocument
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierCode { get; set; } = string.Empty;
    public PlanningDocumentStatus Status { get; set; } = PlanningDocumentStatus.Draft;
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset RequiredAt { get; set; }
    public List<PurchaseOrderLine> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLine
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal PushedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Product? Product { get; set; }
}

public sealed class ReceiptNoticeLine
{
    public int Id { get; set; }
    public int ReceiptNoticeId { get; set; }
    public int ProductId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public ReceiptNotice? ReceiptNotice { get; set; }
    public Product? Product { get; set; }
}

public sealed class ProductionOrder : IPlanningDocument
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public int? SourceMrpSuggestionId { get; set; }
    public int? RoutingId { get; set; }
    public string RoutingCode { get; set; } = string.Empty;
    public string RoutingVersion { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public decimal CompletedQuantity { get; set; }
    public decimal ReportedGoodQuantity { get; set; }
    public decimal ReportedDefectQuantity { get; set; }
    public ProductionQualityStatus QualityStatus { get; set; } = ProductionQualityStatus.Pending;
    public string QualityRemark { get; set; } = string.Empty;
    public DateTimeOffset? QualityUpdatedAt { get; set; }
    public PlanningDocumentStatus Status { get; set; } = PlanningDocumentStatus.Draft;
    public DateTimeOffset RequiredAt { get; set; }
    public Product? Product { get; set; }
    public MrpSuggestion? SourceMrpSuggestion { get; set; }
    public List<ProductionOrderMaterial> Materials { get; set; } = [];
    public List<ProductionOrderOperation> Operations { get; set; } = [];
    public List<ProductionReceipt> Receipts { get; set; } = [];
}

public sealed class ProductionOrderOperation
{
    public int Id { get; set; }
    public int ProductionOrderId { get; set; }
    public int Sequence { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public decimal StandardMinutes { get; set; }
    public bool RequiresQuality { get; set; }
    public decimal ReportedGoodQuantity { get; set; }
    public decimal ReportedDefectQuantity { get; set; }
    public string Status { get; set; } = "Pending";
    public ProductionOrder? ProductionOrder { get; set; }
}

public sealed class ProductionExecutionReport
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string Workstation { get; set; } = string.Empty;
    public decimal GoodQuantity { get; set; }
    public decimal DefectQuantity { get; set; }
    public string Operator { get; set; } = string.Empty;
    public DateTimeOffset ReportedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProductionQualityReport
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public string Checkpoint { get; set; } = string.Empty;
    public QualityInspectionResult Result { get; set; }
    public string? Remark { get; set; }
    public string Inspector { get; set; } = string.Empty;
    public DateTimeOffset InspectedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class IntegrationOutboxEvent
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public string AggregateNo { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ProductionOrderMaterial
{
    public int Id { get; set; }
    public int ProductionOrderId { get; set; }
    public int ComponentProductId { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public Product? ComponentProduct { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
}

public sealed class ProductionReceipt
{
    public int Id { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public int ProductionOrderId { get; set; }
    public int ProductId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public ProductionOrder? ProductionOrder { get; set; }
    public Product? Product { get; set; }
}

public sealed class MrpRun
{
    public int Id { get; set; }
    public string RunNo { get; set; } = string.Empty;
    public DateTime PlanDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public PlanningDocumentStatus Status { get; set; } = PlanningDocumentStatus.Completed;
    public List<MrpSuggestion> Suggestions { get; set; } = [];
}

public sealed class MrpSuggestion
{
    public int Id { get; set; }
    public int MrpRunId { get; set; }
    public int ProductId { get; set; }
    public int? ParentProductId { get; set; }
    public MrpSuggestionType Type { get; set; }
    public decimal GrossRequirement { get; set; }
    public decimal AvailableInventory { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal NetRequirement { get; set; }
    public decimal SuggestedQuantity { get; set; }
    public string SourceOrderNos { get; set; } = string.Empty;
    public PlanningDocumentStatus Status { get; set; } = PlanningDocumentStatus.Draft;
    public MrpRun? MrpRun { get; set; }
    public Product? Product { get; set; }
    public Product? ParentProduct { get; set; }
}
