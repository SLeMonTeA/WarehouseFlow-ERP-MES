using System.Security.Claims;
using System.Text.Json;
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
    /// <summary>查询其他出入库调整单及其审核状态。</summary>
    [HttpGet("adjustments")]
    [Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<IActionResult> ListAdjustments([FromQuery] string? type, CancellationToken cancellationToken)
    {
        var query = db.DocumentAudits.AsNoTracking().Where(x => x.DocumentType == "InventoryAdjustment" && x.Action == "Create");
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.FromStatus == type);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Select(x => new { documentNo = x.DocumentNo, type = x.FromStatus, status = x.ToStatus, operatorName = x.Operator, payload = x.Remark, createdAt = x.CreatedAt }).ToListAsync(cancellationToken);
        return Ok(rows.Select(x =>
        {
            var request = JsonSerializer.Deserialize<CreateInventoryAdjustmentRequest>(x.payload);
            return new { x.documentNo, x.type, x.status, x.operatorName, x.createdAt, sku = request?.Sku, locationCode = request?.LocationCode, batchNo = request?.BatchNo, quantity = request?.Quantity, reason = request?.Reason };
        }));
    }

    /// <summary>创建其他入库或其他出库草稿，审核通过后才过账库存。</summary>
    [HttpPost("adjustments")]
    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    public async Task<IActionResult> CreateAdjustment(CreateInventoryAdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (request.Type is not ("OtherInbound" or "OtherOutbound")) return BadRequest(new { message = "调整类型无效" });
        if (request.Quantity <= 0 || string.IsNullOrWhiteSpace(request.Sku) || string.IsNullOrWhiteSpace(request.LocationCode) || string.IsNullOrWhiteSpace(request.BatchNo) || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "物料、库位、批次、原因不能为空，数量必须大于 0" });
        if (request.ExpiryDate is not null && request.ExpiryDate <= DateTimeOffset.UtcNow) return BadRequest(new { message = "效期必须晚于当前时间" });
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.Sku && x.IsActive, cancellationToken);
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == request.LocationCode && x.IsActive, cancellationToken);
        if (product is null || location is null) return NotFound(new { message = "物料或启用库位不存在" });
        if (request.Type == "OtherOutbound")
        {
            var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == product.Id && x.LocationId == location.Id && x.BatchNo == request.BatchNo, cancellationToken);
            if (balance is null || balance.ExpiryDate is not null && balance.ExpiryDate <= DateTimeOffset.UtcNow || request.Quantity > balance.OnHand - balance.Reserved - balance.QualityHold)
                return Conflict(new { message = "所选批次可用库存不足，或批次已过期" });
        }
        var no = $"ADJ-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..29];
        var body = JsonSerializer.Serialize(request);
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InventoryAdjustment", DocumentNo = no, Action = "Create", FromStatus = request.Type, ToStatus = "Draft", Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, Remark = body });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { documentNo = no, type = request.Type, status = "Draft" });
    }

    /// <summary>审核其他出入库调整单并在同一事务内更新库存余额和库存流水。</summary>
    [HttpPost("adjustments/{documentNo}/approve")]
    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    public async Task<IActionResult> ApproveAdjustment(string documentNo, CancellationToken cancellationToken)
    {
        var draft = await db.DocumentAudits.SingleOrDefaultAsync(x => x.DocumentType == "InventoryAdjustment" && x.DocumentNo == documentNo && x.Action == "Create" && x.ToStatus == "Draft", cancellationToken);
        if (draft is null) return NotFound(new { message = "其他出入库草稿不存在或已审核" });
        var request = JsonSerializer.Deserialize<CreateInventoryAdjustmentRequest>(draft.Remark);
        if (request is null) return Conflict(new { message = "调整单内容损坏" });
        var product = await db.Products.SingleAsync(x => x.Sku == request.Sku, cancellationToken);
        var location = await db.Locations.SingleAsync(x => x.Code == request.LocationCode, cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == product.Id && x.LocationId == location.Id && x.BatchNo == request.BatchNo, cancellationToken);
        if (request.Type == "OtherInbound")
        {
            var locationOnHand = await db.Inventory.Where(x => x.LocationId == location.Id).SumAsync(x => x.OnHand, cancellationToken);
            if (locationOnHand + request.Quantity > location.Capacity) return Conflict(new { message = "入库后将超过库位容量" });
            if (balance is null) { balance = new InventoryBalance { ProductId = product.Id, LocationId = location.Id, BatchNo = request.BatchNo, ExpiryDate = request.ExpiryDate }; db.Inventory.Add(balance); }
            else if (balance.ExpiryDate != request.ExpiryDate && request.ExpiryDate is not null) return Conflict(new { message = "同批次效期不一致" });
            balance.OnHand += request.Quantity;
            db.Movements.Add(new InventoryMovement { ProductId = product.Id, LocationId = location.Id, Type = InventoryMovementType.OtherInbound, Quantity = request.Quantity, ReferenceNo = documentNo, BatchNo = request.BatchNo, ExpiryDate = request.ExpiryDate, Operator = User.FindFirstValue(ClaimTypes.Name) ?? draft.Operator });
        }
        else
        {
            if (balance is null || request.Quantity > balance.OnHand - balance.Reserved - balance.QualityHold || balance.ExpiryDate is not null && balance.ExpiryDate <= DateTimeOffset.UtcNow)
                return Conflict(new { message = "审核时可用库存不足或批次已过期，调整单未过账" });
            balance.OnHand -= request.Quantity;
            db.Movements.Add(new InventoryMovement { ProductId = product.Id, LocationId = location.Id, Type = InventoryMovementType.OtherOutbound, Quantity = -request.Quantity, ReferenceNo = documentNo, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, Operator = User.FindFirstValue(ClaimTypes.Name) ?? draft.Operator });
        }
        draft.ToStatus = "Completed";
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InventoryAdjustment", DocumentNo = documentNo, Action = "Approve", FromStatus = "Draft", ToStatus = "Completed", Operator = User.FindFirstValue(ClaimTypes.Name) ?? draft.Operator, Remark = request.Reason });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await InvalidateAndNotifyAsync(product.Sku, "inventoryAdjustmentApproved", cancellationToken);
        return Ok(new { documentNo, request.Type, status = "Completed", request.Sku, request.Quantity });
    }

    /// <summary>作废尚未审核的其他出入库调整草稿。</summary>
    [HttpPost("adjustments/{documentNo}/cancel")]
    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    public async Task<IActionResult> CancelAdjustment(string documentNo, CancellationToken cancellationToken)
    {
        var draft = await db.DocumentAudits.SingleOrDefaultAsync(x => x.DocumentType == "InventoryAdjustment" && x.DocumentNo == documentNo && x.Action == "Create" && x.ToStatus == "Draft", cancellationToken);
        if (draft is null) return NotFound(new { message = "只有未审核的调整单草稿可以作废" });
        draft.ToStatus = "Cancelled";
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "InventoryAdjustment", DocumentNo = documentNo, Action = "Cancel", FromStatus = "Draft", ToStatus = "Cancelled", Operator = User.FindFirstValue(ClaimTypes.Name) ?? draft.Operator });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { documentNo, status = "Cancelled" });
    }

    /// <summary>查询按物料、库位和批次拆分的即时库存余额及预占、质检冻结数量。</summary>
    [HttpGet]
    [Authorize(Policy = PermissionCodes.InventoryView)]
    public async Task<ActionResult<IReadOnlyList<InventoryRow>>> Get(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await db.Inventory.AsNoTracking().Include(x => x.Product).Include(x => x.Location)
            .Where(x => x.OnHand != 0 || x.Reserved != 0 || x.QualityHold != 0)
            .OrderBy(x => x.Product!.Sku)
            .Select(x => new InventoryRow(x.Product!.Sku, x.Product.Name, x.Product.Category,
                x.Location!.Warehouse!.Code, x.Location.Warehouse.Name, x.Location.Code,
                x.BatchNo, x.ExpiryDate, x.OnHand, x.Reserved, x.QualityHold,
                x.ExpiryDate != null && x.ExpiryDate <= now ? 0 : x.OnHand - x.Reserved - x.QualityHold, x.Product.SafetyStock)).ToListAsync(cancellationToken);
        return Ok(rows);
    }

    /// <summary>停用旧的即时收货接口；采购收货须审核收货通知，非采购收货须审核其他入库单。</summary>
    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("receive")]
    public IActionResult Receive() => Conflict(new { message = "即时收货接口已停用，请使用收货通知审核或其他入库调整单" });

    /// <summary>停用旧的即时其他入库接口；请创建调整草稿并审核后过账。</summary>
    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("other-inbound")]
    public IActionResult OtherInbound() => Conflict(new { message = "即时过账接口已停用，请通过 /api/inventory/adjustments 创建草稿并审核" });

    private async Task<IActionResult> ReceiveStockAsync(ReceiveStockRequest request, InventoryMovementType movementType, string action, CancellationToken cancellationToken)
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
            Type = movementType, ReferenceNo = request.ReferenceNo,
            Operator = User.FindFirstValue(ClaimTypes.Name) ?? "unknown", BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await InvalidateAndNotifyAsync(product.Sku, action, cancellationToken);
        return Ok(new { request.Sku, request.LocationCode, balance.BatchNo, balance.ExpiryDate, balance.OnHand, balance.Reserved, balance.QualityHold, available = balance.ExpiryDate is not null && balance.ExpiryDate <= DateTimeOffset.UtcNow ? 0 : balance.OnHand - balance.Reserved - balance.QualityHold });
    }

    /// <summary>按效期优先规则预占可用库存，并记录对应库存流水。</summary>
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

    /// <summary>停用旧的即时库存调整接口；请通过其他出入库调整单审核过账。</summary>
    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("adjust")]
    public IActionResult Adjust() => Conflict(new { message = "即时库存调整接口已停用，请通过 /api/inventory/adjustments 创建其他出入库草稿并审核" });

    /// <summary>停用旧的即时其他出库接口；请创建调整草稿并审核后过账。</summary>
    [Authorize(Policy = PermissionCodes.InventoryOperate)]
    [HttpPost("other-outbound")]
    public IActionResult OtherOutbound() => Conflict(new { message = "即时过账接口已停用，请通过 /api/inventory/adjustments 创建草稿并审核" });

    /// <summary>查询库存流水，可按物料编码过滤并限制返回条数。</summary>
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
