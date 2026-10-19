namespace WarehouseFlow.Api.Domain;

public enum InventoryOperationStatus { Draft, Applied, Cancelled }
public enum InventoryReturnType { PurchaseReturn, SalesReturn }

public sealed class InventoryTransfer
{
    public int Id { get; set; }
    public string TransferNo { get; set; } = string.Empty;
    public string FromLocationCode { get; set; } = string.Empty;
    public string ToLocationCode { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public InventoryOperationStatus Status { get; set; } = InventoryOperationStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
    public List<InventoryTransferLine> Lines { get; set; } = [];
}

public sealed class InventoryTransferLine
{
    public int Id { get; set; }
    public int InventoryTransferId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public string BatchNo { get; set; } = "UNBATCHED";
    public DateTimeOffset? ExpiryDate { get; set; }
    public InventoryTransfer? InventoryTransfer { get; set; }
    public Product? Product { get; set; }
}

public sealed class StocktakeOrder
{
    public int Id { get; set; }
    public string CheckNo { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public InventoryOperationStatus Status { get; set; } = InventoryOperationStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
    public List<StocktakeLine> Lines { get; set; } = [];
}

public sealed class StocktakeLine
{
    public int Id { get; set; }
    public int StocktakeOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal BookQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public string BatchNo { get; set; } = "UNBATCHED";
    public DateTimeOffset? ExpiryDate { get; set; }
    public StocktakeOrder? StocktakeOrder { get; set; }
    public Product? Product { get; set; }
}

public sealed class InventoryReturnOrder
{
    public int Id { get; set; }
    public string ReturnNo { get; set; } = string.Empty;
    public InventoryReturnType ReturnType { get; set; }
    public string SourceDocumentNo { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public InventoryOperationStatus Status { get; set; } = InventoryOperationStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
    public List<InventoryReturnLine> Lines { get; set; } = [];
}

public sealed class InventoryReturnLine
{
    public int Id { get; set; }
    public int InventoryReturnOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public bool FromQualityHold { get; set; }
    public string BatchNo { get; set; } = "UNBATCHED";
    public DateTimeOffset? ExpiryDate { get; set; }
    public InventoryReturnOrder? InventoryReturnOrder { get; set; }
    public Product? Product { get; set; }
}
