using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Application;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;
using WarehouseFlow.Api.Realtime;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/returns")]
[Authorize(Policy = PermissionCodes.ReturnsOperate)]
public sealed class InventoryReturnsController(WmsDbContext db, IDistributedCache cache, IHubContext<InventoryHub> hub, DocumentNumberService numbers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.InventoryReturnOrders.AsNoTracking()
        .Include(x => x.Lines).ThenInclude(x => x.Product)
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new
        {
            x.ReturnNo, x.ReturnType, x.SourceDocumentNo, x.PartnerName, x.LocationCode, x.Operator, x.Status, x.CreatedAt, x.AppliedAt,
            lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.Quantity, l.FromQualityHold, l.BatchNo, l.ExpiryDate })
        }).ToListAsync(cancellationToken));

    [HttpGet("{returnNo}")]
    public async Task<IActionResult> Get(string returnNo, CancellationToken cancellationToken)
    {
        var result = await db.InventoryReturnOrders.AsNoTracking().Where(x => x.ReturnNo == returnNo)
            .Select(x => new
            {
                x.ReturnNo, x.ReturnType, x.SourceDocumentNo, x.PartnerName, x.LocationCode, x.Operator, x.Status, x.CreatedAt, x.AppliedAt,
                lines = x.Lines.Select(l => new { l.Id, sku = l.Product!.Sku, productName = l.Product.Name, l.Quantity, l.FromQualityHold, l.BatchNo, l.ExpiryDate })
            }).SingleOrDefaultAsync(cancellationToken);
        return result is null ? NotFound(new { message = "退货单不存在" }) : Ok(result);
    }

    [Authorize(Policy = PermissionCodes.ReturnsOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateInventoryReturnRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PartnerName) || string.IsNullOrWhiteSpace(request.LocationCode))
            return BadRequest(new { message = "往来单位和库位不能为空" });
        var returnNo = string.IsNullOrWhiteSpace(request.ReturnNo) ? await numbers.NextAsync("InventoryReturn", cancellationToken) : request.ReturnNo.Trim();
        if (request.Lines is null || request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0) || request.Lines.GroupBy(x => x.Sku).Any(x => x.Count() > 1))
            return BadRequest(new { message = "退货单至少需要一条不重复的有效明细" });
        if (await db.InventoryReturnOrders.AnyAsync(x => x.ReturnNo == returnNo, cancellationToken))
            return Conflict(new { message = "退货单号已存在" });

        var locationExists = await db.Locations.AnyAsync(x => x.Code == request.LocationCode, cancellationToken);
        if (!locationExists) return NotFound(new { message = "退货库位不存在" });
        if (request.ReturnType == InventoryReturnType.SalesReturn && request.Lines.Any(x => x.FromQualityHold))
            return BadRequest(new { message = "销售退货不能使用质检冻结库存" });
        if (request.Lines.Any(x => string.IsNullOrWhiteSpace(x.BatchNo))) return BadRequest(new { message = "退货明细批次号不能为空" });
        if (request.ReturnType == InventoryReturnType.SalesReturn && request.Lines.Any(x => x.ExpiryDate is not null && x.ExpiryDate <= DateTimeOffset.UtcNow))
            return BadRequest(new { message = "销售退货效期必须晚于当前时间" });

        if (!string.IsNullOrWhiteSpace(request.SourceDocumentNo))
        {
            var sourceExists = request.ReturnType == InventoryReturnType.PurchaseReturn
                ? await db.PurchaseOrders.AnyAsync(x => x.OrderNo == request.SourceDocumentNo, cancellationToken)
                : await db.SalesOrders.AnyAsync(x => x.OrderNo == request.SourceDocumentNo, cancellationToken);
            if (!sourceExists) return NotFound(new { message = "来源业务单据不存在" });
        }

        var skus = request.Lines.Select(x => x.Sku).ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator;
        var order = new InventoryReturnOrder
        {
            ReturnNo = returnNo,
            ReturnType = request.ReturnType,
            SourceDocumentNo = request.SourceDocumentNo,
            PartnerName = request.PartnerName,
            LocationCode = request.LocationCode,
            Operator = actor
        };
        order.Lines.AddRange(request.Lines.Select(x => new InventoryReturnLine
        {
            ProductId = products[x.Sku].Id,
            Quantity = x.Quantity,
            FromQualityHold = x.FromQualityHold,
            BatchNo = x.BatchNo,
            ExpiryDate = x.ExpiryDate
        }));
        db.InventoryReturnOrders.Add(order);
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InventoryReturn", DocumentNo = order.ReturnNo, Action = "Create", ToStatus = order.Status.ToString(), Operator = actor });
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(order.SourceDocumentNo))
            db.DocumentRelations.Add(new DocumentRelation { SourceType = order.ReturnType == InventoryReturnType.PurchaseReturn ? "PurchaseOrder" : "SalesOrder", SourceNo = order.SourceDocumentNo, TargetType = "InventoryReturn", TargetNo = order.ReturnNo, Quantity = order.Lines.Sum(x => x.Quantity) });
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { returnNo = order.ReturnNo }, new { order.ReturnNo, order.ReturnType, order.Status });
    }

    [Authorize(Policy = PermissionCodes.ReturnsOperate)]
    [HttpPost("{returnNo}/apply")]
    public async Task<IActionResult> Apply(string returnNo, CancellationToken cancellationToken)
    {
        var order = await db.InventoryReturnOrders.Include(x => x.Lines).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.ReturnNo == returnNo, cancellationToken);
        if (order is null) return NotFound(new { message = "退货单不存在" });
        if (order.Status != InventoryOperationStatus.Draft) return Conflict(new { message = "当前退货单已执行或不可执行" });
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == order.LocationCode, cancellationToken);
        if (location is null) return NotFound(new { message = "退货库位不存在" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in order.Lines)
        {
            var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == line.ProductId && x.LocationId == location.Id && x.BatchNo == line.BatchNo, cancellationToken);
            if (order.ReturnType == InventoryReturnType.PurchaseReturn)
            {
                if (balance is null) return Conflict(new { message = $"SKU {line.Product!.Sku} 库存不足，无法采购退货" });
                var available = balance.OnHand - balance.Reserved - balance.QualityHold;
                if (line.FromQualityHold)
                {
                    if (balance.QualityHold < line.Quantity || balance.OnHand - balance.Reserved < line.Quantity)
                        return Conflict(new { message = $"SKU {line.Product!.Sku} 质检冻结库存不足，无法退回供应商" });
                    balance.QualityHold -= line.Quantity;
                }
                else if (available < line.Quantity)
                    return Conflict(new { message = $"SKU {line.Product!.Sku} 可用库存不足，无法采购退货" });
                balance.OnHand -= line.Quantity;
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = location.Id, Type = InventoryMovementType.PurchaseReturn, Quantity = -line.Quantity, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, ReferenceNo = order.ReturnNo, Operator = order.Operator });
            }
            else
            {
                if (balance is null)
                {
                    balance = new InventoryBalance { ProductId = line.ProductId, LocationId = location.Id, BatchNo = line.BatchNo, ExpiryDate = line.ExpiryDate };
                    db.Inventory.Add(balance);
                }
                else if (line.ExpiryDate is not null && balance.ExpiryDate != line.ExpiryDate)
                    return Conflict(new { message = $"SKU {line.Product!.Sku} 同批次效期不一致" });
                if (balance.OnHand + line.Quantity > location.Capacity) return Conflict(new { message = $"库位 {location.Code} 容量不足，无法销售退货入库" });
                balance.OnHand += line.Quantity;
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = location.Id, Type = InventoryMovementType.SalesReturn, Quantity = line.Quantity, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, ReferenceNo = order.ReturnNo, Operator = order.Operator });
            }
        }

        order.Status = InventoryOperationStatus.Applied;
        order.AppliedAt = DateTimeOffset.UtcNow;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InventoryReturn", DocumentNo = order.ReturnNo, Action = "Apply", FromStatus = InventoryOperationStatus.Draft.ToString(), ToStatus = order.Status.ToString(), Operator = order.Operator });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await cache.RemoveAsync("wms:inventory:all", cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "returnApplied", returnNo }, cancellationToken);
        return Ok(new { order.ReturnNo, order.ReturnType, order.Status, order.AppliedAt });
    }

    [Authorize(Policy = PermissionCodes.ReturnsOperate)]
    [HttpPost("{returnNo}/cancel")]
    public async Task<IActionResult> Cancel(string returnNo, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        var order = await db.InventoryReturnOrders.SingleOrDefaultAsync(x => x.ReturnNo == returnNo, cancellationToken);
        if (order is null) return NotFound(new { message = "退货单不存在" });
        if (order.Status != InventoryOperationStatus.Draft) return Conflict(new { message = "只有草稿退货单可以作废" });
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator;
        order.Status = InventoryOperationStatus.Cancelled;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InventoryReturn", DocumentNo = order.ReturnNo, Action = "Cancel", FromStatus = InventoryOperationStatus.Draft.ToString(), ToStatus = order.Status.ToString(), Operator = actor, Remark = request.Remark });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { order.ReturnNo, order.Status });
    }
}
