using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Application;
using WarehouseFlow.Api.Application.Partners;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;
using WarehouseFlow.Api.Realtime;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/inbound")]
[Authorize(Policy = PermissionCodes.InboundOperate)]
public sealed class InboundController(WmsDbContext db, IHubContext<InventoryHub> hub, PartnerLookupService partners, DocumentNumberService numbers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await db.InboundOrders.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.ExpectedAt)
            .Select(x => new { x.OrderNo, x.SupplierCode, x.SupplierName, x.SourceDocumentNo, x.Status, x.ExpectedAt, x.CompletedAt, lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.ReceivedQuantity }) })
            .ToListAsync(cancellationToken));

    [Authorize(Policy = PermissionCodes.InboundOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateInboundRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return BadRequest(new { message = "入库单至少需要一条明细" });
        var orderNo = string.IsNullOrWhiteSpace(request.OrderNo) ? await numbers.NextAsync("InboundOrder", cancellationToken) : request.OrderNo.Trim();
        if (await db.InboundOrders.AnyAsync(x => x.OrderNo == orderNo, cancellationToken)) return Conflict(new { message = "入库单号已存在" });
        if (request.Lines.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return BadRequest(new { message = "同一入库单不能重复录入 SKU，请合并数量" });
        var skus = request.Lines.Select(x => x.Sku).Distinct().ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });
        if (request.Lines.Any(x => x.Quantity <= 0)) return BadRequest(new { message = "入库数量必须大于 0" });
        (string Code, string Name) supplier;
        try { supplier = await partners.ResolveSupplierAsync(request.SupplierCode, request.SupplierName, cancellationToken); }
        catch (KeyNotFoundException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }

        var order = new InboundOrder { OrderNo = orderNo, SupplierCode = supplier.Code, SupplierName = supplier.Name, ExpectedAt = request.ExpectedAt, Status = OrderStatus.Confirmed };
        order.Lines.AddRange(request.Lines.Select(x => new InboundLine { ProductId = products[x.Sku].Id, PlannedQuantity = x.Quantity }));
        db.InboundOrders.Add(order);
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InboundOrder", DocumentNo = order.OrderNo, Action = "Create", FromStatus = "", ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system" });
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { orderNo = order.OrderNo }, new { order.OrderNo, order.SupplierCode, order.SupplierName, order.Status, order.ExpectedAt, lines = order.Lines.Select(x => new { x.ProductId, x.PlannedQuantity, x.ReceivedQuantity }) });
    }

    [Authorize(Policy = PermissionCodes.InboundOperate)]
    [HttpPut("{orderNo}")]
    public async Task<IActionResult> Update(string orderNo, UpdateInboundRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return BadRequest(new { message = "入库单至少需要一条明细" });
        if (request.Lines.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return BadRequest(new { message = "同一入库单不能重复录入 SKU，请合并数量" });
        if (request.Lines.Any(x => x.Quantity <= 0)) return BadRequest(new { message = "入库数量必须大于 0" });
        var order = await db.InboundOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "入库单不存在" });
        if (!string.IsNullOrWhiteSpace(order.SourceDocumentNo) || order.Status != OrderStatus.Confirmed || order.Lines.Any(x => x.ReceivedQuantity > 0)) return Conflict(new { message = "只有未收货的手工入库单可以编辑；下推单请回到来源单调整" });
        var supplier = await partners.ResolveSupplierAsync(request.SupplierCode, request.SupplierName, cancellationToken);
        var skus = request.Lines.Select(x => x.Sku).Distinct().ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });
        db.InboundLines.RemoveRange(order.Lines);
        order.SupplierCode = supplier.Code; order.SupplierName = supplier.Name; order.ExpectedAt = request.ExpectedAt;
        order.Lines = request.Lines.Select(x => new InboundLine { ProductId = products[x.Sku].Id, PlannedQuantity = x.Quantity }).ToList();
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InboundOrder", DocumentNo = order.OrderNo, Action = "Update", FromStatus = order.Status.ToString(), ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system", Remark = "修改手工入库草稿" });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { order.OrderNo, order.SupplierCode, order.SupplierName, order.Status, order.ExpectedAt });
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.InboundOrders.AsNoTracking().Where(x => x.OrderNo == orderNo)
            .Select(x => new { x.OrderNo, x.SupplierCode, x.SupplierName, x.SourceDocumentNo, x.Status, x.ExpectedAt, x.CompletedAt, lines = x.Lines.Select(l => new { l.Id, sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.ReceivedQuantity, openQuantity = l.PlannedQuantity - l.ReceivedQuantity }) })
            .SingleOrDefaultAsync(cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = PermissionCodes.InboundOperate)]
    [HttpPost("{orderNo}/receive")]
    public async Task<IActionResult> Receive(string orderNo, ReceiveInboundRequest request, CancellationToken cancellationToken)
    {
        var order = await db.InboundOrders.Include(x => x.Lines).ThenInclude(x => x.Product).Include(x => x.SourceReceiptNotice).ThenInclude(x => x!.Lines).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == request.LocationCode, cancellationToken);
        if (order is null || location is null) return NotFound(new { message = "入库单或库位不存在" });
        if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled) return Conflict(new { message = "当前入库单状态不允许收货" });
        if (request.Lines is not null && request.Lines.GroupBy(x => x.Sku).Any(x => x.Count() > 1))
            return BadRequest(new { message = "部分收货明细不能重复 SKU" });
        var requested = request.Lines?.ToDictionary(x => x.Sku, x => x);
        if (requested is not null && (requested.Values.Any(x => x.Quantity < 0) || requested.Keys.Any(x => order.Lines.All(l => l.Product!.Sku != x))))
            return BadRequest(new { message = "部分收货明细必须使用本入库单 SKU，且数量不能小于 0" });

        var fromStatus = order.Status;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var locationOnHand = await db.Inventory.Where(x => x.LocationId == location.Id).SumAsync(x => x.OnHand, cancellationToken);
        var receivedByProduct = new Dictionary<int, decimal>();
        foreach (var line in order.Lines)
        {
            var remaining = line.PlannedQuantity - line.ReceivedQuantity;
            var requestLine = requested?.GetValueOrDefault(line.Product!.Sku);
            var receiving = requested is null ? remaining : requestLine?.Quantity ?? 0;
            if (receiving <= 0) continue;
            if (receiving > remaining) return Conflict(new { message = $"SKU {line.Product!.Sku} 收货数量超过未收数量" });
            var batchNo = string.IsNullOrWhiteSpace(requestLine?.BatchNo) || requestLine!.BatchNo == "UNBATCHED" ? request.BatchNo : requestLine.BatchNo;
            if (string.IsNullOrWhiteSpace(batchNo)) batchNo = "UNBATCHED";
            var expiryDate = requestLine?.ExpiryDate ?? request.ExpiryDate;
            if (expiryDate is not null && expiryDate <= DateTimeOffset.UtcNow) return BadRequest(new { message = $"SKU {line.Product!.Sku} 的效期必须晚于当前时间" });
            var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == line.ProductId && x.LocationId == location.Id && x.BatchNo == batchNo, cancellationToken);
            if (balance is null)
            {
                balance = new InventoryBalance { ProductId = line.ProductId, LocationId = location.Id, BatchNo = batchNo, ExpiryDate = expiryDate };
                db.Inventory.Add(balance);
            }
            else if (expiryDate is not null && balance.ExpiryDate != expiryDate)
                return Conflict(new { message = $"SKU {line.Product!.Sku} 同批次效期不一致" });
            if (locationOnHand + receiving > location.Capacity) return Conflict(new { message = $"库位 {location.Code} 容量不足" });
            balance.OnHand += receiving;
            if (line.Product!.RequiresQuality) balance.QualityHold += receiving;
            locationOnHand += receiving;
            line.ReceivedQuantity += receiving;
            receivedByProduct[line.ProductId] = receivedByProduct.GetValueOrDefault(line.ProductId) + receiving;
            var noticeLine = order.SourceReceiptNotice?.Lines.FirstOrDefault(x => x.ProductId == line.ProductId && x.PlannedQuantity > x.ReceivedQuantity);
            if (noticeLine is not null) noticeLine.ReceivedQuantity += receiving;
            db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = location.Id, Type = InventoryMovementType.Receive, Quantity = receiving, ReferenceNo = order.OrderNo, BatchNo = batchNo, ExpiryDate = expiryDate, Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator });
            if (line.Product.RequiresQuality)
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = location.Id, Type = InventoryMovementType.QualityHold, Quantity = receiving, ReferenceNo = order.OrderNo, BatchNo = batchNo, ExpiryDate = expiryDate, Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator });
        }
        if (order.Lines.All(x => x.ReceivedQuantity >= x.PlannedQuantity))
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTimeOffset.UtcNow;
        }
        else order.Status = OrderStatus.InProgress;
        if (order.SourceReceiptNotice is not null)
        {
            order.SourceReceiptNotice.Status = order.SourceReceiptNotice.Lines.All(x => x.ReceivedQuantity >= x.PlannedQuantity) ? PlanningDocumentStatus.Completed : PlanningDocumentStatus.InProgress;
            if (!string.IsNullOrWhiteSpace(order.SourceReceiptNotice.SourceDocumentNo))
            {
                var purchase = await db.PurchaseOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == order.SourceReceiptNotice.SourceDocumentNo, cancellationToken);
                if (purchase is not null)
                {
                    foreach (var item in receivedByProduct)
                    {
                        var remaining = item.Value;
                        foreach (var purchaseLine in purchase.Lines.Where(x => x.ProductId == item.Key && x.ReceivedQuantity < x.OrderedQuantity))
                        {
                            var received = Math.Min(remaining, purchaseLine.OrderedQuantity - purchaseLine.ReceivedQuantity);
                            purchaseLine.ReceivedQuantity += received;
                            remaining -= received;
                            if (remaining <= 0) break;
                        }
                    }
                    purchase.Status = purchase.Lines.All(x => x.ReceivedQuantity >= x.OrderedQuantity) ? PlanningDocumentStatus.Completed : PlanningDocumentStatus.InProgress;
                }
            }
        }
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InboundOrder", DocumentNo = order.OrderNo, Action = "Receive", FromStatus = fromStatus.ToString(), ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "inboundCompleted", orderNo }, cancellationToken);
        return Ok(new { order.OrderNo, order.Status, order.CompletedAt });
    }

    [Authorize(Policy = PermissionCodes.InboundOperate)]
    [HttpPost("{orderNo}/cancel")]
    public async Task<IActionResult> Cancel(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        var order = await db.InboundOrders.Include(x => x.Lines).Include(x => x.SourceReceiptNotice).ThenInclude(x => x!.Lines)
            .SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "入库单不存在" });
        if (order.Status == OrderStatus.Cancelled) return Conflict(new { message = "入库单已经取消" });
        if (order.Lines.Any(x => x.ReceivedQuantity > 0)) return Conflict(new { message = "已经收货的入库单不能取消" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        if (order.SourceReceiptNotice is not null && order.SourceReceiptNotice.Status != PlanningDocumentStatus.Completed)
            order.SourceReceiptNotice.Status = PlanningDocumentStatus.Approved;

        var fromStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = "InboundOrder",
            DocumentNo = order.OrderNo,
            Action = "Cancel",
            FromStatus = fromStatus.ToString(),
            ToStatus = order.Status.ToString(),
            Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator,
            Remark = request.Remark
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "inboundCancelled", orderNo }, cancellationToken);
        return Ok(new { order.OrderNo, order.Status });
    }
}
