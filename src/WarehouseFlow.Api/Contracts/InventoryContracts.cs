namespace WarehouseFlow.Api.Contracts;

public sealed record ReceiveStockRequest(string Sku, string LocationCode, decimal Quantity, string ReferenceNo, string BatchNo = "UNBATCHED", DateTimeOffset? ExpiryDate = null);
public sealed record ReserveStockRequest(string Sku, decimal Quantity, string OrderNo);
public sealed record AdjustStockRequest(string Sku, string LocationCode, decimal Quantity, string Reason, string BatchNo = "UNBATCHED");
public sealed record InventoryRow(string Sku, string ProductName, string Category, string LocationCode, string BatchNo, DateTimeOffset? ExpiryDate, decimal OnHand, decimal Reserved, decimal QualityHold, decimal Available, decimal SafetyStock);
public sealed record CreateProductRequest(string Sku, string Barcode, string Name, string Category, string Specification, string Unit, decimal SafetyStock, decimal MaxStock);
public sealed record UpdateProductRequest(string Barcode, string Name, string Category, string Specification, string Unit, decimal SafetyStock, decimal MaxStock, bool IsActive);
public sealed record CreateLocationRequest(string WarehouseCode, string Code, string Zone, int Capacity);
public sealed record UpdateLocationRequest(string Zone, int Capacity, bool IsActive);
public sealed record CreateWarehouseRequest(string Code, string Name, string Address);
public sealed record UpdateWarehouseRequest(string Name, string Address, bool IsActive);
public sealed record DashboardSummary(int ProductCount, int LocationCount, decimal TotalOnHand, decimal TotalAvailable, int LowStockCount, int PendingInboundCount, int PendingOutboundCount);
