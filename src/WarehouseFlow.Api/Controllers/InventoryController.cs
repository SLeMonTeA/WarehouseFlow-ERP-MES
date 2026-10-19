using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;
using WarehouseFlow.Api.Realtime;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(WmsDbContext db, IHubContext<InventoryHub> hub) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<ActionResult<IReadOnlyList<InventoryRow>>> Get(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Inventory.AsNoTracking().Include(x => x.Product).Include(x => x.Location)
            .Where(x => x.OnHand != 0 || x.Reserved != 0 || x.QualityHold != 0)
            .OrderBy(x => x.Product!.Sku)
            .Select(x => new InventoryRow(x.Product!.Sku, x.Product.Name, x.Product.Category, x.Location!.Code,
                x.BatchNo, x.ExpiryDate, x.OnHand, x.Reserved, x.QualityHold,
                x.ExpiryDate != null && x.ExpiryDate <= now ? 0 : x.OnHand - x.Reserved - x.QualityHold, x.Product.SafetyStock)).ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("receive")]
    public async Task<IActionResult> Receive(ReceiveStockRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) return BadRequest(new { message = "入库数量必须大于 0" });
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.Sku, cancellationToken);
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == request.LocationCode, cancellationToken);
        if (product is null || location is null) return NotFound(new { message = "SKU 或库位不存在" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.BatchNo)) return BadRequest(new { message = "批次号不能为空" });
        if (request.ExpiryDate is not null && request.ExpiryDate <= DateTimeOffset.UtcNow) return BadRequest(new { message = "效期必须晚于当前时间" });
        var locationOnHand = await db.Inventory.Where(x => x.LocationId == location.Id).SumAsync(x => x.OnHand, cancellationToken);
        var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == product.Id && x.LocationId == location.Id && x.BatchNo == request.BatchNo, cancellationToken);
        if (balance is null)
        {
            balance = new InventoryBalance { ProductId = product.Id, LocationId = location.Id, BatchNo = request.BatchNo, ExpiryDate = request.ExpiryDate };
            db.Inventory.Add(balance);
        }
        else if (balance.ExpiryDate != request.ExpiryDate && request.ExpiryDate is not null)
            return Conflict(new { message = "同一批次的效期不一致" });
        if (locationOnHand + request.Quantity > location.Capacity)
            return Conflict(new { message = "入库后将超过库位容量" });
        balance.OnHand += request.Quantity;
        db.Movements.Add(new InventoryMovement
        {
            ProductId = product.Id, LocationId = location.Id, Quantity = request.Quantity,
            Type = InventoryMovementType.Receive, ReferenceNo = request.ReferenceNo,
            Operator = User.FindFirstValue(ClaimTypes.Name) ?? "unknown", BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await InvalidateAndNotifyAsync(product.Sku, "receive", cancellationToken);
        return Ok(new { request.Sku, request.LocationCode, balance.BatchNo, balance.ExpiryDate, balance.OnHand, balance.Reserved, balance.QualityHold, available = balance.ExpiryDate is not null && balance.ExpiryDate <= DateTimeOffset.UtcNow ? 0 : balance.OnHand - balance.Reserved - balance.QualityHold });
    }

    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve(ReserveStockRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) return BadRequest(new { message = "预占数量必须大于 0" });
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.Sku, cancellationToken);
        if (product is null) return NotFound(new { message = "SKU 不存在" });
        var now = DateTimeOffset.UtcNow;
        var balances = await db.Inventory.Where(x => x.ProductId == product.Id && (x.ExpiryDate == null || x.ExpiryDate > now)).OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.LocationId).ToListAsync(cancellationToken);
        var totalAvailable = balances.Sum(x => x.OnHand - x.Reserved - x.QualityHold);
        if (totalAvailable < request.Quantity)
            return Conflict(new { message = "可用库存不足，无法预占" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var remaining = request.Quantity;
        foreach (var balance in balances)
        {
            if (remaining <= 0) break;
            var allocated = Math.Min(remaining, balance.OnHand - balance.Reserved - balance.QualityHold);
            if (allocated <= 0) continue;
            balance.Reserved += allocated;
            remaining -= allocated;
            db.Movements.Add(new InventoryMovement
            {
                ProductId = product.Id, LocationId = balance.LocationId, Quantity = allocated,
                Type = InventoryMovementType.Reserve, ReferenceNo = request.OrderNo, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate,
                Operator = User.FindFirstValue(ClaimTypes.Name) ?? "unknown"
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await InvalidateAndNotifyAsync(product.Sku, "reserve", cancellationToken);
        return Ok(new { request.Sku, request.OrderNo, reserved = request.Quantity, available = totalAvailable - request.Quantity });
    }

    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust(AdjustStockRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.Sku, cancellationToken);
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == request.LocationCode, cancellationToken);
        if (product is null || location is null) return NotFound(new { message = "SKU 或库位不存在" });
        var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == product.Id && x.LocationId == location.Id && x.BatchNo == request.BatchNo, cancellationToken);
        if (balance is null || balance.OnHand + request.Quantity < balance.Reserved + balance.QualityHold)
            return Conflict(new { message = "调整后库存不能低于已预占和质检冻结库存" });

        balance.OnHand += request.Quantity;
        db.Movements.Add(new InventoryMovement
        {
            ProductId = product.Id, LocationId = location.Id, Quantity = request.Quantity,
            Type = InventoryMovementType.Adjust, ReferenceNo = request.Reason,
            Operator = User.FindFirstValue(ClaimTypes.Name) ?? "unknown", BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate
        });
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAndNotifyAsync(product.Sku, "adjust", cancellationToken);
        return Ok(new { request.Sku, request.LocationCode, balance.BatchNo, balance.ExpiryDate, balance.OnHand, balance.Reserved, balance.QualityHold, available = balance.ExpiryDate is not null && balance.ExpiryDate <= DateTimeOffset.UtcNow ? 0 : balance.OnHand - balance.Reserved - balance.QualityHold });
    }

    [HttpGet("movements")]
    [Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> Movements([FromQuery] string? sku, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        var query = db.Movements.AsNoTracking().Include(x => x.Product).Include(x => x.Location).AsQueryable();
        if (!string.IsNullOrWhiteSpace(sku)) query = query.Where(x => x.Product!.Sku == sku);
        var result = await query.OrderByDescending(x => x.CreatedAt).Take(take)
            .Select(x => new { x.Id, sku = x.Product!.Sku, productName = x.Product.Name, locationCode = x.Location!.Code, x.BatchNo, x.ExpiryDate, type = x.Type.ToString(), x.Quantity, x.ReferenceNo, x.Operator, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(result);
    }

    private async Task InvalidateAndNotifyAsync(string sku, string action, CancellationToken cancellationToken)
    {
        await hub.Clients.All.SendAsync("InventoryChanged", new { sku, action, changedAt = DateTimeOffset.UtcNow }, cancellationToken);
    }
}
