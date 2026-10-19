namespace WarehouseFlow.Api.Contracts;

public sealed record RoutingOperationRequest(int Sequence, string OperationCode, string OperationName, string WorkCenter, decimal StandardMinutes, bool RequiresQuality = false);
public sealed record CreateRoutingRequest(string ProductSku, string Code, string Version, DateTime EffectiveFrom, bool IsDefault, IReadOnlyList<RoutingOperationRequest> Operations);
public sealed record UpdateRoutingRequest(string Version, DateTime EffectiveFrom, bool IsDefault, bool IsActive, IReadOnlyList<RoutingOperationRequest> Operations);
