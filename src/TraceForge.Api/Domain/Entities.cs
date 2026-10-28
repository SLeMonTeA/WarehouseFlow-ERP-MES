namespace TraceForge.Api.Domain;

public enum WorkOrderStatus { Draft, Released, InProgress, Completed, Closed, Cancelled }
public enum InspectionResult { Pending, Passed, Failed, Rework }
public enum ErpSyncLogStatus { Pending, Succeeded, Conflict, Failed }
public enum ErpReportDeliveryStatus { Pending, Sent, Failed }
public enum LumenGridDeliveryStatus { Pending, Sent, Failed }
public enum EquipmentPauseSuggestionStatus { Pending, Accepted, Rejected, Superseded }

public sealed class WorkOrder
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public string RoutingCode { get; set; } = string.Empty;
    public string RoutingVersion { get; set; } = string.Empty;
    public decimal PlannedQuantity { get; set; }
    public decimal CompletedQuantity { get; set; }
    public decimal ReportedGoodQuantity { get; set; }
    public decimal ReportedDefectQuantity { get; set; }
    public decimal ErpCompletedQuantity { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Draft;
    public bool ErpStarted { get; set; }
    public bool ErpMaterialIssued { get; set; }
    public string ErpQualityStatus { get; set; } = "Pending";
    public DateTimeOffset PlannedStart { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public List<OperationReport> Reports { get; set; } = [];
    public List<WorkOrderOperation> Operations { get; set; } = [];
    public List<QualityInspection> Inspections { get; set; } = [];
}

public sealed class WorkOrderOperation
{
    public long Id { get; set; }
    public int WorkOrderId { get; set; }
    public int Sequence { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public decimal StandardMinutes { get; set; }
    public bool RequiresQuality { get; set; }
    public decimal ReportedGoodQuantity { get; set; }
    public decimal ReportedDefectQuantity { get; set; }
    public string Status { get; set; } = "Pending";
    public WorkOrder? WorkOrder { get; set; }
}

public sealed class OperationReport
{
    public long Id { get; set; }
    public int WorkOrderId { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string Workstation { get; set; } = string.Empty;
    public decimal GoodQuantity { get; set; }
    public decimal DefectQuantity { get; set; }
    public string Operator { get; set; } = string.Empty;
    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public WorkOrder? WorkOrder { get; set; }
}

public sealed class QualityInspection
{
    public long Id { get; set; }
    public int WorkOrderId { get; set; }
    public string OperationCode { get; set; } = string.Empty;
    public string Checkpoint { get; set; } = string.Empty;
    public InspectionResult Result { get; set; } = InspectionResult.Pending;
    public string? Remark { get; set; }
    public string Inspector { get; set; } = string.Empty;
    public DateTimeOffset InspectedAt { get; set; } = DateTimeOffset.UtcNow;
    public WorkOrder? WorkOrder { get; set; }
}

public sealed class TraceEvent
{
    public long Id { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ErpSyncCursor
{
    public int Id { get; set; }
    public string StreamName { get; set; } = string.Empty;
    public long LastEventId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ErpSyncLog
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public ErpSyncLogStatus Status { get; set; } = ErpSyncLogStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ErpReportDelivery
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public int WorkOrderId { get; set; }
    public long OperationReportId { get; set; }
    public ErpReportDeliveryStatus Status { get; set; } = ErpReportDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public OperationReport? OperationReport { get; set; }
}

public sealed class ErpQualityDelivery
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public int WorkOrderId { get; set; }
    public long QualityInspectionId { get; set; }
    public ErpReportDeliveryStatus Status { get; set; } = ErpReportDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public QualityInspection? QualityInspection { get; set; }
}

public sealed class LumenGridExecutionDelivery
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public string WorkOrderNo { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string OperationName { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public string BindingStatus { get; set; } = "Bound";
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal ReportedGoodQuantity { get; set; }
    public decimal ReportedDefectQuantity { get; set; }
    public LumenGridDeliveryStatus Status { get; set; } = LumenGridDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}

public sealed class LumenGridEquipmentNotification
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public string EquipmentStatus { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public bool IsAvailable { get; set; }
    public bool CanReceiveProduction { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// 设备不可用时生成的人工确认建议。建议本身不改变工序状态，只有操作员接受后才会产生暂停投递。
/// </summary>
public sealed class EquipmentPauseSuggestion
{
    public long Id { get; set; }
    public string SuggestionKey { get; set; } = string.Empty;
    public string EventKey { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public string WorkOrderNo { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public EquipmentPauseSuggestionStatus Status { get; set; } = EquipmentPauseSuggestionStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecisionRemark { get; set; }
}
