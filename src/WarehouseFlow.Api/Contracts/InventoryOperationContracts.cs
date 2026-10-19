using WarehouseFlow.Api.Domain;

namespace WarehouseFlow.Api.Contracts;

public sealed record InventoryTransferLineRequest(string Sku, decimal Quantity, string BatchNo = "UNBATCHED", DateTimeOffset? ExpiryDate = null);
public sealed record CreateInventoryTransferRequest(string TransferNo, string FromLocationCode, string ToLocationCode, string Operator, IReadOnlyList<InventoryTransferLineRequest> Lines);
public sealed record StocktakeLineRequest(string Sku, decimal CountedQuantity, string BatchNo = "UNBATCHED");
public sealed record CreateStocktakeRequest(string CheckNo, string LocationCode, string Operator, IReadOnlyList<StocktakeLineRequest> Lines);
public sealed record InventoryReturnLineRequest(string Sku, decimal Quantity, bool FromQualityHold = false, string BatchNo = "UNBATCHED", DateTimeOffset? ExpiryDate = null);
public sealed record CreateInventoryReturnRequest(string? ReturnNo, InventoryReturnType ReturnType, string SourceDocumentNo, string PartnerName, string LocationCode, string Operator, IReadOnlyList<InventoryReturnLineRequest> Lines);
