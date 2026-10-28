namespace WarehouseFlow.Api.Domain;

public enum InventoryMovementType { Receive, OtherInbound, Reserve, Release, Ship, Issue, OtherOutbound, Adjust, Transfer, QualityHold, QualityRelease, PurchaseReturn, SalesReturn }
public enum OrderStatus { Draft, Confirmed, InProgress, Completed, Cancelled }

public sealed class Warehouse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<WarehouseLocation> Locations { get; set; } = [];
}

public sealed class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Specification { get; set; } = string.Empty;
    public string Unit { get; set; } = "件";
    public decimal SafetyStock { get; set; }
    public decimal MaxStock { get; set; }
    public bool RequiresQuality { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WarehouseLocation
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public Warehouse? Warehouse { get; set; }
}

public sealed class InventoryBalance
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int LocationId { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal QualityHold { get; set; }
    public string BatchNo { get; set; } = "UNBATCHED";
    public DateTimeOffset? ExpiryDate { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public Product? Product { get; set; }
    public WarehouseLocation? Location { get; set; }
    public List<OutboundAllocation> OutboundAllocations { get; set; } = [];
}

public sealed class InventoryMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int LocationId { get; set; }
    public InventoryMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public string BatchNo { get; set; } = "UNBATCHED";
    public DateTimeOffset? ExpiryDate { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Product? Product { get; set; }
    public WarehouseLocation? Location { get; set; }
}

public sealed class InboundOrder
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierCode { get; set; } = string.Empty;
    public string SourceDocumentNo { get; set; } = string.Empty;
    public int? SourceReceiptNoticeId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public DateTimeOffset ExpectedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<InboundLine> Lines { get; set; } = [];
    public ReceiptNotice? SourceReceiptNotice { get; set; }
}

public sealed class InboundLine
{
    public int Id { get; set; }
    public int InboundOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public Product? Product { get; set; }
    public InboundOrder? InboundOrder { get; set; }
}

public sealed class OutboundOrder
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerCode { get; set; } = string.Empty;
    public string SourceDocumentNo { get; set; } = string.Empty;
    public int? SourceDeliveryNoticeId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public DateTimeOffset RequiredAt { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public List<OutboundLine> Lines { get; set; } = [];
    public DeliveryNotice? SourceDeliveryNotice { get; set; }
}

public sealed class OutboundLine
{
    public int Id { get; set; }
    public int OutboundOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal PickedQuantity { get; set; }
    public decimal ShippedQuantity { get; set; }
    public Product? Product { get; set; }
    public OutboundOrder? OutboundOrder { get; set; }
    public List<OutboundAllocation> Allocations { get; set; } = [];
}

public sealed class OutboundAllocation
{
    public long Id { get; set; }
    public int OutboundLineId { get; set; }
    public int InventoryBalanceId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ShippedQuantity { get; set; }
    public OutboundLine? OutboundLine { get; set; }
    public InventoryBalance? InventoryBalance { get; set; }
}
