namespace WarehouseFlow.Api.Contracts;

public sealed record CreateInboundRequest(string? OrderNo, string SupplierName, DateTimeOffset ExpectedAt, IReadOnlyList<OrderLineRequest> Lines, string? SupplierCode = null);
public sealed record CreateOutboundRequest(string? OrderNo, string CustomerName, DateTimeOffset RequiredAt, IReadOnlyList<OrderLineRequest> Lines, string? CustomerCode = null);
public sealed record UpdateInboundRequest(string SupplierName, DateTimeOffset ExpectedAt, IReadOnlyList<OrderLineRequest> Lines, string? SupplierCode = null);
public sealed record UpdateOutboundRequest(string CustomerName, DateTimeOffset RequiredAt, IReadOnlyList<OrderLineRequest> Lines, string? CustomerCode = null);
public sealed record OrderLineRequest(string Sku, decimal Quantity);
public sealed record WarehouseOperationLineRequest(string Sku, decimal Quantity, string BatchNo = "UNBATCHED", DateTimeOffset? ExpiryDate = null);
public sealed record ReceiveInboundRequest(string LocationCode, string Operator, IReadOnlyList<WarehouseOperationLineRequest>? Lines = null, string BatchNo = "UNBATCHED", DateTimeOffset? ExpiryDate = null);
public sealed record ReserveOutboundRequest(string Operator, IReadOnlyList<WarehouseOperationLineRequest>? Lines = null);
public sealed record ShipOutboundRequest(string Operator, IReadOnlyList<WarehouseOperationLineRequest>? Lines = null);
