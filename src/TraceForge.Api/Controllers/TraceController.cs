using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Infrastructure;

namespace TraceForge.Api.Controllers;

[ApiController]
[Route("api/trace")]
public sealed class TraceController(TraceForgeDbContext db) : ControllerBase
{
    [HttpGet("{batchNo}")]
    public async Task<IActionResult> Get(string batchNo, CancellationToken cancellationToken)
    {
        var events = await db.TraceEvents.AsNoTracking().Where(x => x.BatchNo == batchNo)
            .OrderBy(x => x.OccurredAt).Select(x => new { x.EventType, x.EventData, x.OccurredAt }).ToListAsync(cancellationToken);
        return Ok(new { batchNo, events });
    }
}
