using TraceForge.Api.Domain;

namespace TraceForge.Api.Contracts;

public sealed record CreateWorkOrderOperationRequest(int Sequence, string OperationCode, string OperationName, string WorkCenter, decimal StandardMinutes, bool RequiresQuality = false);
public sealed record CreateWorkOrderRequest(
    string OrderNo,
    string ProductCode,
    string BatchNo,
    decimal PlannedQuantity,
    DateTimeOffset PlannedStart,
    IReadOnlyList<CreateWorkOrderOperationRequest>? Operations = null);
public sealed record ReportOperationRequest(string OperationCode, string Workstation, decimal GoodQuantity, decimal DefectQuantity, string Operator, string? DeviceCode = null);
public sealed record OperationExecutionRequest(string EventKey, string DeviceCode, string? Operator = null);
public sealed record CreateInspectionRequest(string Checkpoint, InspectionResult Result, string? Remark, string Inspector, string? OperationCode = null);
