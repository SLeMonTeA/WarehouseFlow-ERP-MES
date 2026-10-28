using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Application;
using WarehouseFlow.Api.Application.Documents;
using WarehouseFlow.Api.Application.Planning;
using WarehouseFlow.Api.Application.Partners;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/bom")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class BomController(WmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.BomHeaders.AsNoTracking().Include(x => x.Product).Include(x => x.Lines).ThenInclude(x => x.ComponentProduct)
        .OrderByDescending(x => x.EffectiveFrom).Select(x => new { id = x.Id, x.Product!.Sku, productName = x.Product.Name, x.Version, x.EffectiveFrom, x.IsActive, lines = x.Lines.Select(l => new { componentSku = l.ComponentProduct!.Sku, componentName = l.ComponentProduct.Name, l.Quantity, l.ScrapRate }) }).ToListAsync(cancellationToken));

    [HttpGet("{productSku}")]
    public async Task<IActionResult> Get(string productSku, CancellationToken cancellationToken)
    {
        var bom = await db.BomHeaders.AsNoTracking().Include(x => x.Product).Include(x => x.Lines).ThenInclude(x => x.ComponentProduct)
            .Where(x => x.Product!.Sku == productSku && x.IsActive).OrderByDescending(x => x.EffectiveFrom).Select(x => new { x.Product!.Sku, productName = x.Product.Name, x.Version, x.EffectiveFrom, lines = x.Lines.Select(l => new { componentSku = l.ComponentProduct!.Sku, componentName = l.ComponentProduct.Name, l.Quantity, l.ScrapRate }) }).FirstOrDefaultAsync(cancellationToken);
        return bom is null ? NotFound() : Ok(bom);
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateBomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version) || request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0 || x.ScrapRate < 0 || x.ScrapRate > 1)) return BadRequest(new { message = "BOM 版本或明细数量、损耗率无效" });
        if (request.Lines.GroupBy(x => x.ComponentSku).Any(x => x.Count() > 1)) return BadRequest(new { message = "同一 BOM 中不能重复维护相同子件 SKU" });
        var parent = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.ProductSku && x.IsActive, cancellationToken);
        var skus = request.Lines.Select(x => x.ComponentSku).Distinct().ToList();
        var components = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (parent is null || components.Count != skus.Count) return BadRequest(new { message = "成品或子件 SKU 不存在" });
        if (components.Values.Any(x => x.Id == parent.Id)) return BadRequest(new { message = "BOM 不能直接引用自身" });
        if (await db.BomHeaders.AnyAsync(x => x.ProductId == parent.Id && x.Version == request.Version, cancellationToken)) return Conflict(new { message = "BOM 版本已存在" });
        var activeVersions = await db.BomHeaders.Where(x => x.ProductId == parent.Id && x.IsActive).ToListAsync(cancellationToken);
        foreach (var activeVersion in activeVersions) activeVersion.IsActive = false;
        var bom = new BomHeader { ProductId = parent.Id, Version = request.Version, EffectiveFrom = request.EffectiveFrom };
        bom.Lines.AddRange(request.Lines.Select(x => new BomLine { ComponentProductId = components[x.ComponentSku].Id, Quantity = x.Quantity, ScrapRate = x.ScrapRate }));
        db.BomHeaders.Add(bom);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { productSku = request.ProductSku }, new { request.ProductSku, request.Version });
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateBomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version) || request.Lines.Count == 0 || request.Lines.Any(x => string.IsNullOrWhiteSpace(x.ComponentSku) || x.Quantity <= 0 || x.ScrapRate < 0 || x.ScrapRate > 1))
            return BadRequest(new { message = "BOM 版本或明细数量、损耗率无效" });
        if (request.Lines.GroupBy(x => x.ComponentSku, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            return BadRequest(new { message = "同一 BOM 中不能重复维护相同子件 SKU" });

        var bom = await db.BomHeaders.Include(x => x.Product).Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (bom is null) return NotFound(new { message = "BOM 不存在" });

        if (await db.BomHeaders.AnyAsync(x => x.ProductId == bom.ProductId && x.Version == request.Version && x.Id != id, cancellationToken))
            return Conflict(new { message = "BOM 版本已存在" });

        var skus = request.Lines.Select(x => x.ComponentSku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var components = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken);
        if (components.Count != skus.Count) return BadRequest(new { message = "存在不存在或已停用的子件 SKU" });
        if (components.Values.Any(x => x.Id == bom.ProductId)) return BadRequest(new { message = "BOM 不能直接引用自身" });

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "system";
        var oldStatus = bom.IsActive ? "Active" : "Inactive";
        db.BomLines.RemoveRange(bom.Lines);
        bom.Lines.Clear();
        bom.Version = request.Version.Trim();
        bom.EffectiveFrom = request.EffectiveFrom;
        bom.IsActive = request.IsActive;
        bom.Lines.AddRange(request.Lines.Select(x => new BomLine { ComponentProductId = components[x.ComponentSku].Id, Quantity = x.Quantity, ScrapRate = x.ScrapRate }));
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "Bom", DocumentNo = id.ToString(), Action = "Update", FromStatus = oldStatus, ToStatus = bom.IsActive ? "Active" : "Inactive", Operator = actor, Remark = $"更新 BOM {bom.Version}" });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { id = bom.Id, productSku = bom.Product!.Sku, bom.Version, bom.EffectiveFrom, bom.IsActive });
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var bom = await db.BomHeaders.Include(x => x.Product).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (bom is null) return NotFound(new { message = "BOM 不存在" });
        if (!bom.IsActive) return Ok(new { id, isActive = false, message = "BOM 已经停用" });
        bom.IsActive = false;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "Bom", DocumentNo = id.ToString(), Action = "Deactivate", FromStatus = "Active", ToStatus = "Inactive", Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system", Remark = $"停用 BOM {bom.Product!.Sku}/{bom.Version}" });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { id, isActive = false, message = "BOM 已停用，历史单据仍保留" });
    }
}

