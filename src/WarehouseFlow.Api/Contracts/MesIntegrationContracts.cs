using WarehouseFlow.Api.Domain;

namespace WarehouseFlow.Api.Contracts;

public sealed record MesProductionOrderMaterial(
    string ComponentSku,
    string ComponentName,
    decimal RequiredQuantity,
    decimal IssuedQuantity,
    decimal RemainingQuantity);

public sealed record MesProductionOrderOperation(
    int Sequence,
    string OperationCode,
    string OperationName,
    string WorkCenter,
    decimal StandardMinutes,
    bool RequiresQuality,
    decimal ReportedGoodQuantity,
    decimal ReportedDefectQuantity,
    string Status);

public sealed record MesProductionOrderSnapshot(
    string OrderNo,
    string ProductSku,
    string ProductName,
    string Status,
    decimal PlannedQuantity,
    decimal CompletedQuantity,
    decimal ReportedGoodQuantity,
    decimal ReportedDefectQuantity,
    string QualityStatus,
    string QualityRemark,
    DateTimeOffset RequiredAt,
    IReadOnlyList<MesProductionOrderMaterial> Materials,
    string RoutingCode,
    string RoutingVersion,
    IReadOnlyList<MesProductionOrderOperation> Operations);

public sealed record MesProductionOrderEvent(
    long EventId,
    string EventKey,
    string EventType,
    string AggregateNo,
    DateTimeOffset OccurredAt,
    MesProductionOrderSnapshot? Snapshot);

public sealed record MesProductionProgressRequest(
    string EventKey,
    string OperationCode,
    string Workstation,
    decimal GoodQuantity,
    decimal DefectQuantity,
    string Operator,
    DateTimeOffset ReportedAt);

public sealed record MesProductionQualityRequest(
    string EventKey,
    string Checkpoint,
    QualityInspectionResult Result,
    string? Remark,
    string Inspector,
    DateTimeOffset InspectedAt);
