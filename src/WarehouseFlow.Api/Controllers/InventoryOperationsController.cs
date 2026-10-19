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
[Route("api/inventory-transfers")]
[Authorize(Policy = PermissionCodes.InventoryOperate)]
public sealed class InventoryTransfersController(WmsDbContext db, IHubContext<InventoryHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.InventoryTransfers.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.CreatedAt)
        .Select(x => new { x.TransferNo, x.FromLocationCode, x.ToLocationCode, x.Operator, x.Status, x.CreatedAt, x.AppliedAt, lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.Quantity, l.BatchNo, l.ExpiryDate }) })
        .ToListAsync(cancellationToken));

    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateInventoryTransferRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TransferNo) || string.IsNullOrWhiteSpace(request.Operator) || request.FromLocationCode == request.ToLocationCode)
            return BadRequest(new { message = "调拨单号、操作人不能为空，来源库位和目标库位不能相同" });
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.BatchNo)) || request.Lines.GroupBy(x => x.Sku).Any(x => x.Count() > 1))
            return BadRequest(new { message = "调拨单至少需要一条不重复的有效明细" });
        if (await db.InventoryTransfers.AnyAsync(x => x.TransferNo == request.TransferNo, cancellationToken)) return Conflict(new { message = "调拨单号已存在" });
        var fromExists = await db.Locations.AnyAsync(x => x.Code == request.FromLocationCode, cancellationToken);
        var toExists = await db.Locations.AnyAsync(x => x.Code == request.ToLocationCode, cancellationToken);
        if (!fromExists || !toExists) return NotFound(new { message = "来源或目标库位不存在" });
        var skus = request.Lines.Select(x => x.Sku).ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });
        var transfer = new InventoryTransfer { TransferNo = request.TransferNo, FromLocationCode = request.FromLocationCode, ToLocationCode = request.ToLocationCode, Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator };
        transfer.Lines.AddRange(request.Lines.Select(x => new InventoryTransferLine { ProductId = products[x.Sku].Id, Quantity = x.Quantity, BatchNo = x.BatchNo, ExpiryDate = x.ExpiryDate }));
        db.InventoryTransfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), null, new { transfer.TransferNo, transfer.Status });
    }

    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("{transferNo}/apply")]
    public async Task<IActionResult> Apply(string transferNo, CancellationToken cancellationToken)
    {
        var transfer = await db.InventoryTransfers.Include(x => x.Lines).ThenInclude(x => x.Product).SingleOrDefaultAsync(x => x.TransferNo == transferNo, cancellationToken);
        if (transfer is null) return NotFound(new { message = "调拨单不存在" });
        if (transfer.Status != InventoryOperationStatus.Draft) return Conflict(new { message = "当前调拨单已执行或不可执行" });
        var from = await db.Locations.SingleOrDefaultAsync(x => x.Code == transfer.FromLocationCode, cancellationToken);
        var to = await db.Locations.SingleOrDefaultAsync(x => x.Code == transfer.ToLocationCode, cancellationToken);
        if (from is null || to is null) return NotFound(new { message = "来源或目标库位不存在" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var targetOnHand = await db.Inventory.Where(x => x.LocationId == to.Id).SumAsync(x => x.OnHand, cancellationToken);
        foreach (var line in transfer.Lines)
        {
            var source = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == line.ProductId && x.LocationId == from.Id && x.BatchNo == line.BatchNo, cancellationToken);
            if (source is null || source.ExpiryDate is not null && source.ExpiryDate <= DateTimeOffset.UtcNow || source.OnHand - source.Reserved - source.QualityHold < line.Quantity) return Conflict(new { message = $"SKU {line.Product!.Sku} 来源库位可用库存不足、批次不存在或已过期" });
            var target = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == line.ProductId && x.LocationId == to.Id && x.BatchNo == line.BatchNo, cancellationToken);
            if (target is null)
            {
                target = new InventoryBalance { ProductId = line.ProductId, LocationId = to.Id, BatchNo = source.BatchNo, ExpiryDate = source.ExpiryDate };
                db.Inventory.Add(target);
            }
            else if (target.ExpiryDate != source.ExpiryDate) return Conflict(new { message = "目标库位同批次效期不一致" });
            if (targetOnHand + line.Quantity > to.Capacity) return Conflict(new { message = $"目标库位 {to.Code} 容量不足" });
            source.OnHand -= line.Quantity;
            target.OnHand += line.Quantity;
            targetOnHand += line.Quantity;
            db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = from.Id, Type = InventoryMovementType.Transfer, Quantity = -line.Quantity, BatchNo = source.BatchNo, ExpiryDate = source.ExpiryDate, ReferenceNo = transfer.TransferNo, Operator = transfer.Operator });
            db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = to.Id, Type = InventoryMovementType.Transfer, Quantity = line.Quantity, BatchNo = source.BatchNo, ExpiryDate = source.ExpiryDate, ReferenceNo = transfer.TransferNo, Operator = transfer.Operator });
        }
        transfer.Status = InventoryOperationStatus.Applied;
        transfer.AppliedAt = DateTimeOffset.UtcNow;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InventoryTransfer", DocumentNo = transfer.TransferNo, Action = "Apply", FromStatus = InventoryOperationStatus.Draft.ToString(), ToStatus = InventoryOperationStatus.Applied.ToString(), Operator = transfer.Operator });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "transferApplied", transferNo }, cancellationToken);
        return Ok(new { transfer.TransferNo, transfer.Status, transfer.AppliedAt });
    }
}

