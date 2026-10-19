using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentCenterController(WmsDbContext db) : ControllerBase
{
    [HttpGet("{documentType}/{documentNo}/audits")]
    public async Task<IActionResult> Audits(string documentType, string documentNo, CancellationToken cancellationToken) => Ok(await db.DocumentAudits.AsNoTracking().Where(x => x.DocumentType == documentType && x.DocumentNo == documentNo).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken));

    [HttpGet("{documentType}/{documentNo}/relations")]
    public async Task<IActionResult> Relations(string documentType, string documentNo, CancellationToken cancellationToken) => Ok(await db.DocumentRelations.AsNoTracking().Where(x => (x.SourceType == documentType && x.SourceNo == documentNo) || (x.TargetType == documentType && x.TargetNo == documentNo)).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken));
}
