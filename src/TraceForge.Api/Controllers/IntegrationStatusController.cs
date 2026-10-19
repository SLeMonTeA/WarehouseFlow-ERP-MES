using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Contracts;
using TraceForge.Api.Domain;
using TraceForge.Api.Infrastructure;

namespace TraceForge.Api.Controllers;

[ApiController]
[Route("api/integration/status")]
public sealed class IntegrationStatusController(TraceForgeDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IntegrationStatusResponse>> Get(CancellationToken cancellationToken)
    {
        var erpLogs = db.ErpSyncLogs.AsNoTracking();
        var erpLastSucceeded = await erpLogs.Where(x => x.Status == ErpSyncLogStatus.Succeeded || x.Status == ErpSyncLogStatus.Conflict).OrderByDescending(x => x.ProcessedAt).FirstOrDefaultAsync(cancellationToken);
        var erpLastFailed = await erpLogs.Where(x => x.Status == ErpSyncLogStatus.Failed).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var execution = db.LumenGridExecutionDeliveries.AsNoTracking();
        var executionLastSucceeded = await execution.Where(x => x.Status == LumenGridDeliveryStatus.Sent).OrderByDescending(x => x.SentAt).FirstOrDefaultAsync(cancellationToken);
        var executionLastFailed = await execution.Where(x => x.Status == LumenGridDeliveryStatus.Failed).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var inbox = db.LumenGridEquipmentNotifications.AsNoTracking();
        var inboxLast = await inbox.OrderByDescending(x => x.ReceivedAt).FirstOrDefaultAsync(cancellationToken);

        return Ok(new IntegrationStatusResponse(
            "TraceForge",
            DateTimeOffset.UtcNow,
            [
                new IntegrationChannelStatus(
                    "erp-production-order",
                    "inbound",
                    "WarehouseFlow",
                    await erpLogs.CountAsync(x => x.Status == ErpSyncLogStatus.Pending, cancellationToken),
                    await erpLogs.CountAsync(x => x.Status == ErpSyncLogStatus.Succeeded || x.Status == ErpSyncLogStatus.Conflict, cancellationToken),
                    await erpLogs.CountAsync(x => x.Status == ErpSyncLogStatus.Failed, cancellationToken),
                    erpLastSucceeded?.ProcessedAt,
                    erpLastFailed?.CreatedAt,
                    erpLastFailed?.LastError),
                new IntegrationChannelStatus(
                    "lumengrid-execution-binding",
                    "outbound",
                    "LumenGrid",
                    await execution.CountAsync(x => x.Status == LumenGridDeliveryStatus.Pending, cancellationToken),
                    await execution.CountAsync(x => x.Status == LumenGridDeliveryStatus.Sent, cancellationToken),
                    await execution.CountAsync(x => x.Status == LumenGridDeliveryStatus.Failed, cancellationToken),
                    executionLastSucceeded?.SentAt,
                    executionLastFailed?.CreatedAt,
                    executionLastFailed?.LastError),
                new IntegrationChannelStatus(
                    "lumengrid-equipment-notification",
                    "inbound",
                    "LumenGrid",
                    0,
                    await inbox.CountAsync(cancellationToken),
                    0,
                    inboxLast?.ReceivedAt,
                    null,
                    null)
            ]));
    }
}
