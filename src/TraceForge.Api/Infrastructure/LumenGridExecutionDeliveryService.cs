using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Contracts;
using TraceForge.Api.Domain;

namespace TraceForge.Api.Infrastructure;

/// <summary>
/// MES 到 LumenGrid 的最小 Outbox。业务状态先落库，外部设备平台随后投递，避免跨系统调用失败回滚 MES。
/// </summary>
public sealed class LumenGridExecutionDeliveryService(
    TraceForgeDbContext db,
    LumenGridClient lumenGrid,
    ILogger<LumenGridExecutionDeliveryService> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public LumenGridExecutionDelivery Enqueue(
        WorkOrder workOrder,
        WorkOrderOperation operation,
        string eventKey,
        string eventType,
        string deviceCode,
        string bindingStatus,
        DateTimeOffset occurredAt)
    {
        var delivery = new LumenGridExecutionDelivery
        {
            EventKey = eventKey.Trim(),
            EventType = eventType,
            DeviceCode = deviceCode.Trim(),
            WorkOrderNo = workOrder.OrderNo,
            OperationCode = operation.OperationCode,
            OperationName = operation.OperationName,
            WorkCenter = operation.WorkCenter,
            BindingStatus = bindingStatus,
            StartedAt = eventType == "OperationStarted" ? occurredAt : null,
            OccurredAt = occurredAt,
            ReportedGoodQuantity = operation.ReportedGoodQuantity,
            ReportedDefectQuantity = operation.ReportedDefectQuantity,
            NextAttemptAt = DateTimeOffset.UtcNow
        };
        db.LumenGridExecutionDeliveries.Add(delivery);
        return delivery;
    }

    public async Task<int> DeliverDueAsync(int limit, CancellationToken cancellationToken)
    {
        if (!await Gate.WaitAsync(0, cancellationToken)) return 0;
        try
        {
            return await DeliverDueCoreAsync(limit, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<int> RetryAsync(string? eventKey, int limit, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var query = db.LumenGridExecutionDeliveries.Where(x => x.Status != LumenGridDeliveryStatus.Sent);
            if (!string.IsNullOrWhiteSpace(eventKey)) query = query.Where(x => x.EventKey == eventKey.Trim());
            var deliveries = await query.OrderBy(x => x.Id).Take(Math.Clamp(limit, 1, 100)).ToListAsync(cancellationToken);
            foreach (var delivery in deliveries) delivery.NextAttemptAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return await DeliverDueCoreAsync(deliveries.Count == 0 ? limit : deliveries.Count, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<int> DeliverDueCoreAsync(int limit, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var deliveries = await db.LumenGridExecutionDeliveries
            .Where(x => x.Status != LumenGridDeliveryStatus.Sent && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
        var delivered = 0;
        foreach (var delivery in deliveries)
        {
            if (await TryDeliverAsync(delivery, cancellationToken)) delivered++;
            await db.SaveChangesAsync(cancellationToken);
        }
        return delivered;
    }

    private async Task<bool> TryDeliverAsync(LumenGridExecutionDelivery delivery, CancellationToken cancellationToken)
    {
        delivery.AttemptCount++;
        try
        {
            var result = await lumenGrid.PublishExecutionBindingAsync(new LumenGridExecutionBindingRequest(
                delivery.EventKey,
                delivery.EventType,
                delivery.DeviceCode,
                delivery.WorkOrderNo,
                delivery.OperationCode,
                delivery.OperationName,
                delivery.WorkCenter,
                delivery.BindingStatus,
                delivery.StartedAt,
                delivery.OccurredAt,
                delivery.ReportedGoodQuantity,
                delivery.ReportedDefectQuantity), cancellationToken);
            if (result is null) throw new InvalidOperationException("LumenGrid 未返回执行绑定结果");
            delivery.Status = LumenGridDeliveryStatus.Sent;
            delivery.LastError = null;
            delivery.SentAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (Exception ex) when (ex is LumenGridIntegrationException or HttpRequestException or TaskCanceledException)
        {
            delivery.Status = LumenGridDeliveryStatus.Failed;
            delivery.LastError = ex.Message;
            delivery.NextAttemptAt = DateTimeOffset.UtcNow + CalculateBackoff(delivery.AttemptCount);
            logger.LogWarning(ex, "LumenGrid 执行绑定投递失败：{EventKey}，第 {AttemptCount} 次，下一次重试 {NextAttemptAt}", delivery.EventKey, delivery.AttemptCount, delivery.NextAttemptAt);
            return false;
        }
    }

    private static TimeSpan CalculateBackoff(int attemptCount) =>
        TimeSpan.FromSeconds(Math.Min(900, 10 * Math.Pow(2, Math.Min(attemptCount - 1, 6))));
}

public sealed class LumenGridExecutionDeliveryHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<LumenGridExecutionDeliveryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<LumenGridExecutionDeliveryService>();
                var delivered = await service.DeliverDueAsync(50, stoppingToken);
                if (delivered > 0) logger.LogInformation("LumenGrid 执行绑定 Outbox 已投递 {Delivered} 条", delivered);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "LumenGrid 执行绑定 Outbox 处理失败，将在下一轮重试"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
