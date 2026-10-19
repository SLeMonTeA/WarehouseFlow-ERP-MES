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
[Route("api/outbound")]
[Authorize(Policy = PermissionCodes.OutboundOperate)]
public sealed class OutboundController(WmsDbContext db, IHubContext<InventoryHub> hub, PartnerLookupService partners, DocumentNumberService numbers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await db.OutboundOrders.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.RequiredAt)
            .Select(x => new { x.OrderNo, x.CustomerCode, x.CustomerName, x.SourceDocumentNo, x.Status, x.RequiredAt, x.ShippedAt, lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.PickedQuantity, l.ShippedQuantity }) })
            .ToListAsync(cancellationToken));

    [Authorize(Policy = PermissionCodes.OutboundOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateOutboundRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return BadRequest(new { message = "出库单至少需要一条明细" });
        var orderNo = string.IsNullOrWhiteSpace(request.OrderNo) ? await numbers.NextAsync("OutboundOrder", cancellationToken) : request.OrderNo.Trim();
        if (await db.OutboundOrders.AnyAsync(x => x.OrderNo == orderNo, cancellationToken)) return Conflict(new { message = "出库单号已存在" });
        if (request.Lines.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return BadRequest(new { message = "同一出库单不能重复录入 SKU，请合并数量" });
        var skus = request.Lines.Select(x => x.Sku).Distinct().ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });
        if (request.Lines.Any(x => x.Quantity <= 0)) return BadRequest(new { message = "出库数量必须大于 0" });
        (string Code, string Name) customer;
        try { customer = await partners.ResolveCustomerAsync(request.CustomerCode, request.CustomerName, cancellationToken); }
        catch (KeyNotFoundException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }

        var order = new OutboundOrder { OrderNo = orderNo, CustomerCode = customer.Code, CustomerName = customer.Name, RequiredAt = request.RequiredAt, Status = OrderStatus.Confirmed };
        order.Lines.AddRange(request.Lines.Select(x => new OutboundLine { ProductId = products[x.Sku].Id, PlannedQuantity = x.Quantity }));
        db.OutboundOrders.Add(order);
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "OutboundOrder", DocumentNo = order.OrderNo, Action = "Create", FromStatus = "", ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system" });
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { orderNo = order.OrderNo }, new { order.OrderNo, order.CustomerCode, order.CustomerName, order.Status, order.RequiredAt, lines = order.Lines.Select(x => new { x.ProductId, x.PlannedQuantity, x.PickedQuantity, x.ShippedQuantity }) });
    }

    [Authorize(Policy = PermissionCodes.OutboundOperate)]
    [HttpPut("{orderNo}")]
    public async Task<IActionResult> Update(string orderNo, UpdateOutboundRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return BadRequest(new { message = "出库单至少需要一条明细" });
        if (request.Lines.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return BadRequest(new { message = "同一出库单不能重复录入 SKU，请合并数量" });
        if (request.Lines.Any(x => x.Quantity <= 0)) return BadRequest(new { message = "出库数量必须大于 0" });
        var order = await db.OutboundOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "出库单不存在" });
        if (!string.IsNullOrWhiteSpace(order.SourceDocumentNo) || order.Status != OrderStatus.Confirmed || order.Lines.Any(x => x.PickedQuantity > 0 || x.ShippedQuantity > 0)) return Conflict(new { message = "只有未预占的手工出库单可以编辑；下推单请回到来源单调整" });
        var customer = await partners.ResolveCustomerAsync(request.CustomerCode, request.CustomerName, cancellationToken);
        var skus = request.Lines.Select(x => x.Sku).Distinct().ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });
        db.OutboundLines.RemoveRange(order.Lines);
        order.CustomerCode = customer.Code; order.CustomerName = customer.Name; order.RequiredAt = request.RequiredAt;
        order.Lines = request.Lines.Select(x => new OutboundLine { ProductId = products[x.Sku].Id, PlannedQuantity = x.Quantity }).ToList();
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "OutboundOrder", DocumentNo = order.OrderNo, Action = "Update", FromStatus = order.Status.ToString(), ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system", Remark = "修改手工出库草稿" });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { order.OrderNo, order.CustomerCode, order.CustomerName, order.Status, order.RequiredAt });
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.OutboundOrders.AsNoTracking().Where(x => x.OrderNo == orderNo)
            .Select(x => new { x.OrderNo, x.CustomerCode, x.CustomerName, x.SourceDocumentNo, x.Status, x.RequiredAt, x.ShippedAt, lines = x.Lines.Select(l => new { l.Id, sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.PickedQuantity, l.ShippedQuantity, allocations = l.Allocations.Select(a => new { a.Quantity, a.ShippedQuantity, locationCode = a.InventoryBalance!.Location!.Code, batchNo = a.InventoryBalance.BatchNo, expiryDate = a.InventoryBalance.ExpiryDate }) }) })
            .SingleOrDefaultAsync(cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = PermissionCodes.OutboundOperate)]
    [HttpPost("{orderNo}/reserve")]
    public async Task<IActionResult> Reserve(string orderNo, ReserveOutboundRequest request, CancellationToken cancellationToken)
    {
        var order = await db.OutboundOrders.Include(x => x.Lines).ThenInclude(x => x.Product).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "出库单不存在" });
        if (order.Status is OrderStatus.Completed or OrderStatus.Cancelled) return Conflict(new { message = "当前出库单状态不允许预占" });
        if (request.Lines is not null && request.Lines.GroupBy(x => x.Sku).Any(x => x.Count() > 1)) return BadRequest(new { message = "部分预占明细不能重复 SKU" });
        var requested = request.Lines?.ToDictionary(x => x.Sku, x => x.Quantity);
        if (requested is not null && (requested.Values.Any(x => x <= 0) || requested.Keys.Any(x => order.Lines.All(l => l.Product!.Sku != x)))) return BadRequest(new { message = "部分预占明细必须使用本出库单 SKU，且数量必须大于 0" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in order.Lines)
        {
            if (line.PickedQuantity >= line.PlannedQuantity) continue;
            var remaining = line.PlannedQuantity - line.PickedQuantity;
            var required = requested is null ? remaining : requested.GetValueOrDefault(line.Product!.Sku);
            if (required <= 0) continue;
            if (required > remaining) return Conflict(new { message = $"SKU {line.Product!.Sku} 预占数量超过未预占数量" });
            var now = DateTimeOffset.UtcNow;
            var balances = await db.Inventory.Where(x => x.ProductId == line.ProductId && (x.ExpiryDate == null || x.ExpiryDate > now)).OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.LocationId).ToListAsync(cancellationToken);
            var available = balances.Sum(x => x.OnHand - x.Reserved - x.QualityHold);
            if (available < required) return Conflict(new { message = $"SKU {line.ProductId} 可用库存不足" });
            var remainingToReserve = required;
            foreach (var balance in balances)
            {
                if (remainingToReserve <= 0) break;
                var allocated = Math.Min(remainingToReserve, balance.OnHand - balance.Reserved - balance.QualityHold);
                if (allocated <= 0) continue;
                balance.Reserved += allocated;
                db.OutboundAllocations.Add(new OutboundAllocation { OutboundLineId = line.Id, InventoryBalanceId = balance.Id, Quantity = allocated });
                line.PickedQuantity += allocated;
                remainingToReserve -= allocated;
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = balance.LocationId, Type = InventoryMovementType.Reserve, Quantity = allocated, ReferenceNo = order.OrderNo, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator });
            }
        }
        order.Status = OrderStatus.InProgress;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "outboundReserved", orderNo }, cancellationToken);
        return Ok(new { order.OrderNo, order.Status, lines = order.Lines.Select(x => new { x.ProductId, x.PlannedQuantity, x.PickedQuantity }) });
    }

    [Authorize(Policy = PermissionCodes.OutboundOperate)]
    [HttpPost("{orderNo}/ship")]
    public async Task<IActionResult> Ship(string orderNo, ShipOutboundRequest request, CancellationToken cancellationToken)
    {
        var order = await db.OutboundOrders.Include(x => x.Lines).ThenInclude(x => x.Product).Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.InventoryBalance).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "出库单不存在" });
        if (order.Status == OrderStatus.Completed) return Conflict(new { message = "出库单已经发货" });
        if (request.Lines is not null && request.Lines.GroupBy(x => x.Sku).Any(x => x.Count() > 1)) return BadRequest(new { message = "部分发货明细不能重复 SKU" });
        var requested = request.Lines?.ToDictionary(x => x.Sku, x => x.Quantity);
        if (requested is not null && (requested.Values.Any(x => x <= 0) || requested.Keys.Any(x => order.Lines.All(l => l.Product!.Sku != x)))) return BadRequest(new { message = "部分发货明细必须使用本出库单 SKU，且数量必须大于 0" });
        var shipPlan = order.Lines.ToDictionary(x => x.Id, x => requested is null ? x.PickedQuantity - x.ShippedQuantity : requested.GetValueOrDefault(x.Product!.Sku));
        foreach (var line in order.Lines)
        {
            var available = line.PickedQuantity - line.ShippedQuantity;
            if (shipPlan[line.Id] > available) return Conflict(new { message = $"SKU {line.Product!.Sku} 发货数量超过已预占未发数量" });
        }
        if (shipPlan.Values.All(x => x <= 0)) return Conflict(new { message = "没有可发货的已预占数量" });

        var fromStatus = order.Status;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator;
        foreach (var line in order.Lines)
        {
            var remainingToShip = shipPlan[line.Id];
            if (remainingToShip <= 0) continue;
            foreach (var allocation in line.Allocations)
            {
                if (remainingToShip <= 0) break;
                var allocationRemaining = allocation.Quantity - allocation.ShippedQuantity;
                var shipped = Math.Min(remainingToShip, allocationRemaining);
                if (shipped <= 0) continue;
                var balance = allocation.InventoryBalance!;
                balance.OnHand -= shipped;
                balance.Reserved -= shipped;
                allocation.ShippedQuantity += shipped;
                line.ShippedQuantity += shipped;
                remainingToShip -= shipped;
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = balance.LocationId, Type = InventoryMovementType.Ship, Quantity = shipped, ReferenceNo = order.OrderNo, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, Operator = actor });
            }
        }
        if (order.SourceDeliveryNoticeId is not null)
        {
            var notice = await db.DeliveryNotices.Include(x => x.Lines).ThenInclude(x => x.SourceSalesOrderLine)
                .SingleOrDefaultAsync(x => x.Id == order.SourceDeliveryNoticeId, cancellationToken);
            if (notice is not null)
            {
                foreach (var line in order.Lines.Where(x => x.ShippedQuantity > 0))
                {
                    var noticeLine = notice.Lines.FirstOrDefault(x => x.ProductId == line.ProductId && x.PlannedQuantity > x.FulfilledQuantity);
                    if (noticeLine is null) continue;
                    var fulfilled = Math.Min(line.ShippedQuantity, noticeLine.PlannedQuantity - noticeLine.FulfilledQuantity);
                    noticeLine.FulfilledQuantity += fulfilled;
                    if (noticeLine.SourceSalesOrderLine is not null)
                        noticeLine.SourceSalesOrderLine.FulfilledQuantity += fulfilled;
                }
                notice.Status = notice.Lines.All(x => x.FulfilledQuantity >= x.PlannedQuantity) ? PlanningDocumentStatus.Completed : PlanningDocumentStatus.InProgress;
                if (notice.SourceSalesOrderId is not null)
                {
                    var sales = await db.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == notice.SourceSalesOrderId, cancellationToken);
                    if (sales is not null)
                    {
                        sales.Status = sales.Lines.All(x => x.FulfilledQuantity >= x.OrderedQuantity) ? PlanningDocumentStatus.Completed : PlanningDocumentStatus.InProgress;
                    }
                }
            }
        }
        if (order.Lines.All(x => x.ShippedQuantity >= x.PlannedQuantity))
        {
            order.Status = OrderStatus.Completed;
            order.ShippedAt = DateTimeOffset.UtcNow;
        }
        else order.Status = OrderStatus.InProgress;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "OutboundOrder", DocumentNo = order.OrderNo, Action = "Ship", FromStatus = fromStatus.ToString(), ToStatus = order.Status.ToString(), Operator = actor, Remark = request.Operator });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "outboundShipped", orderNo }, cancellationToken);
        return Ok(new { order.OrderNo, order.Status, order.ShippedAt });
    }

    [Authorize(Policy = PermissionCodes.OutboundOperate)]
    [HttpPost("{orderNo}/release")]
    public async Task<IActionResult> Release(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        var order = await db.OutboundOrders.Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.InventoryBalance)
            .SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "出库单不存在" });
        if (order.Lines.Any(x => x.ShippedQuantity > 0)) return Conflict(new { message = "已经发货的出库单不能释放预占" });
        if (order.Lines.SelectMany(x => x.Allocations).All(x => x.Quantity <= 0)) return Conflict(new { message = "当前出库单没有可释放的预占库存" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var fromStatus = order.Status;
        foreach (var line in order.Lines)
        {
            foreach (var allocation in line.Allocations)
            {
                if (allocation.Quantity <= 0) continue;
                allocation.InventoryBalance!.Reserved -= allocation.Quantity;
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = allocation.InventoryBalance.LocationId, Type = InventoryMovementType.Release, Quantity = -allocation.Quantity, ReferenceNo = order.OrderNo, BatchNo = allocation.InventoryBalance.BatchNo, ExpiryDate = allocation.InventoryBalance.ExpiryDate, Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator });
            }
            line.PickedQuantity = 0;
        }
        db.OutboundAllocations.RemoveRange(order.Lines.SelectMany(x => x.Allocations));
        order.Status = OrderStatus.Confirmed;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "OutboundOrder", DocumentNo = order.OrderNo, Action = "ReleaseReservation", FromStatus = fromStatus.ToString(), ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, Remark = request.Remark });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "outboundReservationReleased", orderNo }, cancellationToken);
        return Ok(new { order.OrderNo, order.Status });
    }

    [Authorize(Policy = PermissionCodes.OutboundOperate)]
    [HttpPost("{orderNo}/cancel")]
    public async Task<IActionResult> Cancel(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        var order = await db.OutboundOrders.Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.InventoryBalance)
            .SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "出库单不存在" });
        if (order.Status == OrderStatus.Completed || order.Lines.Any(x => x.ShippedQuantity > 0)) return Conflict(new { message = "已经发货的出库单不能取消" });
        if (order.Status == OrderStatus.Cancelled) return Conflict(new { message = "出库单已经取消" });

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in order.Lines)
        {
            foreach (var allocation in line.Allocations)
            {
                if (allocation.Quantity <= 0) continue;
                allocation.InventoryBalance!.Reserved -= allocation.Quantity;
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = allocation.InventoryBalance.LocationId, Type = InventoryMovementType.Release, Quantity = -allocation.Quantity, ReferenceNo = order.OrderNo, BatchNo = allocation.InventoryBalance.BatchNo, ExpiryDate = allocation.InventoryBalance.ExpiryDate, Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator });
            }
            line.PickedQuantity = 0;
        }
        db.OutboundAllocations.RemoveRange(order.Lines.SelectMany(x => x.Allocations));
        if (order.SourceDeliveryNoticeId is not null)
        {
            var notice = await db.DeliveryNotices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == order.SourceDeliveryNoticeId, cancellationToken);
            if (notice is not null)
            {
                foreach (var line in order.Lines)
                {
                    var noticeLine = notice.Lines.FirstOrDefault(x => x.ProductId == line.ProductId);
                    if (noticeLine is not null) noticeLine.PushedQuantity = Math.Max(noticeLine.FulfilledQuantity, noticeLine.PushedQuantity - line.PlannedQuantity);
                }
                notice.Status = notice.Lines.Any(x => x.PushedQuantity > x.FulfilledQuantity) ? PlanningDocumentStatus.InProgress : PlanningDocumentStatus.Approved;
            }
        }
        var fromStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "OutboundOrder", DocumentNo = order.OrderNo, Action = "Cancel", FromStatus = fromStatus.ToString(), ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, Remark = request.Remark });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "outboundCancelled", orderNo }, cancellationToken);
        return Ok(new { order.OrderNo, order.Status });
    }
}