[ApiController]
[Route("api/stocktakes")]
[Authorize(Policy = PermissionCodes.InventoryOperate)]
public sealed class StocktakesController(WmsDbContext db, IHubContext<InventoryHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.StocktakeOrders.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.CreatedAt)
        .Select(x => new { x.CheckNo, x.LocationCode, x.Operator, x.Status, x.CreatedAt, x.AppliedAt, lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.BatchNo, l.ExpiryDate, l.BookQuantity, l.CountedQuantity, difference = l.CountedQuantity - l.BookQuantity }) })
        .ToListAsync(cancellationToken));

    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateStocktakeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CheckNo) || string.IsNullOrWhiteSpace(request.Operator)) return BadRequest(new { message = "盘点单号和操作人不能为空" });
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.CountedQuantity < 0 || string.IsNullOrWhiteSpace(x.BatchNo)) || request.Lines.GroupBy(x => x.Sku).Any(x => x.Count() > 1)) return BadRequest(new { message = "盘点单至少需要一条不重复且带批次的有效明细" });
        if (await db.StocktakeOrders.AnyAsync(x => x.CheckNo == request.CheckNo, cancellationToken)) return Conflict(new { message = "盘点单号已存在" });
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == request.LocationCode, cancellationToken);
        if (location is null) return NotFound(new { message = "盘点库位不存在" });
        var skus = request.Lines.Select(x => x.Sku).ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });
        var productIds = products.Values.Select(x => x.Id).ToList();
        var balances = await db.Inventory.Where(x => x.LocationId == location.Id && productIds.Contains(x.ProductId)).ToListAsync(cancellationToken);
        var stocktake = new StocktakeOrder { CheckNo = request.CheckNo, LocationCode = request.LocationCode, Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator };
        stocktake.Lines.AddRange(request.Lines.Select(x => new StocktakeLine { ProductId = products[x.Sku].Id, BatchNo = x.BatchNo, BookQuantity = balances.SingleOrDefault(b => b.ProductId == products[x.Sku].Id && b.BatchNo == x.BatchNo)?.OnHand ?? 0, CountedQuantity = x.CountedQuantity }));
        db.StocktakeOrders.Add(stocktake);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), null, new { stocktake.CheckNo, stocktake.Status });
    }

    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("{checkNo}/apply")]
    public async Task<IActionResult> Apply(string checkNo, CancellationToken cancellationToken)
    {
        var stocktake = await db.StocktakeOrders.Include(x => x.Lines).ThenInclude(x => x.Product).SingleOrDefaultAsync(x => x.CheckNo == checkNo, cancellationToken);
        if (stocktake is null) return NotFound(new { message = "盘点单不存在" });
        if (stocktake.Status != InventoryOperationStatus.Draft) return Conflict(new { message = "当前盘点单已应用或不可应用" });
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == stocktake.LocationCode, cancellationToken);
        if (location is null) return NotFound(new { message = "盘点库位不存在" });
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in stocktake.Lines)
        {
            var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == line.ProductId && x.LocationId == location.Id && x.BatchNo == line.BatchNo, cancellationToken);
            if (balance is null) { balance = new InventoryBalance { ProductId = line.ProductId, LocationId = location.Id, BatchNo = line.BatchNo }; db.Inventory.Add(balance); }
            if (line.CountedQuantity < balance.Reserved + balance.QualityHold) return Conflict(new { message = $"SKU {line.Product!.Sku} 实盘数不能低于已预占和质检冻结库存" });
            var difference = line.CountedQuantity - balance.OnHand;
            if (difference == 0) continue;
            balance.OnHand = line.CountedQuantity;
            db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = location.Id, Type = InventoryMovementType.Adjust, Quantity = difference, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, ReferenceNo = stocktake.CheckNo, Operator = stocktake.Operator });
        }
        stocktake.Status = InventoryOperationStatus.Applied;
        stocktake.AppliedAt = DateTimeOffset.UtcNow;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "Stocktake", DocumentNo = stocktake.CheckNo, Action = "Apply", FromStatus = InventoryOperationStatus.Draft.ToString(), ToStatus = InventoryOperationStatus.Applied.ToString(), Operator = stocktake.Operator });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "stocktakeApplied", checkNo }, cancellationToken);
        return Ok(new { stocktake.CheckNo, stocktake.Status, stocktake.AppliedAt });
    }
}
