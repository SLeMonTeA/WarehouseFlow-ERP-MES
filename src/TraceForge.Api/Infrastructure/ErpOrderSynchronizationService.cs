using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Contracts;
using TraceForge.Api.Domain;

namespace TraceForge.Api.Infrastructure;

public sealed record ErpOrderSyncResult(
    int Fetched,
    int Created,
    int Updated,
    int EventsApplied,
    int EventsFailed,
    int Conflicts,
    int Archived,
    IReadOnlyList<ErpProductionOrderSnapshot> Orders);

public sealed class ErpOrderSynchronizationService(WarehouseFlowClient erp, TraceForgeDbContext db)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<ErpOrderSyncResult> SyncAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var snapshots = await erp.GetProductionOrdersAsync(cancellationToken);
            var orderNos = snapshots.Select(x => x.OrderNo).ToList();
            var existing = orderNos.Count == 0
                ? new Dictionary<string, WorkOrder>()
                : await db.WorkOrders.Include(x => x.Operations).Where(x => orderNos.Contains(x.OrderNo)).ToDictionaryAsync(x => x.OrderNo, cancellationToken);
            var created = 0;
            foreach (var snapshot in snapshots) UpsertSnapshot(snapshot, existing, ref created);
            var archived = 0;
            if (snapshots.Count > 0)
            {
                var activeErpOrders = await db.WorkOrders
                    .Where(x => x.ArchivedAt == null && x.BatchNo.StartsWith("ERP-"))
                    .ToListAsync(cancellationToken);
                foreach (var stale in activeErpOrders.Where(x => !orderNos.Contains(x.OrderNo, StringComparer.OrdinalIgnoreCase)))
                {
                    stale.ArchivedAt = DateTimeOffset.UtcNow;
                    stale.Status = WorkOrderStatus.Cancelled;
                    db.TraceEvents.Add(new TraceEvent
                    {
                        BatchNo = stale.BatchNo,
                        EventType = "WorkOrderArchived",
                        EventData = $"ERP 工单已不再返回，MES 自动归档：{stale.OrderNo}"
                    });
                    archived++;
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            var eventResult = await SyncOrderEventsAsync(cancellationToken);
            return new(snapshots.Count, created, snapshots.Count - created, eventResult.Applied, eventResult.Failed, eventResult.Conflicts, archived, snapshots);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<WorkOrder> RefreshOrderAsync(string orderNo, CancellationToken cancellationToken)
    {
        var snapshot = await erp.GetProductionOrderAsync(orderNo, cancellationToken);
        var existing = await db.WorkOrders
            .Include(x => x.Operations)
            .Where(x => x.OrderNo == orderNo)
            .ToDictionaryAsync(x => x.OrderNo, cancellationToken);
        var created = 0;
        var workOrder = UpsertSnapshot(snapshot, existing, ref created);
        await db.SaveChangesAsync(cancellationToken);
        return workOrder;
    }

    private WorkOrder UpsertSnapshot(ErpProductionOrderSnapshot snapshot, Dictionary<string, WorkOrder> existing, ref int created)
    {
        if (!existing.TryGetValue(snapshot.OrderNo, out var workOrder))
        {
            workOrder = new WorkOrder { OrderNo = snapshot.OrderNo, BatchNo = $"ERP-{snapshot.OrderNo}" };
            db.WorkOrders.Add(workOrder);
            existing[snapshot.OrderNo] = workOrder;
            created++;
        }

        workOrder.ProductCode = snapshot.ProductSku;
        workOrder.ArchivedAt = null;
        workOrder.RoutingCode = snapshot.RoutingCode;
        workOrder.RoutingVersion = snapshot.RoutingVersion;
        workOrder.PlannedQuantity = snapshot.PlannedQuantity;
        workOrder.ReportedGoodQuantity = snapshot.ReportedGoodQuantity;
        workOrder.ReportedDefectQuantity = snapshot.ReportedDefectQuantity;
        workOrder.ErpCompletedQuantity = snapshot.CompletedQuantity;
        workOrder.CompletedQuantity = snapshot.ReportedGoodQuantity > 0 ? snapshot.ReportedGoodQuantity : snapshot.CompletedQuantity;
        workOrder.PlannedStart = snapshot.RequiredAt;
        workOrder.Status = MapStatus(snapshot.Status);
        workOrder.ErpStarted = snapshot.Status is "InProgress" or "PartiallyCompleted" or "Completed" or "Closed";
        workOrder.ErpMaterialIssued = snapshot.Materials.Count == 0 || snapshot.Materials.All(x => x.IssuedQuantity >= x.RequiredQuantity);
        workOrder.ErpQualityStatus = snapshot.QualityStatus;
        if (workOrder.Operations.Count == 0 && snapshot.Operations.Count > 0)
            workOrder.Operations.AddRange(snapshot.Operations.OrderBy(x => x.Sequence).Select(x => new WorkOrderOperation
            {
                Sequence = x.Sequence, OperationCode = x.OperationCode, OperationName = x.OperationName, WorkCenter = x.WorkCenter,
                StandardMinutes = x.StandardMinutes, RequiresQuality = x.RequiresQuality,
                ReportedGoodQuantity = x.ReportedGoodQuantity, ReportedDefectQuantity = x.ReportedDefectQuantity, Status = x.Status
            }));
        if (workOrder.Status == WorkOrderStatus.Completed && workOrder.CompletedAt is null)
            workOrder.CompletedAt = DateTimeOffset.UtcNow;
        return workOrder;
    }

    private static WorkOrderStatus MapStatus(string status) => status switch
    {
        "Draft" => WorkOrderStatus.Draft,
        "InProgress" => WorkOrderStatus.InProgress,
        "PartiallyCompleted" => WorkOrderStatus.InProgress,
        "Completed" => WorkOrderStatus.Completed,
        "Closed" => WorkOrderStatus.Closed,
        "Cancelled" => WorkOrderStatus.Cancelled,
        _ => WorkOrderStatus.Released
    };

    private async Task<(int Applied, int Failed, int Conflicts)> SyncOrderEventsAsync(CancellationToken cancellationToken)
    {
        const string streamName = "ERP-ProductionOrder";
        var cursor = await db.ErpSyncCursors.SingleOrDefaultAsync(x => x.StreamName == streamName, cancellationToken);
        if (cursor is null)
        {
            cursor = new ErpSyncCursor { StreamName = streamName };
            db.ErpSyncCursors.Add(cursor);
            await db.SaveChangesAsync(cancellationToken);
        }

        var events = await erp.GetProductionOrderEventsAsync(cursor.LastEventId, 100, cancellationToken);
        var applied = 0;
        var failed = 0;
        var conflicts = 0;
        foreach (var integrationEvent in events)
        {
            var log = await db.ErpSyncLogs.SingleOrDefaultAsync(x => x.EventKey == integrationEvent.EventKey, cancellationToken);
            if (log?.Status is ErpSyncLogStatus.Succeeded or ErpSyncLogStatus.Conflict)
            {
                cursor.LastEventId = integrationEvent.EventId;
                cursor.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            try
            {
                if (integrationEvent.Snapshot is null)
                    throw new InvalidOperationException($"ERP 事件 {integrationEvent.EventKey} 未返回生产订单快照");
                var existing = await db.WorkOrders.Include(x => x.Operations).Where(x => x.OrderNo == integrationEvent.AggregateNo).ToDictionaryAsync(x => x.OrderNo, cancellationToken);
                var current = existing.GetValueOrDefault(integrationEvent.AggregateNo);
                var conflictMessage = current is not null &&
                    (current.ProductCode != integrationEvent.Snapshot.ProductSku ||
                     current.PlannedQuantity != integrationEvent.Snapshot.PlannedQuantity ||
                     current.Status != MapStatus(integrationEvent.Snapshot.Status))
                    ? $"MES 本地状态已被 ERP 校正：产品 {current.ProductCode}->{integrationEvent.Snapshot.ProductSku}，数量 {current.PlannedQuantity}->{integrationEvent.Snapshot.PlannedQuantity}，状态 {current.Status}->{integrationEvent.Snapshot.Status}"
                    : null;
                var ignored = 0;
                UpsertSnapshot(integrationEvent.Snapshot, existing, ref ignored);
                log ??= new ErpSyncLog { EventKey = integrationEvent.EventKey, EventType = integrationEvent.EventType, OrderNo = integrationEvent.AggregateNo, OccurredAt = integrationEvent.OccurredAt };
                if (log.Id == 0) db.ErpSyncLogs.Add(log);
                log.Status = conflictMessage is null ? ErpSyncLogStatus.Succeeded : ErpSyncLogStatus.Conflict;
                log.AttemptCount++;
                log.LastError = conflictMessage;
                log.ProcessedAt = DateTimeOffset.UtcNow;
                cursor.LastEventId = integrationEvent.EventId;
                cursor.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                applied++;
                if (conflictMessage is not null) conflicts++;
            }
            catch (Exception ex)
            {
                log ??= new ErpSyncLog { EventKey = integrationEvent.EventKey, EventType = integrationEvent.EventType, OrderNo = integrationEvent.AggregateNo, OccurredAt = integrationEvent.OccurredAt };
                if (log.Id == 0) db.ErpSyncLogs.Add(log);
                log.Status = ErpSyncLogStatus.Failed;
                log.AttemptCount++;
                log.LastError = ex.Message;
                await db.SaveChangesAsync(cancellationToken);
                failed++;
                break;
            }
        }

        return (applied, failed, conflicts);
    }
}

public sealed class ErpOrderSyncHostedService(IServiceScopeFactory scopeFactory, ILogger<ErpOrderSyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var synchronizer = scope.ServiceProvider.GetRequiredService<ErpOrderSynchronizationService>();
                var result = await synchronizer.SyncAsync(stoppingToken);
                if (result.Fetched > 0 || result.Archived > 0 || result.EventsApplied > 0 || result.EventsFailed > 0)
                    logger.LogInformation("ERP 生产订单自动同步完成：订单 {Fetched}，归档 {Archived}，事件处理 {Applied}，冲突 {Conflicts}，失败 {Failed}", result.Fetched, result.Archived, result.EventsApplied, result.Conflicts, result.EventsFailed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "ERP 生产订单自动同步失败，将在下一轮重试"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
