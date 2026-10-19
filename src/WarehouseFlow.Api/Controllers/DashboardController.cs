using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = PermissionCodes.DashboardView)]
public sealed class DashboardController(WmsDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> Summary(CancellationToken cancellationToken)
    {
        var balances = db.Inventory.AsNoTracking();
        var now = DateTimeOffset.UtcNow;
        var result = new DashboardSummary(
            await db.Products.CountAsync(x => x.IsActive, cancellationToken),
            await db.Locations.CountAsync(cancellationToken),
            await balances.SumAsync(x => (decimal?)x.OnHand, cancellationToken) ?? 0,
            await balances.SumAsync(x => (decimal?)(x.ExpiryDate != null && x.ExpiryDate <= now ? 0 : x.OnHand - x.Reserved - x.QualityHold), cancellationToken) ?? 0,
            await balances.Include(x => x.Product).CountAsync(x => (x.ExpiryDate != null && x.ExpiryDate <= now ? 0 : x.OnHand - x.Reserved - x.QualityHold) < x.Product!.SafetyStock, cancellationToken),
            await db.InboundOrders.CountAsync(x => x.Status == OrderStatus.Confirmed || x.Status == OrderStatus.InProgress, cancellationToken),
            await db.OutboundOrders.CountAsync(x => x.Status == OrderStatus.Confirmed || x.Status == OrderStatus.InProgress, cancellationToken));
        return Ok(result);
    }

    [HttpGet("recent-movements")]
    public async Task<IActionResult> RecentMovements([FromQuery] int take = 8, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 30);
        var result = await db.Movements.AsNoTracking().Include(x => x.Product).Include(x => x.Location)
            .OrderByDescending(x => x.CreatedAt).Take(take)
            .Select(x => new { x.Id, sku = x.Product!.Sku, productName = x.Product.Name, locationCode = x.Location!.Code, x.BatchNo, x.ExpiryDate, type = x.Type.ToString(), x.Quantity, x.ReferenceNo, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(result);
    }
}
