using System.Text.Json.Serialization;

namespace TraceForge.Api.Contracts;

public sealed record LumenGridEquipmentAvailabilityResponse(
    [property: JsonPropertyName("generatedAt")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("items")] IReadOnlyList<LumenGridEquipmentAvailabilityItem> Items,
    [property: JsonPropertyName("totalRatedCapacityPerHour")] decimal TotalRatedCapacityPerHour,
    [property: JsonPropertyName("availableCapacityPerHour")] decimal AvailableCapacityPerHour);

public sealed record LumenGridEquipmentAvailabilityItem(
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("isOnline")] bool IsOnline,
    [property: JsonPropertyName("isAvailable")] bool IsAvailable,
    [property: JsonPropertyName("canReceiveProduction")] bool CanReceiveProduction,
    [property: JsonPropertyName("ratedCapacityPerHour")] decimal RatedCapacityPerHour,
    [property: JsonPropertyName("currentCapacityPerHour")] decimal CurrentCapacityPerHour,
    [property: JsonPropertyName("targetCapacityPerHour")] decimal TargetCapacityPerHour,
    [property: JsonPropertyName("capacityTargetSource")] string CapacityTargetSource,
    [property: JsonPropertyName("matchedProductionOrderNos")] IReadOnlyList<string> MatchedProductionOrderNos,
    [property: JsonPropertyName("activeWorkOrderNo")] string ActiveWorkOrderNo,
    [property: JsonPropertyName("activeOperationCode")] string ActiveOperationCode,
    [property: JsonPropertyName("activeOperationStatus")] string ActiveOperationStatus,
    [property: JsonPropertyName("activeAlertCount")] int ActiveAlertCount,
    [property: JsonPropertyName("activeAlertCodes")] IReadOnlyList<string> ActiveAlertCodes,
    [property: JsonPropertyName("lastHeartbeat")] DateTimeOffset? LastHeartbeat,
    [property: JsonPropertyName("snapshotAt")] DateTimeOffset SnapshotAt,
    [property: JsonPropertyName("estimatedRecoveryAt")] DateTimeOffset? EstimatedRecoveryAt);

public sealed record LumenGridEquipmentEventBatchResponse(
    [property: JsonPropertyName("afterId")] long AfterId,
    [property: JsonPropertyName("nextAfterId")] long? NextAfterId,
    [property: JsonPropertyName("hasMore")] bool HasMore,
    [property: JsonPropertyName("items")] IReadOnlyList<LumenGridEquipmentEvent> Items);

public sealed record LumenGridEquipmentEvent(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("productionOrderNo")] string ProductionOrderNo,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("payload")] string Payload);

public sealed record LumenGridExecutionBindingRequest(
    [property: JsonPropertyName("eventKey")] string EventKey,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("workOrderNo")] string WorkOrderNo,
    [property: JsonPropertyName("operationCode")] string OperationCode,
    [property: JsonPropertyName("operationName")] string OperationName,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("startedAt")] DateTimeOffset? StartedAt,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset? OccurredAt,
    [property: JsonPropertyName("reportedGoodQuantity")] decimal? ReportedGoodQuantity,
    [property: JsonPropertyName("reportedDefectQuantity")] decimal? ReportedDefectQuantity);

public sealed record LumenGridExecutionBindingResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("eventKey")] string EventKey,
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("workOrderNo")] string WorkOrderNo,
    [property: JsonPropertyName("operationCode")] string OperationCode,
    [property: JsonPropertyName("operationName")] string OperationName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("startedAt")] DateTimeOffset? StartedAt,
    [property: JsonPropertyName("pausedAt")] DateTimeOffset? PausedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt,
    [property: JsonPropertyName("reportedGoodQuantity")] decimal ReportedGoodQuantity,
    [property: JsonPropertyName("reportedDefectQuantity")] decimal ReportedDefectQuantity,
    [property: JsonPropertyName("lastEventAt")] DateTimeOffset LastEventAt,
    [property: JsonPropertyName("isActive")] bool IsActive);

public sealed record LumenGridExecutionBindingWriteResponse(
    [property: JsonPropertyName("duplicate")] bool Duplicate,
    [property: JsonPropertyName("binding")] LumenGridExecutionBindingResponse Binding);

public sealed record LumenGridEquipmentNotificationRequest(
    [property: JsonPropertyName("eventKey")] string EventKey,
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("equipmentStatus")] string EquipmentStatus,
    [property: JsonPropertyName("isOnline")] bool IsOnline,
    [property: JsonPropertyName("isAvailable")] bool IsAvailable,
    [property: JsonPropertyName("canReceiveProduction")] bool CanReceiveProduction,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("payload")] string Payload);
