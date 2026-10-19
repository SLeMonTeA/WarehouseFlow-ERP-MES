using System.Text.Json.Serialization;

namespace TraceForge.Api.Contracts;

public sealed record EquipmentPauseDecisionRequest(
    [property: JsonPropertyName("operator")] string Operator,
    [property: JsonPropertyName("remark")] string? Remark);

public sealed record EquipmentPauseSuggestionResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("suggestionKey")] string SuggestionKey,
    [property: JsonPropertyName("eventKey")] string EventKey,
    [property: JsonPropertyName("deviceCode")] string DeviceCode,
    [property: JsonPropertyName("workCenter")] string WorkCenter,
    [property: JsonPropertyName("workOrderNo")] string WorkOrderNo,
    [property: JsonPropertyName("operationCode")] string OperationCode,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("decidedAt")] DateTimeOffset? DecidedAt,
    [property: JsonPropertyName("decidedBy")] string? DecidedBy,
    [property: JsonPropertyName("decisionRemark")] string? DecisionRemark);
