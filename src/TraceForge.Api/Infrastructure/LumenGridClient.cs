using System.Net.Http.Json;
using System.Text.Json;
using TraceForge.Api.Contracts;

namespace TraceForge.Api.Infrastructure;

public sealed class LumenGridClient(HttpClient http, ILogger<LumenGridClient> logger)
{
    public Task<LumenGridEquipmentAvailabilityResponse?> GetEquipmentAvailabilityAsync(
        string? workCenter,
        CancellationToken cancellationToken)
    {
        var query = string.IsNullOrWhiteSpace(workCenter)
            ? string.Empty
            : "?workCenter=" + Uri.EscapeDataString(workCenter);
        return GetAsync<LumenGridEquipmentAvailabilityResponse>("/api/integration/mes/equipment-availability" + query, cancellationToken);
    }

    public Task<LumenGridEquipmentEventBatchResponse?> GetEquipmentEventsAsync(
        long afterId,
        string? deviceCode,
        string? workCenter,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = $"?afterId={afterId}&limit={Math.Clamp(limit, 1, 500)}";
        if (!string.IsNullOrWhiteSpace(deviceCode)) query += "&deviceCode=" + Uri.EscapeDataString(deviceCode);
        if (!string.IsNullOrWhiteSpace(workCenter)) query += "&workCenter=" + Uri.EscapeDataString(workCenter);
        return GetAsync<LumenGridEquipmentEventBatchResponse>("/api/integration/mes/equipment-events" + query, cancellationToken);
    }

    public async Task<LumenGridExecutionBindingWriteResponse?> PublishExecutionBindingAsync(
        LumenGridExecutionBindingRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("/api/integration/mes/execution-bindings", request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<LumenGridExecutionBindingWriteResponse>(new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("LumenGrid API 调用失败 {Path} {StatusCode}: {Body}", "/api/integration/mes/execution-bindings", (int)response.StatusCode, body);
        throw new LumenGridIntegrationException((int)response.StatusCode, $"LumenGrid 接口调用失败（{(int)response.StatusCode}）：{body}");
    }

    public Task<IReadOnlyList<LumenGridExecutionBindingResponse>?> GetExecutionBindingsAsync(
        string? deviceCode,
        string? workOrderNo,
        string? workCenter,
        bool activeOnly,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = $"?activeOnly={activeOnly.ToString().ToLowerInvariant()}&limit={Math.Clamp(limit, 1, 500)}";
        if (!string.IsNullOrWhiteSpace(deviceCode)) query += "&deviceCode=" + Uri.EscapeDataString(deviceCode);
        if (!string.IsNullOrWhiteSpace(workOrderNo)) query += "&workOrderNo=" + Uri.EscapeDataString(workOrderNo);
        if (!string.IsNullOrWhiteSpace(workCenter)) query += "&workCenter=" + Uri.EscapeDataString(workCenter);
        return GetAsync<IReadOnlyList<LumenGridExecutionBindingResponse>>("/api/integration/mes/execution-bindings" + query, cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(path, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("LumenGrid API 调用失败 {Path} {StatusCode}: {Body}", path, (int)response.StatusCode, body);
        throw new LumenGridIntegrationException((int)response.StatusCode, $"LumenGrid 接口调用失败（{(int)response.StatusCode}）：{body}");
    }
}

public sealed class LumenGridIntegrationException(int statusCode, string message) : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;
}
