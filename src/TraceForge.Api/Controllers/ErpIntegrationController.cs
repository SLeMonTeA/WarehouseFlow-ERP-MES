using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Contracts;
using TraceForge.Api.Domain;
using TraceForge.Api.Infrastructure;

namespace TraceForge.Api.Controllers;

[ApiController]
[Route("api/integration/erp")]
public sealed class ErpIntegrationController(WarehouseFlowClient erp, TraceForgeDbContext db, ErpOrderSynchronizationService synchronizer) : ControllerBase
{
    [HttpGet("production-orders")]
    public async Task<IActionResult> ListProductionOrders(CancellationToken cancellationToken)
    {
        try { return Ok(await erp.GetProductionOrdersAsync(cancellationToken)); }
        catch (InvalidOperationException ex) { return ToProblem(ex); }
    }

    [HttpGet("production-orders/{orderNo}")]
    public async Task<IActionResult> GetProductionOrder(string orderNo, CancellationToken cancellationToken)
    {
        try { return Ok(await erp.GetProductionOrderAsync(orderNo, cancellationToken)); }
        catch (InvalidOperationException ex) { return ToProblem(ex); }
    }

    [HttpPost("production-orders/{orderNo}/start")]
    public async Task<IActionResult> StartProduction(string orderNo, ErpDocumentActionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await erp.StartProductionAsync(orderNo, request, cancellationToken);
            var workOrder = await synchronizer.RefreshOrderAsync(orderNo, cancellationToken);
            return Ok(new { result, local = new { workOrder.OrderNo, workOrder.Status, workOrder.ErpStarted, workOrder.ErpMaterialIssued, workOrder.ErpQualityStatus } });
        }
        catch (InvalidOperationException ex) { return ToProblem(ex); }
    }

    [HttpPost("production-orders/{orderNo}/issue-materials")]
    public async Task<IActionResult> IssueMaterials(string orderNo, ErpIssueMaterialsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await erp.IssueMaterialsAsync(orderNo, request, cancellationToken);
            var workOrder = await synchronizer.RefreshOrderAsync(orderNo, cancellationToken);
            return Ok(new { result, local = new { workOrder.OrderNo, workOrder.Status, workOrder.ErpStarted, workOrder.ErpMaterialIssued, workOrder.ErpQualityStatus } });
        }
        catch (InvalidOperationException ex) { return ToProblem(ex); }
    }

    [HttpPost("production-orders/{orderNo}/receive")]
    public async Task<IActionResult> ReceiveProduction(string orderNo, ErpProductionReceiptRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await erp.ReceiveProductionAsync(orderNo, request, cancellationToken);
            var workOrder = await synchronizer.RefreshOrderAsync(orderNo, cancellationToken);
            return Ok(new { result, local = new { workOrder.OrderNo, workOrder.Status, workOrder.ErpStarted, workOrder.ErpMaterialIssued, workOrder.ErpQualityStatus } });
        }
        catch (InvalidOperationException ex) { return ToProblem(ex); }
    }

    [HttpPost("production-orders/sync")]
    public async Task<IActionResult> SyncProductionOrders(CancellationToken cancellationToken)
    {
        try
        {
            var result = await synchronizer.SyncAsync(cancellationToken);
            return Ok(new { fetched = result.Fetched, created = result.Created, updated = result.Updated, archived = result.Archived, eventsApplied = result.EventsApplied, eventsFailed = result.EventsFailed, conflicts = result.Conflicts, orders = result.Orders });
        }
        catch (InvalidOperationException ex) { return ToProblem(ex); }
    }

    [HttpPost("report-deliveries/retry")]
    public async Task<IActionResult> RetryReportDeliveries(CancellationToken cancellationToken)
    {
        var deliveries = await db.ErpReportDeliveries
            .Include(x => x.WorkOrder)
            .Include(x => x.OperationReport)
            .Where(x => x.Status != ErpReportDeliveryStatus.Sent)
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var sent = 0;
        var failed = 0;
        foreach (var delivery in deliveries)
        {
            try
            {
                delivery.AttemptCount++;
                var report = delivery.OperationReport ?? throw new InvalidOperationException("报工记录不存在");
                var workOrder = delivery.WorkOrder ?? throw new InvalidOperationException("MES 工单不存在");
                await erp.ReportProgressAsync(workOrder.OrderNo, new ErpProductionProgressRequest(
                    delivery.EventKey, report.OperationCode, report.Workstation, report.GoodQuantity,
                    report.DefectQuantity, report.Operator, report.ReportedAt), cancellationToken);
                delivery.Status = ErpReportDeliveryStatus.Sent;
                delivery.SentAt = DateTimeOffset.UtcNow;
                delivery.LastError = null;
                sent++;
            }
            catch (Exception ex)
            {
                delivery.Status = ErpReportDeliveryStatus.Failed;
                delivery.LastError = ex.Message;
                failed++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { fetched = deliveries.Count, sent, failed });
    }

    [HttpPost("quality-deliveries/retry")]
    public async Task<IActionResult> RetryQualityDeliveries(CancellationToken cancellationToken)
    {
        var deliveries = await db.ErpQualityDeliveries
            .Include(x => x.WorkOrder)
            .Include(x => x.QualityInspection)
            .Where(x => x.Status != ErpReportDeliveryStatus.Sent)
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var sent = 0;
        var failed = 0;
        foreach (var delivery in deliveries)
        {
            try
            {
                delivery.AttemptCount++;
                var inspection = delivery.QualityInspection ?? throw new InvalidOperationException("质检记录不存在");
                var workOrder = delivery.WorkOrder ?? throw new InvalidOperationException("MES 工单不存在");
                await erp.ReportQualityAsync(workOrder.OrderNo, new ErpProductionQualityRequest(
                    delivery.EventKey, inspection.Checkpoint, inspection.Result.ToString(), inspection.Remark,
                    inspection.Inspector, inspection.InspectedAt), cancellationToken);
                delivery.Status = ErpReportDeliveryStatus.Sent;
                delivery.SentAt = DateTimeOffset.UtcNow;
                delivery.LastError = null;
                sent++;
            }
            catch (Exception ex)
            {
                delivery.Status = ErpReportDeliveryStatus.Failed;
                delivery.LastError = ex.Message;
                failed++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { fetched = deliveries.Count, sent, failed });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var localCount = await db.WorkOrders.CountAsync(cancellationToken);
        var failedEvents = await db.ErpSyncLogs.CountAsync(x => x.Status == ErpSyncLogStatus.Failed, cancellationToken);
        var conflictEvents = await db.ErpSyncLogs.CountAsync(x => x.Status == ErpSyncLogStatus.Conflict, cancellationToken);
        var pendingReports = await db.ErpReportDeliveries.CountAsync(x => x.Status != ErpReportDeliveryStatus.Sent, cancellationToken);
        var pendingQuality = await db.ErpQualityDeliveries.CountAsync(x => x.Status != ErpReportDeliveryStatus.Sent, cancellationToken);
        var cursor = await db.ErpSyncCursors.AsNoTracking().SingleOrDefaultAsync(x => x.StreamName == "ERP-ProductionOrder", cancellationToken);
        try
        {
            var orders = await erp.GetProductionOrdersAsync(cancellationToken);
            return Ok(new { connected = true, erpOrderCount = orders.Count, localWorkOrderCount = localCount, failedEvents, conflictEvents, pendingReports, pendingQuality, lastEventId = cursor?.LastEventId ?? 0, lastSyncAt = cursor?.UpdatedAt });
        }
        catch (InvalidOperationException ex)
        {
            return Ok(new { connected = false, erpOrderCount = 0, localWorkOrderCount = localCount, failedEvents, conflictEvents, pendingReports, pendingQuality, lastEventId = cursor?.LastEventId ?? 0, lastSyncAt = cursor?.UpdatedAt, message = ex.Message });
        }
    }

    [HttpGet("sync-logs")]
    public async Task<IActionResult> SyncLogs(int limit = 100, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var logs = await db.ErpSyncLogs.AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new { x.EventKey, x.EventType, x.OrderNo, x.Status, x.AttemptCount, x.LastError, x.OccurredAt, x.ProcessedAt })
            .ToListAsync(cancellationToken);
        return Ok(logs);
    }

    [HttpGet("report-deliveries")]
    public async Task<IActionResult> ReportDeliveries(int limit = 100, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var deliveries = await db.ErpReportDeliveries.AsNoTracking()
            .Include(x => x.WorkOrder)
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new { x.EventKey, orderNo = x.WorkOrder!.OrderNo, x.Status, x.AttemptCount, x.LastError, x.CreatedAt, x.SentAt })
            .ToListAsync(cancellationToken);
        return Ok(deliveries);
    }

    [HttpGet("quality-deliveries")]
    public async Task<IActionResult> QualityDeliveries(int limit = 100, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var deliveries = await db.ErpQualityDeliveries.AsNoTracking()
            .Include(x => x.WorkOrder)
            .OrderByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new { x.EventKey, orderNo = x.WorkOrder!.OrderNo, x.Status, x.AttemptCount, x.LastError, x.CreatedAt, x.SentAt })
            .ToListAsync(cancellationToken);
        return Ok(deliveries);
    }

    private static ObjectResult ToProblem(InvalidOperationException exception)
    {
        var statusCode = exception is WarehouseFlowIntegrationException remote
            ? remote.StatusCode
            : StatusCodes.Status502BadGateway;
        return new ObjectResult(new { message = exception.Message }) { StatusCode = statusCode };
    }

}
