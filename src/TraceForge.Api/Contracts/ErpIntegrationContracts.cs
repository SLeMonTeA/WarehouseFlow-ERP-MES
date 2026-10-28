using System.Text.Json.Serialization;

namespace TraceForge.Api.Contracts;

public sealed record ErpProductionOrderMaterial(
    [property: JsonPropertyName("componentSku")] string ComponentSku,
    [property: JsonPropertyName("componentName")] string ComponentName,
    [property: JsonPropertyName("requiredQuantity")] decimal RequiredQuantity,
    [property: JsonPropertyName("issuedQuantity")] decimal IssuedQuantity,
    [property: JsonPropertyName("openQuantity")] decimal OpenQuantity);

public sealed record ErpProductionOrderOperation(
    [property: JsonPropertyName("sequence")] int Sequence,
    [property: JsonPropertyName("operationCode")] string OperationCode,
    [property: JsonPropertyName("operationName")] string OperationName,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("standardMinutes")] decimal StandardMinutes,
    [property: JsonPropertyName("requiresQuality")] bool RequiresQuality,
    [property: JsonPropertyName("reportedGoodQuantity")] decimal ReportedGoodQuantity,
    [property: JsonPropertyName("reportedDefectQuantity")] decimal ReportedDefectQuantity,
    [property: JsonPropertyName("status")] string Status);

public sealed record ErpProductionOrderSnapshot(
    [property: JsonPropertyName("orderNo")] string OrderNo,
    [property: JsonPropertyName("productSku")] string ProductSku,
    [property: JsonPropertyName("productName")] string ProductName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("plannedQuantity")] decimal PlannedQuantity,
    [property: JsonPropertyName("completedQuantity")] decimal CompletedQuantity,
    [property: JsonPropertyName("reportedGoodQuantity")] decimal ReportedGoodQuantity,
    [property: JsonPropertyName("reportedDefectQuantity")] decimal ReportedDefectQuantity,
    [property: JsonPropertyName("qualityStatus")] string QualityStatus,
    [property: JsonPropertyName("qualityRemark")] string QualityRemark,
    [property: JsonPropertyName("requiredAt")] DateTimeOffset RequiredAt,
    [property: JsonPropertyName("materials")] IReadOnlyList<ErpProductionOrderMaterial> Materials,
    [property: JsonPropertyName("routingCode")] string RoutingCode,
    [property: JsonPropertyName("routingVersion")] string RoutingVersion,
    [property: JsonPropertyName("operations")] IReadOnlyList<ErpProductionOrderOperation> Operations);

public sealed record ErpProductionOrderEvent(
    [property: JsonPropertyName("eventId")] long EventId,
    [property: JsonPropertyName("eventKey")] string EventKey,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("aggregateNo")] string AggregateNo,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("snapshot")] ErpProductionOrderSnapshot? Snapshot);

public sealed record ErpDocumentActionRequest(string Operator, string Remark = "");
public sealed record ErpIssueMaterialsRequest(string Operator);
public sealed record ErpProductionLocation(string LocationCode, string Zone, int Capacity, string WarehouseCode, string WarehouseName);
public sealed record ErpProductionReceiptRequest(string ReceiptNo, string LocationCode, decimal Quantity, string BatchNo = "UNBATCHED", DateTimeOffset? ExpiryDate = null);
public sealed record ErpProductionProgressRequest(string EventKey, string OperationCode, string Workstation, decimal GoodQuantity, decimal DefectQuantity, string Operator, DateTimeOffset ReportedAt);
public sealed record ErpProductionQualityRequest(string EventKey, string Checkpoint, string Result, string? Remark, string Inspector, DateTimeOffset InspectedAt);
