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
[Route("api/quality-inspections")]
[Authorize(Policy = PermissionCodes.QualityOperate)]
public sealed class QualityInspectionsController(WmsDbContext db, IDistributedCache cache, IHubContext<InventoryHub> hub, DocumentNumberService numbers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.QualityInspections.AsNoTracking()
        .Include(x => x.Lines).ThenInclude(x => x.Product)
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new
        {
            x.InspectionNo, x.SourceInboundOrderNo, x.SupplierName, x.Status, x.Inspector, x.Remark, x.CreatedAt, x.CompletedAt,
            lines = x.Lines.Select(l => new { sku = l.Product!.Sku, productName = l.Product.Name, l.ReceivedQuantity, l.QualifiedQuantity, l.RejectedQuantity, l.Result, l.Remark })
        }).ToListAsync(cancellationToken));

    [HttpGet("{inspectionNo}")]
    public async Task<IActionResult> Get(string inspectionNo, CancellationToken cancellationToken)
    {
        var inspection = await db.QualityInspections.AsNoTracking().Where(x => x.InspectionNo == inspectionNo)
            .Select(x => new
            {
                x.InspectionNo, x.SourceInboundOrderNo, x.SupplierName, x.Status, x.Inspector, x.Remark, x.CreatedAt, x.CompletedAt,
                lines = x.Lines.Select(l => new { l.Id, sku = l.Product!.Sku, productName = l.Product.Name, l.ReceivedQuantity, l.QualifiedQuantity, l.RejectedQuantity, l.Result, l.Remark })
            }).SingleOrDefaultAsync(cancellationToken);
        return inspection is null ? NotFound(new { message = "质检单不存在" }) : Ok(inspection);
    }

    [Authorize(Policy = PermissionCodes.QualityOperate)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateQualityInspectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InboundOrderNo))
            return BadRequest(new { message = "来源入库单不能为空" });
        var inspectionNo = string.IsNullOrWhiteSpace(request.InspectionNo) ? await numbers.NextAsync("QualityInspection", cancellationToken) : request.InspectionNo.Trim();
        if (await db.QualityInspections.AnyAsync(x => x.InspectionNo == inspectionNo, cancellationToken))
            return Conflict(new { message = "质检单号已存在" });

        var inbound = await db.InboundOrders.Include(x => x.Lines).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.OrderNo == request.InboundOrderNo, cancellationToken);
        if (inbound is null) return NotFound(new { message = "来源入库单不存在" });
        if (inbound.Status != OrderStatus.Completed) return Conflict(new { message = "入库单完成收货后才能创建质检单" });
        if (inbound.Lines.Count == 0 || inbound.Lines.Any(x => x.ReceivedQuantity <= 0)) return Conflict(new { message = "来源入库单没有可检验数量" });
        if (await db.QualityInspections.AnyAsync(x => x.SourceInboundOrderNo == request.InboundOrderNo && x.Status != QualityInspectionStatus.Cancelled, cancellationToken))
            return Conflict(new { message = "该入库单已有有效质检单" });

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? request.Inspector;
        var inspection = new QualityInspection
        {
            InspectionNo = inspectionNo,
            SourceInboundOrderNo = inbound.OrderNo,
            SupplierName = inbound.SupplierName,
            Inspector = actor,
            Remark = request.Remark
        };
        inspection.Lines.AddRange(inbound.Lines.Select(x => new QualityInspectionLine
        {
            ProductId = x.ProductId,
            ReceivedQuantity = x.ReceivedQuantity
        }));
        db.QualityInspections.Add(inspection);
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "QualityInspection", DocumentNo = inspection.InspectionNo, Action = "Create", FromStatus = "", ToStatus = inspection.Status.ToString(), Operator = actor, Remark = request.Remark });
        db.DocumentRelations.Add(new DocumentRelation { SourceType = "InboundOrder", SourceNo = inbound.OrderNo, TargetType = "QualityInspection", TargetNo = inspection.InspectionNo, Quantity = inspection.Lines.Sum(x => x.ReceivedQuantity) });
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { inspectionNo = inspection.InspectionNo }, new { inspection.InspectionNo, inspection.SourceInboundOrderNo, inspection.Status });
    }

    [Authorize(Policy = PermissionCodes.QualityOperate)]
    [HttpPost("{inspectionNo}/submit")]
    public async Task<IActionResult> Submit(string inspectionNo, DocumentActionRequest request, CancellationToken cancellationToken) => await ChangeStatusAsync(inspectionNo, QualityInspectionStatus.Submitted, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken);

    [Authorize(Policy = PermissionCodes.QualityOperate)]
    [HttpPost("{inspectionNo}/cancel")]
    public async Task<IActionResult> Cancel(string inspectionNo, DocumentActionRequest request, CancellationToken cancellationToken) => await ChangeStatusAsync(inspectionNo, QualityInspectionStatus.Cancelled, User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken);

    [Authorize(Policy = PermissionCodes.QualityOperate)]
    [HttpPost("{inspectionNo}/complete")]
    public async Task<IActionResult> Complete(string inspectionNo, CompleteQualityInspectionRequest request, CancellationToken cancellationToken)
    {
        var inspection = await db.QualityInspections.Include(x => x.Lines).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.InspectionNo == inspectionNo, cancellationToken);
        if (inspection is null) return NotFound(new { message = "质检单不存在" });
        if (inspection.Status != QualityInspectionStatus.Submitted) return Conflict(new { message = "只有已提交的质检单可以完成检验" });
        if (request.Lines is null || request.Lines.Count != inspection.Lines.Count || request.Lines.GroupBy(x => x.Sku).Any(x => x.Count() > 1))
            return BadRequest(new { message = "质检明细必须完整且不能重复 SKU" });

        var requested = request.Lines.ToDictionary(x => x.Sku, x => x);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var line in inspection.Lines)
        {
            if (!requested.TryGetValue(line.Product!.Sku, out var item)) return BadRequest(new { message = $"缺少 SKU {line.Product.Sku} 的质检明细" });
            if (item.QualifiedQuantity < 0 || item.RejectedQuantity < 0 || item.QualifiedQuantity + item.RejectedQuantity != line.ReceivedQuantity)
                return BadRequest(new { message = $"SKU {line.Product.Sku} 的合格数与不合格数之和必须等于收货数 {line.ReceivedQuantity}" });
            line.QualifiedQuantity = item.QualifiedQuantity;
            line.RejectedQuantity = item.RejectedQuantity;
            line.Result = item.RejectedQuantity == 0 ? QualityInspectionResult.Passed : item.QualifiedQuantity == 0 ? QualityInspectionResult.Failed : QualityInspectionResult.Rework;
            line.Remark = item.Remark;
        }

        var holds = inspection.Lines.Where(x => x.RejectedQuantity > 0).GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Sum(line => line.RejectedQuantity));
        if (holds.Count > 0)
        {
            var productIds = holds.Keys.ToList();
            var receipts = await db.Movements.Where(x => x.Type == InventoryMovementType.Receive
                    && x.ReferenceNo == inspection.SourceInboundOrderNo && productIds.Contains(x.ProductId))
                .OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);
            var balanceIds = receipts.Select(x => x.LocationId).Distinct().ToList();
            var balances = await db.Inventory.Where(x => productIds.Contains(x.ProductId) && balanceIds.Contains(x.LocationId)).ToListAsync(cancellationToken);
            var capacities = balances.ToDictionary(x => x.Id, x => Math.Max(0, x.OnHand - x.Reserved - x.QualityHold));

            foreach (var hold in holds)
            {
                var remaining = hold.Value;
                foreach (var movement in receipts.Where(x => x.ProductId == hold.Key))
                {
                    if (remaining <= 0) break;
                    var balance = balances.SingleOrDefault(x => x.ProductId == movement.ProductId && x.LocationId == movement.LocationId && x.BatchNo == movement.BatchNo);
                    if (balance is null) continue;
                    var quantity = Math.Min(remaining, Math.Min(movement.Quantity, capacities[balance.Id]));
                    if (quantity <= 0) continue;
                    balance.QualityHold += quantity;
                    capacities[balance.Id] -= quantity;
                    remaining -= quantity;
                    db.Movements.Add(new InventoryMovement
                    {
                        ProductId = balance.ProductId,
                        LocationId = balance.LocationId,
                        Type = InventoryMovementType.QualityHold,
                        Quantity = quantity,
                        BatchNo = balance.BatchNo,
                        ExpiryDate = balance.ExpiryDate,
                        ReferenceNo = inspection.InspectionNo,
                        Operator = User.FindFirstValue(ClaimTypes.Name) ?? request.Inspector
                    });
                }

                if (remaining > 0)
                    return Conflict(new { message = $"SKU {inspection.Lines.Single(x => x.ProductId == hold.Key).Product!.Sku} 当前可冻结库存不足，可能已被预占或出库" });
            }
        }

        var actor = User.FindFirstValue(ClaimTypes.Name) ?? request.Inspector;
        inspection.Inspector = actor;
        inspection.Status = QualityInspectionStatus.Completed;
        inspection.CompletedAt = DateTimeOffset.UtcNow;
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "QualityInspection", DocumentNo = inspection.InspectionNo, Action = "Complete", FromStatus = QualityInspectionStatus.Submitted.ToString(), ToStatus = inspection.Status.ToString(), Operator = actor, Remark = inspection.Remark });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await cache.RemoveAsync("wms:inventory:all", cancellationToken);
        await hub.Clients.All.SendAsync("InventoryChanged", new { action = "qualityHoldApplied", inspectionNo }, cancellationToken);
        return Ok(new { inspection.InspectionNo, inspection.Status, inspection.CompletedAt, lines = inspection.Lines.Select(x => new { sku = x.Product!.Sku, x.ReceivedQuantity, x.QualifiedQuantity, x.RejectedQuantity, qualityHold = x.RejectedQuantity, x.Result }) });
    }

    private async Task<IActionResult> ChangeStatusAsync(string inspectionNo, QualityInspectionStatus to, string actor, string remark, CancellationToken cancellationToken)
    {
        var inspection = await db.QualityInspections.SingleOrDefaultAsync(x => x.InspectionNo == inspectionNo, cancellationToken);
        if (inspection is null) return NotFound(new { message = "质检单不存在" });
        var allowed = (inspection.Status, to) is (QualityInspectionStatus.Draft, QualityInspectionStatus.Submitted) or (QualityInspectionStatus.Draft, QualityInspectionStatus.Cancelled) or (QualityInspectionStatus.Submitted, QualityInspectionStatus.Cancelled);
        if (!allowed) return Conflict(new { message = $"质检单不能从 {inspection.Status} 变更为 {to}" });
        db.DocumentAudits.Add(new DocumentAudit { DocumentType = "QualityInspection", DocumentNo = inspection.InspectionNo, Action = to == QualityInspectionStatus.Submitted ? "Submit" : "Cancel", FromStatus = inspection.Status.ToString(), ToStatus = to.ToString(), Operator = actor, Remark = remark });
        inspection.Status = to;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { inspection.InspectionNo, inspection.Status });
    }
}
