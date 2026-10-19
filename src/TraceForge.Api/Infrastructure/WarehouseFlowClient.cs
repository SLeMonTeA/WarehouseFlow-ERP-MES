using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TraceForge.Api.Contracts;

namespace TraceForge.Api.Infrastructure;

/// <summary>
/// MES 到 ERP 的唯一调用入口。MES 不直接写 ERP 数据库，库存和 ERP 单据状态由 ERP 负责。
/// </summary>
public sealed class WarehouseFlowClient(HttpClient http, IConfiguration configuration, ILogger<WarehouseFlowClient> logger)
{
    private string? accessToken;
    private DateTimeOffset tokenExpiresAt;

    public async Task<IReadOnlyList<ErpProductionOrderSnapshot>> GetProductionOrdersAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/integration/mes/production-orders", null, cancellationToken);
        return await ReadAsync<List<ErpProductionOrderSnapshot>>(response, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<ErpProductionOrderEvent>> GetProductionOrderEventsAsync(long afterId, int limit, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/integration/mes/production-order-events?afterId={afterId}&limit={limit}", null, cancellationToken);
        return await ReadAsync<List<ErpProductionOrderEvent>>(response, cancellationToken) ?? [];
    }

    public async Task<ErpProductionOrderSnapshot> GetProductionOrderAsync(string orderNo, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"/api/integration/mes/production-orders/{Uri.EscapeDataString(orderNo)}", null, cancellationToken);
        return await ReadAsync<ErpProductionOrderSnapshot>(response, cancellationToken)
            ?? throw new InvalidOperationException("ERP 未返回生产订单");
    }

    public Task<JsonElement?> StartProductionAsync(string orderNo, ErpDocumentActionRequest request, CancellationToken cancellationToken) =>
        SendJsonAsync($"/api/integration/mes/production-orders/{Uri.EscapeDataString(orderNo)}/start", request, cancellationToken);

    public Task<JsonElement?> IssueMaterialsAsync(string orderNo, ErpIssueMaterialsRequest request, CancellationToken cancellationToken) =>
        SendJsonAsync($"/api/integration/mes/production-orders/{Uri.EscapeDataString(orderNo)}/issue-materials", request, cancellationToken);

    public Task<JsonElement?> ReceiveProductionAsync(string orderNo, ErpProductionReceiptRequest request, CancellationToken cancellationToken) =>
        SendJsonAsync($"/api/integration/mes/production-orders/{Uri.EscapeDataString(orderNo)}/receive", request, cancellationToken);

    public Task<JsonElement?> ReportProgressAsync(string orderNo, ErpProductionProgressRequest request, CancellationToken cancellationToken) =>
        SendJsonAsync($"/api/integration/mes/production-orders/{Uri.EscapeDataString(orderNo)}/progress", request, cancellationToken);

    public Task<JsonElement?> ReportQualityAsync(string orderNo, ErpProductionQualityRequest request, CancellationToken cancellationToken) =>
        SendJsonAsync($"/api/integration/mes/production-orders/{Uri.EscapeDataString(orderNo)}/quality", request, cancellationToken);

    private async Task<JsonElement?> SendJsonAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, path, JsonContent.Create(body), cancellationToken);
        return await ReadAsync<JsonElement>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return response;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("ERP API 调用失败 {Path} {StatusCode}: {Body}", path, (int)response.StatusCode, body);
        throw new WarehouseFlowIntegrationException((int)response.StatusCode, $"ERP 接口调用失败（{(int)response.StatusCode}）：{body}");
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(accessToken) && tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return accessToken;

        var userName = configuration["Erp:UserName"];
        var password = configuration["Erp:Password"];
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("MES 未配置 ERP 账号，请设置 Erp:UserName 和 Erp:Password");

        using var response = await http.PostAsJsonAsync("/api/auth/token", new { userName, password }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"MES 登录 ERP 失败（{(int)response.StatusCode}）：{body}");
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        accessToken = document.RootElement.GetProperty("accessToken").GetString();
        var expiresIn = document.RootElement.TryGetProperty("expiresIn", out var expiresElement) ? expiresElement.GetInt32() : 3600;
        tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn));
        return accessToken ?? throw new InvalidOperationException("ERP 登录未返回 accessToken");
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
    }
}

public sealed class WarehouseFlowIntegrationException(int statusCode, string message) : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;
}
