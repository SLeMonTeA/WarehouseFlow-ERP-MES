using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Domain;
using TraceForge.Api.Infrastructure;

namespace TraceForge.Api.Controllers;

[ApiController]
[Route("api/integration/lumengrid")]
public sealed class LumenGridExecutionDeliveryController(
    TraceForgeDbContext db,
    LumenGridExecutionDeliveryService deliveryService) : ControllerBase
{
    [HttpGet("execution-deliveries")]
    public async Task<IActionResult> List(
        [FromQuery] LumenGridDeliveryStatus? status = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = db.LumenGridExecutionDeliveries.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.Id).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new
        {
            x.Id,
            x.EventKey,
            x.EventType,
            x.DeviceCode,
            x.WorkOrderNo,
            x.OperationCode,
            x.WorkCenter,
            x.BindingStatus,
            x.Status,
            x.AttemptCount,
            x.LastError,
            x.CreatedAt,
            x.NextAttemptAt,
            x.SentAt
        }));
    }

    [HttpPost("execution-deliveries/retry")]
    public async Task<IActionResult> Retry(
        [FromQuery] string? eventKey = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var delivered = await deliveryService.RetryAsync(eventKey, limit, cancellationToken);
        return Ok(new { delivered, eventKey, limit = Math.Clamp(limit, 1, 100) });
    }
}