[ApiController]
[Route("api/sales-orders")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class SalesOrdersController(WmsDbContext db, DocumentFlowService flow, DocumentWorkflowService workflow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.SalesOrders.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.RequiredAt).Select(x => new { x.OrderNo, x.CustomerCode, x.CustomerName, x.Status, x.OrderDate, x.RequiredAt, orderedQuantity = x.Lines.Sum(l => l.OrderedQuantity), fulfilledQuantity = x.Lines.Sum(l => l.FulfilledQuantity), openQuantity = x.Lines.Sum(l => l.OrderedQuantity - l.FulfilledQuantity), lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.OrderedQuantity, l.PushedQuantity, l.FulfilledQuantity, openQuantity = l.OrderedQuantity - l.FulfilledQuantity }) }).ToListAsync(cancellationToken));

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        try { var order = await flow.CreateSalesOrderAsync(request, cancellationToken); db.DocumentAudits.Add(new DocumentAudit { DocumentType = "SalesOrder", DocumentNo = order.OrderNo, Action = "Create", FromStatus = "", ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system" }); await db.SaveChangesAsync(cancellationToken); return CreatedAtAction(nameof(Get), new { orderNo = order.OrderNo }, new { order.OrderNo, order.Status }); }
        catch (KeyNotFoundException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPut("{orderNo}")]
    public async Task<IActionResult> Update(string orderNo, UpdateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        try { var order = await flow.UpdateSalesOrderAsync(orderNo, request, cancellationToken); db.DocumentAudits.Add(new DocumentAudit { DocumentType = "SalesOrder", DocumentNo = order.OrderNo, Action = "Update", FromStatus = order.Status.ToString(), ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system", Remark = "修改草稿单据" }); await db.SaveChangesAsync(cancellationToken); return Ok(new { order.OrderNo, order.Status }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).Where(x => x.OrderNo == orderNo).Select(x => new { x.OrderNo, x.CustomerCode, x.CustomerName, x.Status, x.RequiredAt, orderedQuantity = x.Lines.Sum(l => l.OrderedQuantity), fulfilledQuantity = x.Lines.Sum(l => l.FulfilledQuantity), openQuantity = x.Lines.Sum(l => l.OrderedQuantity - l.FulfilledQuantity), lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.OrderedQuantity, l.PushedQuantity, l.FulfilledQuantity, openQuantity = l.OrderedQuantity - l.FulfilledQuantity }) }).SingleOrDefaultAsync(cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/submit")]
    public Task<IActionResult> Submit(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Submitted, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/approve")]
    public Task<IActionResult> Approve(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Approved, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/reverse-approve")]
    public Task<IActionResult> ReverseApprove(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Draft, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/cancel")]
    public Task<IActionResult> Cancel(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Cancelled, request, cancellationToken);

    private async Task<IActionResult> ChangeStatus(string orderNo, PlanningDocumentStatus status, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(new { orderNo, status = await workflow.ChangeSalesOrderStatusAsync(orderNo, status, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/push-delivery")]
    public async Task<IActionResult> PushDelivery(string orderNo, CancellationToken cancellationToken)
    {
        try
        {
            var notice = await flow.PushSalesOrderToDeliveryAsync(orderNo, cancellationToken);
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "system";
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "SalesOrder", DocumentNo = orderNo, Action = "PushDeliveryNotice", FromStatus = PlanningDocumentStatus.Approved.ToString(), ToStatus = PlanningDocumentStatus.InProgress.ToString(), Operator = actor, Remark = $"下推发货通知单 {notice.NoticeNo}" });
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "DeliveryNotice", DocumentNo = notice.NoticeNo, Action = "CreateFromSalesOrder", FromStatus = "", ToStatus = notice.Status.ToString(), Operator = actor, Remark = $"来源销售订单 {orderNo}" });
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { notice.NoticeNo, notice.SourceDocumentNo, notice.Status });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

[ApiController]
[Route("api/purchase-orders")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class PurchaseOrdersController(WmsDbContext db, DocumentFlowService flow, DocumentWorkflowService workflow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.RequiredAt)
        .Select(x => new { x.OrderNo, x.SupplierCode, x.SupplierName, x.Status, x.OrderDate, x.RequiredAt, orderedQuantity = x.Lines.Sum(l => l.OrderedQuantity), receivedQuantity = x.Lines.Sum(l => l.ReceivedQuantity), openQuantity = x.Lines.Sum(l => l.OrderedQuantity - l.ReceivedQuantity), lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.OrderedQuantity, l.PushedQuantity, l.ReceivedQuantity, openQuantity = l.OrderedQuantity - l.ReceivedQuantity }) })
        .ToListAsync(cancellationToken));

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var order = await flow.CreatePurchaseOrderAsync(request, cancellationToken);
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "PurchaseOrder", DocumentNo = order.OrderNo, Action = "Create", FromStatus = "", ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system" });
            await db.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(Get), new { orderNo = order.OrderNo }, new { order.OrderNo, order.Status });
        }
        catch (KeyNotFoundException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPut("{orderNo}")]
    public async Task<IActionResult> Update(string orderNo, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var order = await flow.UpdatePurchaseOrderAsync(orderNo, request, cancellationToken);
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "PurchaseOrder", DocumentNo = order.OrderNo, Action = "Update", FromStatus = order.Status.ToString(), ToStatus = order.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system", Remark = "修改草稿单据" });
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { order.OrderNo, order.Status });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.AsNoTracking().Where(x => x.OrderNo == orderNo)
            .Select(x => new { x.OrderNo, x.SupplierCode, x.SupplierName, x.Status, x.OrderDate, x.RequiredAt, orderedQuantity = x.Lines.Sum(l => l.OrderedQuantity), receivedQuantity = x.Lines.Sum(l => l.ReceivedQuantity), openQuantity = x.Lines.Sum(l => l.OrderedQuantity - l.ReceivedQuantity), lines = x.Lines.Select(l => new { l.Id, sku = l.Product!.Sku, productName = l.Product.Name, l.OrderedQuantity, l.PushedQuantity, l.ReceivedQuantity, openQuantity = l.OrderedQuantity - l.ReceivedQuantity }) })
            .SingleOrDefaultAsync(cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/submit")]
    public Task<IActionResult> Submit(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Submitted, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/approve")]
    public Task<IActionResult> Approve(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Approved, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/reverse-approve")]
    public Task<IActionResult> ReverseApprove(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Draft, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/cancel")]
    public Task<IActionResult> Cancel(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Cancelled, request, cancellationToken);

    private async Task<IActionResult> ChangeStatus(string orderNo, PlanningDocumentStatus status, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(new { orderNo, status = await workflow.ChangePurchaseOrderStatusAsync(orderNo, status, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/push-receipt")]
    public async Task<IActionResult> PushReceipt(string orderNo, CancellationToken cancellationToken)
    {
        try
        {
            var notice = await flow.PushPurchaseOrderToReceiptNoticeAsync(orderNo, cancellationToken);
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "system";
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "PurchaseOrder", DocumentNo = orderNo, Action = "PushReceiptNotice", FromStatus = PlanningDocumentStatus.Approved.ToString(), ToStatus = PlanningDocumentStatus.InProgress.ToString(), Operator = actor, Remark = $"下推收货通知单 {notice.NoticeNo}" });
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "ReceiptNotice", DocumentNo = notice.NoticeNo, Action = "CreateFromPurchaseOrder", FromStatus = "", ToStatus = notice.Status.ToString(), Operator = actor, Remark = $"来源采购订单 {orderNo}" });
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { notice.NoticeNo, notice.SourceDocumentNo, notice.Status });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

[ApiController]
[Route("api/delivery-notices")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class DeliveryNoticesController(WmsDbContext db, DocumentWorkflowService workflow, Microsoft.AspNetCore.SignalR.IHubContext<WarehouseFlow.Api.Realtime.InventoryHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.DeliveryNotices.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.ExpectedShipAt).Select(x => new { x.NoticeNo, x.SourceDocumentNo, x.CustomerCode, x.CustomerName, x.Status, x.ExpectedShipAt, plannedQuantity = x.Lines.Sum(l => l.PlannedQuantity), fulfilledQuantity = x.Lines.Sum(l => l.FulfilledQuantity), openQuantity = x.Lines.Sum(l => l.PlannedQuantity - l.FulfilledQuantity), lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.PushedQuantity, l.FulfilledQuantity }) }).ToListAsync(cancellationToken));

    [HttpGet("{noticeNo}")]
    public async Task<IActionResult> Get(string noticeNo, CancellationToken cancellationToken)
    {
        var notice = await db.DeliveryNotices.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).Where(x => x.NoticeNo == noticeNo)
            .Select(x => new { x.NoticeNo, x.SourceDocumentNo, x.CustomerCode, x.CustomerName, x.Status, x.ExpectedShipAt, lines = x.Lines.Select(l => new { l.Id, sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.PushedQuantity, l.FulfilledQuantity }) }).SingleOrDefaultAsync(cancellationToken);
        return notice is null ? NotFound() : Ok(notice);
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/submit")]
    public Task<IActionResult> Submit(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(noticeNo, PlanningDocumentStatus.Submitted, request, cancellationToken);

    /// <summary>审核发货通知并按效期优先规则直接扣减可用库存。</summary>
    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/approve")]
    public async Task<IActionResult> Approve(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        var notice = await db.DeliveryNotices.Include(x => x.Lines).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.NoticeNo == noticeNo, cancellationToken);
        if (notice is null) return NotFound(new { message = "发货通知单不存在" });
        if (notice.Status != PlanningDocumentStatus.Submitted) return Conflict(new { message = "只有已提交的发货通知单可以审核并发货" });
        if (notice.Lines.Count == 0 || notice.Lines.Any(x => x.PlannedQuantity <= x.FulfilledQuantity)) return Conflict(new { message = "发货通知单没有待发数量" });

        var now = DateTimeOffset.UtcNow;
        var balances = await db.Inventory.Where(x => x.ExpiryDate == null || x.ExpiryDate > now)
            .OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        foreach (var line in notice.Lines)
        {
            var remaining = line.PlannedQuantity - line.FulfilledQuantity;
            var eligible = balances.Where(x => x.ProductId == line.ProductId && x.OnHand - x.Reserved - x.QualityHold > 0).ToList();
            if (eligible.Sum(x => x.OnHand - x.Reserved - x.QualityHold) < remaining)
                return Conflict(new { message = $"物料 {line.Product!.Sku} 可用库存不足，审核未过账" });
        }

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in notice.Lines)
        {
            var remaining = line.PlannedQuantity - line.FulfilledQuantity;
            foreach (var balance in balances.Where(x => x.ProductId == line.ProductId && x.OnHand - x.Reserved - x.QualityHold > 0))
            {
                if (remaining <= 0) break;
                var quantity = Math.Min(remaining, balance.OnHand - balance.Reserved - balance.QualityHold);
                if (quantity <= 0) continue;
                balance.OnHand -= quantity;
                line.FulfilledQuantity += quantity;
                remaining -= quantity;
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = balance.LocationId, Type = InventoryMovementType.Ship, Quantity = -quantity, ReferenceNo = noticeNo, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, Operator = actor });
            }
            if (line.SourceSalesOrderLine is null && line.SourceSalesOrderLineId is not null)
                line.SourceSalesOrderLine = await db.SalesOrderLines.SingleOrDefaultAsync(x => x.Id == line.SourceSalesOrderLineId, cancellationToken);
            if (line.SourceSalesOrderLine is not null) line.SourceSalesOrderLine.FulfilledQuantity += line.FulfilledQuantity;
        }
        notice.Status = PlanningDocumentStatus.Completed;
        if (notice.SourceSalesOrderId is int salesId)
        {
            var sales = await db.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == salesId, cancellationToken);
            if (sales is not null) sales.Status = sales.Lines.All(x => x.FulfilledQuantity >= x.OrderedQuantity) ? PlanningDocumentStatus.Completed : PlanningDocumentStatus.InProgress;
        }
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "DeliveryNotice", DocumentNo = noticeNo, Action = "ApproveAndShip", FromStatus = PlanningDocumentStatus.Submitted.ToString(), ToStatus = notice.Status.ToString(), Operator = actor, Remark = request.Remark });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "deliveryNoticeShipped", noticeNo }, cancellationToken);
        return Ok(new { noticeNo, status = notice.Status, shipped = true });
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/reverse-approve")]
    public Task<IActionResult> ReverseApprove(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(noticeNo, PlanningDocumentStatus.Draft, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/cancel")]
    public Task<IActionResult> Cancel(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(noticeNo, PlanningDocumentStatus.Cancelled, request, cancellationToken);

    private async Task<IActionResult> ChangeStatus(string noticeNo, PlanningDocumentStatus status, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(new { noticeNo, status = await workflow.ChangeDeliveryNoticeStatusAsync(noticeNo, status, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>兼容旧客户端的停用端点；发货通知审核已直接完成出库过账。</summary>
    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/push-outbound")]
    public IActionResult PushOutbound(string noticeNo) => Conflict(new { message = "发货通知审核会直接扣减库存，不再下推独立出库单" });
}

[ApiController]
[Route("api/receipt-notices")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class ReceiptNoticesController(WmsDbContext db, DocumentFlowService flow, DocumentWorkflowService workflow, PartnerLookupService partners, DocumentNumberService numbers, Microsoft.AspNetCore.SignalR.IHubContext<WarehouseFlow.Api.Realtime.InventoryHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.ReceiptNotices.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).OrderByDescending(x => x.ExpectedAt).Select(x => new { x.NoticeNo, x.SourceDocumentNo, x.SupplierCode, x.SupplierName, x.Status, x.ExpectedAt, plannedQuantity = x.Lines.Sum(l => l.PlannedQuantity), receivedQuantity = x.Lines.Sum(l => l.ReceivedQuantity), openQuantity = x.Lines.Sum(l => l.PlannedQuantity - l.ReceivedQuantity), lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.ReceivedQuantity }) }).ToListAsync(cancellationToken));

    [HttpGet("{noticeNo}")]
    public async Task<IActionResult> Get(string noticeNo, CancellationToken cancellationToken)
    {
        var notice = await db.ReceiptNotices.AsNoTracking().Include(x => x.Lines).ThenInclude(x => x.Product).Where(x => x.NoticeNo == noticeNo)
            .Select(x => new { x.NoticeNo, x.SourceDocumentNo, x.SupplierCode, x.SupplierName, x.Status, x.ExpectedAt, lines = x.Lines.Select(l => new { l.Id, sku = l.Product!.Sku, productName = l.Product.Name, l.PlannedQuantity, l.ReceivedQuantity, openQuantity = l.PlannedQuantity - l.ReceivedQuantity }) }).SingleOrDefaultAsync(cancellationToken);
        return notice is null ? NotFound() : Ok(notice);
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateReceiptNoticeRequest request, CancellationToken cancellationToken)
    {
        try { var notice = await flow.CreateReceiptNoticeAsync(request, cancellationToken); db.DocumentAudits.Add(new DocumentAudit { DocumentType = "ReceiptNotice", DocumentNo = notice.NoticeNo, Action = "Create", FromStatus = "", ToStatus = notice.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system" }); await db.SaveChangesAsync(cancellationToken); return CreatedAtAction(nameof(List), null, new { notice.NoticeNo, notice.Status }); }
        catch (KeyNotFoundException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPut("{noticeNo}")]
    public async Task<IActionResult> Update(string noticeNo, UpdateReceiptNoticeRequest request, CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return BadRequest(new { message = "收货通知单至少需要一条明细" });
        if (request.Lines.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return BadRequest(new { message = "同一收货通知单不能重复录入 SKU，请合并数量" });
        if (request.Lines.Any(x => x.Quantity <= 0)) return BadRequest(new { message = "收货通知数量必须大于 0" });
        var notice = await db.ReceiptNotices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.NoticeNo == noticeNo, cancellationToken);
        if (notice is null) return NotFound(new { message = "收货通知单不存在" });
        if (!string.Equals(notice.SourceDocumentNo, "手工建单", StringComparison.OrdinalIgnoreCase) || notice.Status != PlanningDocumentStatus.Draft || notice.Lines.Any(x => x.ReceivedQuantity > 0)) return Conflict(new { message = "只有未执行的手工收货通知草稿可以编辑；下推单请回到采购订单调整" });
        var supplier = await partners.ResolveSupplierAsync(request.SupplierCode, request.SupplierName, cancellationToken);
        var skus = request.Lines.Select(x => x.Sku).Distinct().ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) return BadRequest(new { message = "存在无效或停用的 SKU" });
        db.ReceiptNoticeLines.RemoveRange(notice.Lines);
        notice.SupplierCode = supplier.Code; notice.SupplierName = supplier.Name; notice.ExpectedAt = request.ExpectedAt;
        notice.Lines = request.Lines.Select(x => new ReceiptNoticeLine { ProductId = products[x.Sku].Id, PlannedQuantity = x.Quantity }).ToList();
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "ReceiptNotice", DocumentNo = notice.NoticeNo, Action = "Update", FromStatus = notice.Status.ToString(), ToStatus = notice.Status.ToString(), Operator = User.FindFirstValue(ClaimTypes.Name) ?? "system", Remark = "修改手工收货通知草稿" });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { notice.NoticeNo, notice.SupplierCode, notice.SupplierName, notice.Status, notice.ExpectedAt });
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/submit")]
    public Task<IActionResult> Submit(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(noticeNo, PlanningDocumentStatus.Submitted, request, cancellationToken);

    /// <summary>审核收货通知并直接过账库存；需要质检的物料自动生成质检工单并隔离库存。</summary>
    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/approve")]
    public async Task<IActionResult> Approve(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        var notice = await db.ReceiptNotices.Include(x => x.Lines).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.NoticeNo == noticeNo, cancellationToken);
        if (notice is null) return NotFound(new { message = "收货通知单不存在" });
        if (notice.Status != PlanningDocumentStatus.Submitted) return Conflict(new { message = "只有已提交的收货通知单可以审核并收货" });
        if (notice.Lines.Count == 0 || notice.Lines.Any(x => x.PlannedQuantity <= x.ReceivedQuantity)) return Conflict(new { message = "收货通知单没有待收数量" });
        var location = await db.Locations.Where(x => x.IsActive).OrderByDescending(x => x.Zone.Contains("原料")).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (location is null) return Conflict(new { message = "没有启用的收货库位，请先维护库位" });

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator;
        var batchNo = notice.NoticeNo;
        var inspections = notice.Lines.Where(x => x.Product!.RequiresQuality && x.PlannedQuantity > x.ReceivedQuantity).ToList();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var locationOnHand = await db.Inventory.Where(x => x.LocationId == location.Id).SumAsync(x => x.OnHand, cancellationToken);
        foreach (var line in notice.Lines)
        {
            var quantity = line.PlannedQuantity - line.ReceivedQuantity;
            if (locationOnHand + quantity > location.Capacity) return Conflict(new { message = $"库位 {location.Code} 容量不足，未执行收货" });
            var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == line.ProductId && x.LocationId == location.Id && x.BatchNo == batchNo, cancellationToken);
            if (balance is null)
            {
                balance = new InventoryBalance { ProductId = line.ProductId, LocationId = location.Id, BatchNo = batchNo };
                db.Inventory.Add(balance);
            }
            balance.OnHand += quantity;
            if (line.Product!.RequiresQuality) balance.QualityHold += quantity;
            locationOnHand += quantity;
            line.ReceivedQuantity += quantity;
            db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = location.Id, Type = InventoryMovementType.Receive, Quantity = quantity, ReferenceNo = noticeNo, BatchNo = batchNo, Operator = actor });
            if (line.Product.RequiresQuality)
                db.Movements.Add(new InventoryMovement { ProductId = line.ProductId, LocationId = location.Id, Type = InventoryMovementType.QualityHold, Quantity = quantity, ReferenceNo = noticeNo, BatchNo = batchNo, Operator = actor });
        }
        notice.Status = PlanningDocumentStatus.Completed;
        QualityInspection? inspection = null;
        if (inspections.Count > 0)
        {
            inspection = new QualityInspection { InspectionNo = await numbers.NextAsync("QualityInspection", cancellationToken), SourceInboundOrderNo = noticeNo, SupplierName = notice.SupplierName, Inspector = actor, Remark = $"收货通知单 {noticeNo} 自动生成质检工单" };
            inspection.Lines.AddRange(inspections.Select(x => new QualityInspectionLine { ProductId = x.ProductId, ReceivedQuantity = x.PlannedQuantity }));
            db.QualityInspections.Add(inspection);
            db.DocumentRelations.Add(new DocumentRelation { SourceType = "ReceiptNotice", SourceNo = noticeNo, TargetType = "QualityInspection", TargetNo = inspection.InspectionNo, Quantity = inspection.Lines.Sum(x => x.ReceivedQuantity) });
        }
        if (!string.IsNullOrWhiteSpace(notice.SourceDocumentNo) && notice.SourceDocumentNo != "手工建单")
        {
            var purchase = await db.PurchaseOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == notice.SourceDocumentNo, cancellationToken);
            if (purchase is not null)
            {
                foreach (var line in notice.Lines)
                {
                    var remaining = line.PlannedQuantity;
                    foreach (var source in purchase.Lines.Where(x => x.ProductId == line.ProductId && x.ReceivedQuantity < x.OrderedQuantity))
                    {
                        var quantity = Math.Min(remaining, source.OrderedQuantity - source.ReceivedQuantity);
                        source.ReceivedQuantity += quantity;
                        remaining -= quantity;
                        if (remaining <= 0) break;
                    }
                }
                purchase.Status = purchase.Lines.All(x => x.ReceivedQuantity >= x.OrderedQuantity) ? PlanningDocumentStatus.Completed : PlanningDocumentStatus.InProgress;
            }
        }
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "ReceiptNotice", DocumentNo = noticeNo, Action = "ApproveAndReceive", FromStatus = PlanningDocumentStatus.Submitted.ToString(), ToStatus = notice.Status.ToString(), Operator = actor, Remark = request.Remark });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "receiptNoticeReceived", noticeNo }, cancellationToken);
        return Ok(new { noticeNo, status = notice.Status, locationCode = location.Code, qualityInspectionNo = inspection?.InspectionNo, qualityRequiredCount = inspections.Count });
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/reverse-approve")]
    public Task<IActionResult> ReverseApprove(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(noticeNo, PlanningDocumentStatus.Draft, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/cancel")]
    public Task<IActionResult> Cancel(string noticeNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(noticeNo, PlanningDocumentStatus.Cancelled, request, cancellationToken);

    private async Task<IActionResult> ChangeStatus(string noticeNo, PlanningDocumentStatus status, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(new { noticeNo, status = await workflow.ChangeReceiptNoticeStatusAsync(noticeNo, status, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>兼容旧客户端的停用端点；收货通知审核已直接完成入库过账。</summary>
    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{noticeNo}/push-inbound")]
    public IActionResult PushInbound(string noticeNo) => Conflict(new { message = "收货通知审核会直接入库，不再下推独立入库单" });
}

[ApiController]
[Route("api/production-orders")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class ProductionOrdersController(WmsDbContext db, DocumentFlowService flow, DocumentWorkflowService workflow) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.ProductionOrders.AsNoTracking()
        .Include(x => x.Product)
        .Include(x => x.Materials).ThenInclude(x => x.ComponentProduct)
        .Include(x => x.Operations)
        .OrderByDescending(x => x.RequiredAt)
        .Select(x => new
        {
            x.OrderNo,
            productSku = x.Product!.Sku,
            productName = x.Product.Name,
            x.Status,
            x.QualityStatus,
            x.QualityRemark,
            x.PlannedQuantity,
            x.CompletedQuantity,
            x.ReportedGoodQuantity,
            x.ReportedDefectQuantity,
            x.RequiredAt,
            x.RoutingId,
            x.RoutingCode,
            x.RoutingVersion,
            materials = x.Materials.Select(m => new { componentSku = m.ComponentProduct!.Sku, componentName = m.ComponentProduct.Name, m.RequiredQuantity, m.IssuedQuantity }),
            operations = x.Operations.OrderBy(o => o.Sequence).Select(o => new { o.Sequence, o.OperationCode, o.OperationName, o.WorkCenter, o.StandardMinutes, o.RequiresQuality, o.ReportedGoodQuantity, o.ReportedDefectQuantity, o.Status })
        }).ToListAsync(cancellationToken));

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.SourceMrpSuggestion).ThenInclude(x => x!.MrpRun)
            .Include(x => x.Materials).ThenInclude(x => x.ComponentProduct)
            .Include(x => x.Operations)
            .Include(x => x.Receipts)
            .Where(x => x.OrderNo == orderNo)
            .Select(x => new
            {
                x.OrderNo,
                productSku = x.Product!.Sku,
                productName = x.Product.Name,
                x.Status,
                x.PlannedQuantity,
                x.CompletedQuantity,
                x.ReportedGoodQuantity,
                x.ReportedDefectQuantity,
                x.QualityStatus,
                x.QualityRemark,
                x.RoutingId,
                x.RoutingCode,
                x.RoutingVersion,
                openQuantity = Math.Max(0, x.PlannedQuantity - x.CompletedQuantity),
                x.RequiredAt,
                sourceMrpSuggestionId = x.SourceMrpSuggestionId,
                sourceMrpRunNo = x.SourceMrpSuggestion!.MrpRun!.RunNo,
                sourceOrderNos = x.SourceMrpSuggestion.SourceOrderNos,
                materials = x.Materials.Select(m => new { componentSku = m.ComponentProduct!.Sku, componentName = m.ComponentProduct.Name, m.RequiredQuantity, m.IssuedQuantity, openQuantity = Math.Max(0, m.RequiredQuantity - m.IssuedQuantity) }),
                operations = x.Operations.OrderBy(o => o.Sequence).Select(o => new { o.Sequence, o.OperationCode, o.OperationName, o.WorkCenter, o.StandardMinutes, o.RequiresQuality, o.ReportedGoodQuantity, o.ReportedDefectQuantity, o.Status }),
                receipts = x.Receipts.OrderByDescending(r => r.ReceivedAt).Select(r => new { r.ReceiptNo, r.LocationCode, r.Quantity, r.ReceivedAt })
            })
            .SingleOrDefaultAsync(cancellationToken);
        return order is null ? NotFound(new { message = "生产订单不存在" }) : Ok(order);
    }

    [HttpPut("{orderNo}")]
    public async Task<IActionResult> Update(string orderNo, UpdateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) return BadRequest(new { message = "计划数量必须大于 0" });
        var order = await db.ProductionOrders.Include(x => x.Materials).Include(x => x.Operations).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "生产订单不存在" });
        if (order.Status is not (PlanningDocumentStatus.Draft or PlanningDocumentStatus.Approved))
            return Conflict(new { message = "生产订单只有草稿或已审核状态允许修改" });
        if (order.CompletedQuantity > 0 || order.Materials.Any(x => x.IssuedQuantity > 0))
            return Conflict(new { message = "生产订单已入库或已领料，不能直接修改；请走变更单或先处理下游业务" });

        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.ProductSku && x.IsActive, cancellationToken);
        if (product is null) return NotFound(new { message = "成品 SKU 不存在" });
        var bom = await db.BomHeaders.Include(x => x.Lines)
            .Where(x => x.ProductId == product.Id && x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (bom is null) return Conflict(new { message = "成品没有有效 BOM，不能修改生产订单" });

        var routingQuery = db.RoutingHeaders.Include(x => x.Operations)
            .Where(x => x.ProductId == product.Id && x.IsActive && x.Operations.Any());
        var routing = request.RoutingId is not null
            ? await routingQuery.SingleOrDefaultAsync(x => x.Id == request.RoutingId.Value, cancellationToken)
            : await routingQuery.Where(x => x.IsDefault && x.EffectiveFrom <= request.RequiredAt.Date)
                .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (routing is null) return Conflict(new { message = "成品没有有效的默认工艺路线，请先维护工艺路线" });

        db.ProductionOrderMaterials.RemoveRange(order.Materials);
        db.ProductionOrderOperations.RemoveRange(order.Operations);
        order.ProductId = product.Id;
        order.RoutingId = routing.Id;
        order.RoutingCode = routing.Code;
        order.RoutingVersion = routing.Version;
        order.PlannedQuantity = request.Quantity;
        order.RequiredAt = request.RequiredAt;
        db.ProductionOrderMaterials.AddRange(bom.Lines.Select(x => new ProductionOrderMaterial
        {
            ProductionOrderId = order.Id,
            ComponentProductId = x.ComponentProductId,
            RequiredQuantity = decimal.Round(request.Quantity * x.Quantity * (1 + x.ScrapRate), 3)
        }));
        db.ProductionOrderOperations.AddRange(routing.Operations.OrderBy(x => x.Sequence).Select(x => new ProductionOrderOperation
        {
            ProductionOrderId = order.Id,
            Sequence = x.Sequence,
            OperationCode = x.OperationCode,
            OperationName = x.OperationName,
            WorkCenter = x.WorkCenter,
            StandardMinutes = x.StandardMinutes,
            RequiresQuality = x.RequiresQuality
        }));
        db.IntegrationOutboxEvents.Add(new IntegrationOutboxEvent
        {
            EventKey = $"ERP-PRODUCTION-{Guid.NewGuid():N}",
            EventType = "ProductionOrderChanged",
            AggregateType = "ProductionOrder",
            AggregateNo = orderNo
        });
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = "ProductionOrder",
            DocumentNo = orderNo,
            Action = "Update",
            FromStatus = order.Status.ToString(),
            ToStatus = order.Status.ToString(),
            Operator = request.Operator,
            Remark = request.Remark
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { orderNo, order.ProductId, order.PlannedQuantity, order.RequiredAt, order.Status });
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/submit")]
    public Task<IActionResult> Submit(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Submitted, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/approve")]
    public Task<IActionResult> Approve(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Approved, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/release")]
    public Task<IActionResult> Release(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Released, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/reverse-approve")]
    public Task<IActionResult> ReverseApprove(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Draft, request, cancellationToken);

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/cancel")]
    public Task<IActionResult> Cancel(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken) => ChangeStatus(orderNo, PlanningDocumentStatus.Cancelled, request, cancellationToken);

    private async Task<IActionResult> ChangeStatus(string orderNo, PlanningDocumentStatus status, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(new { orderNo, status = await workflow.ChangeProductionOrderStatusAsync(orderNo, status, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken) }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        try { var order = await flow.CreateProductionOrderAsync(request, cancellationToken); return CreatedAtAction(nameof(List), null, new { order.OrderNo, order.Status }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/receive")]
    public async Task<IActionResult> Receive(string orderNo, ProductionReceiptRequest request, CancellationToken cancellationToken)
    {
        try { var receipt = await flow.ReceiveProductionAsync(orderNo, request, cancellationToken); return Ok(new { receipt.ReceiptNo, receipt.Quantity, receipt.LocationCode }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    /// <summary>按生产订单物料需求执行领料并生成可追溯的生产领料单。</summary>
    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("{orderNo}/issue-materials")]
    public async Task<IActionResult> IssueMaterials(string orderNo, IssueProductionMaterialsRequest request, CancellationToken cancellationToken)
    {
        try { var issue = await flow.IssueMaterialsAsync(orderNo, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, cancellationToken); return Ok(new { orderNo, issueNo = issue.IssueNo, movements = issue.Movements }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

[ApiController]
[Route("api/production-material-issues")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class ProductionMaterialIssuesController(WmsDbContext db) : ControllerBase
{
    /// <summary>查询生产领料单及对应物料、批次、库位和领料数量。</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var headers = await db.DocumentAudits.AsNoTracking()
            .Where(x => x.DocumentType == "ProductionMaterialIssue" && x.Action == "Issue")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { issueNo = x.DocumentNo, orderNo = x.Remark, operatorName = x.Operator, issuedAt = x.CreatedAt })
            .ToListAsync(cancellationToken);
        var numbers = headers.Select(x => x.issueNo).ToList();
        var lines = await db.Movements.AsNoTracking().Include(x => x.Product).Include(x => x.Location)
            .Where(x => x.Type == InventoryMovementType.Issue && numbers.Contains(x.ReferenceNo))
            .Select(x => new { issueNo = x.ReferenceNo, sku = x.Product!.Sku, productName = x.Product.Name, locationCode = x.Location!.Code, x.BatchNo, quantity = -x.Quantity, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(headers.Select(header =>
        {
            var issueLines = lines.Where(line => line.issueNo == header.issueNo).ToList();
            return new { header.issueNo, header.orderNo, header.operatorName, header.issuedAt, totalQuantity = issueLines.Sum(x => x.quantity), lines = issueLines };
        }));
    }
}

[ApiController]
[Route("api/mrp")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class MrpController(WmsDbContext db, MrpService mrp, DocumentFlowService flow) : ControllerBase
{
    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("runs")]
    public async Task<IActionResult> Calculate(MrpRunRequest request, CancellationToken cancellationToken)
    {
        var run = await mrp.CalculateAsync(request, cancellationToken);
        return Ok(new { run.RunNo, run.PlanDate, suggestionCount = run.Suggestions.Count, suggestions = run.Suggestions.Select(x => new { x.Id, x.ProductId, x.Type, x.GrossRequirement, x.AvailableInventory, x.SafetyStock, x.NetRequirement, x.SuggestedQuantity, x.SourceOrderNos, x.Status }) });
    }

    [HttpGet("runs")]
    public async Task<IActionResult> Runs(CancellationToken cancellationToken) => Ok(await db.MrpRuns.AsNoTracking().OrderByDescending(x => x.CreatedAt).Select(x => new { x.RunNo, x.PlanDate, x.CreatedAt, x.Status, suggestionCount = x.Suggestions.Count }).ToListAsync(cancellationToken));

    [HttpGet("runs/{runNo}")]
    public async Task<IActionResult> Get(string runNo, CancellationToken cancellationToken)
    {
        var run = await db.MrpRuns.AsNoTracking().Include(x => x.Suggestions).ThenInclude(x => x.Product).Where(x => x.RunNo == runNo).Select(x => new
        {
            x.RunNo,
            x.PlanDate,
            x.CreatedAt,
            x.Status,
            suggestions = x.Suggestions.Select(s => new
            {
                s.Id,
                s.ProductId,
                productSku = s.Product!.Sku,
                productName = s.Product.Name,
                s.Type,
                s.GrossRequirement,
                s.AvailableInventory,
                s.SafetyStock,
                s.NetRequirement,
                s.SuggestedQuantity,
                s.SourceOrderNos,
                s.Status,
                productionReady = s.Type != MrpSuggestionType.Production ||
                    (db.BomHeaders.Any(b => b.ProductId == s.ProductId && b.IsActive) &&
                     db.RoutingHeaders.Any(r => r.ProductId == s.ProductId && r.IsActive && r.IsDefault && r.EffectiveFrom <= x.PlanDate && r.Operations.Any())),
                productionIssue = s.Type != MrpSuggestionType.Production ? "" :
                    (!db.BomHeaders.Any(b => b.ProductId == s.ProductId && b.IsActive) ? "缺少有效 BOM" :
                    (!db.RoutingHeaders.Any(r => r.ProductId == s.ProductId && r.IsActive && r.IsDefault && r.EffectiveFrom <= x.PlanDate && r.Operations.Any()) ? "缺少有效默认工艺路线" : ""))
            })
        }).SingleOrDefaultAsync(cancellationToken);
        return run is null ? NotFound() : Ok(run);
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("suggestions/{id:int}/push-production")]
    public async Task<IActionResult> PushProduction(int id, CancellationToken cancellationToken)
    {
        var suggestion = await db.MrpSuggestions.Include(x => x.Product).Include(x => x.MrpRun).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (suggestion is null) return NotFound(new { message = "MRP 建议不存在" });
        if (suggestion.Type != MrpSuggestionType.Production || suggestion.Status != PlanningDocumentStatus.Draft) return Conflict(new { message = "当前建议不是可下推的生产建议，或已经下推" });
        try
        {
            var order = await flow.CreateProductionOrderAsync(new CreateProductionOrderRequest("", suggestion.Product!.Sku, suggestion.SuggestedQuantity, DateTimeOffset.UtcNow.AddDays(7), suggestion.Id), cancellationToken);
            suggestion.Status = PlanningDocumentStatus.Released;
            db.DocumentRelations.Add(new DocumentRelation { SourceType = "MrpSuggestion", SourceNo = id.ToString(), TargetType = "ProductionOrder", TargetNo = order.OrderNo, Quantity = order.PlannedQuantity });
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "system";
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "MrpSuggestion", DocumentNo = id.ToString(), Action = "PushProduction", FromStatus = PlanningDocumentStatus.Draft.ToString(), ToStatus = PlanningDocumentStatus.Released.ToString(), Operator = actor, Remark = $"生成生产订单 {order.OrderNo}" });
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "ProductionOrder", DocumentNo = order.OrderNo, Action = "CreateFromMrp", FromStatus = "", ToStatus = order.Status.ToString(), Operator = actor, Remark = $"来源 MRP 建议 {id}" });
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { order.OrderNo, suggestion.Id, suggestion.Status });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = PermissionCodes.PlanningOperate)]
    [HttpPost("suggestions/{id:int}/push-purchase")]
    public async Task<IActionResult> PushPurchase(int id, CancellationToken cancellationToken)
    {
        var suggestion = await db.MrpSuggestions.Include(x => x.Product).Include(x => x.MrpRun).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (suggestion is null) return NotFound(new { message = "MRP 建议不存在" });
        if (suggestion.Type != MrpSuggestionType.Purchase || suggestion.Status != PlanningDocumentStatus.Draft) return Conflict(new { message = "当前建议不是可下推的采购建议，或已经下推" });
        try
        {
            var order = await flow.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest("", "MRP 待采购供应商", DateTimeOffset.UtcNow.AddDays(7), [new OrderLineRequest(suggestion.Product!.Sku, suggestion.SuggestedQuantity)]), cancellationToken);
            suggestion.Status = PlanningDocumentStatus.Released;
            db.DocumentRelations.Add(new DocumentRelation { SourceType = "MrpSuggestion", SourceNo = id.ToString(), TargetType = "PurchaseOrder", TargetNo = order.OrderNo, Quantity = suggestion.SuggestedQuantity });
            var actor = User.FindFirstValue(ClaimTypes.Name) ?? "system";
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "MrpSuggestion", DocumentNo = id.ToString(), Action = "PushPurchase", FromStatus = PlanningDocumentStatus.Draft.ToString(), ToStatus = PlanningDocumentStatus.Released.ToString(), Operator = actor, Remark = $"生成采购订单 {order.OrderNo}" });
            db.DocumentAudits.Add(new DocumentAudit { DocumentType = "PurchaseOrder", DocumentNo = order.OrderNo, Action = "CreateFromMrp", FromStatus = "", ToStatus = order.Status.ToString(), Operator = actor, Remark = $"来源 MRP 建议 {id}" });
            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { order.OrderNo, suggestion.Id, suggestion.Status });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
