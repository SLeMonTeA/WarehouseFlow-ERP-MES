using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using WarehouseFlow.Api.Application.Documents;
using WarehouseFlow.Api.Application.Planning;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;
using WarehouseFlow.Api.Realtime;

namespace WarehouseFlow.Api.Controllers;

/// <summary>
/// ERP/WMS 与 MES 的稳定对接边界。
/// MES 负责车间执行，ERP 负责生产订单、库存扣减和成品入库。
/// </summary>
[ApiController]
[Route("api/integration/mes")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class MesIntegrationController(WmsDbContext db, DocumentFlowService flow, DocumentWorkflowService workflow, IDistributedCache cache, IHubContext<InventoryHub> hub) : ControllerBase
{
    [HttpGet("production-orders")]
    public async Task<IActionResult> ListProductionOrders(CancellationToken cancellationToken)
    {
        var orders = await db.ProductionOrders.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Materials).ThenInclude(x => x.ComponentProduct)
            .Include(x => x.Operations)
            .Where(x => new[] { PlanningDocumentStatus.Released, PlanningDocumentStatus.InProgress, PlanningDocumentStatus.PartiallyCompleted, PlanningDocumentStatus.Completed }.Contains(x.Status))
            .OrderBy(x => x.RequiredAt)
            .ToListAsync(cancellationToken);
        return Ok(orders.Select(ToSnapshot));
    }

    [HttpGet("production-order-events")]
    public async Task<IActionResult> ListProductionOrderEvents(long afterId = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var events = await db.IntegrationOutboxEvents.AsNoTracking()
            .Where(x => x.Id > afterId && x.AggregateType == "ProductionOrder")
            .OrderBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var orderNos = events.Select(x => x.AggregateNo).Distinct().ToList();
        var orders = await db.ProductionOrders.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Materials).ThenInclude(x => x.ComponentProduct)
            .Include(x => x.Operations)
            .Where(x => orderNos.Contains(x.OrderNo))
            .ToDictionaryAsync(x => x.OrderNo, cancellationToken);

        return Ok(events.Select(x => new MesProductionOrderEvent(
            x.Id,
            x.EventKey,
            x.EventType,
            x.AggregateNo,
            x.OccurredAt,
            orders.TryGetValue(x.AggregateNo, out var order) ? ToSnapshot(order) : null)));
    }

    [HttpGet("production-orders/{orderNo}")]
    public async Task<IActionResult> GetProductionOrder(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Materials).ThenInclude(x => x.ComponentProduct)
            .Include(x => x.Operations)
            .SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        return order is null ? NotFound(new { message = "生产订单不存在" }) : Ok(ToSnapshot(order));
    }

    [HttpGet("production-locations")]
    public async Task<IActionResult> ListProductionLocations(CancellationToken cancellationToken)
    {
        var locations = await db.Locations.AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.IsActive && x.Warehouse!.IsActive)
            .OrderBy(x => x.Warehouse!.Code)
            .ThenBy(x => x.Code)
            .Select(x => new
            {
                locationCode = x.Code,
                x.Zone,
                x.Capacity,
                warehouseCode = x.Warehouse!.Code,
                warehouseName = x.Warehouse.Name
            })
            .ToListAsync(cancellationToken);
        return Ok(locations);
    }

    [HttpPost("production-orders/{orderNo}/start")]
    public async Task<IActionResult> StartProduction(string orderNo, DocumentActionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var status = await workflow.ChangeProductionOrderStatusAsync(orderNo, PlanningDocumentStatus.InProgress,
                User.FindFirstValue(ClaimTypes.Name) ?? request.Operator, request.Remark, cancellationToken);
            return Ok(new { orderNo, status });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("production-orders/{orderNo}/progress")]
    public async Task<IActionResult> ReportProgress(string orderNo, MesProductionProgressRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventKey)) return BadRequest(new { message = "报工事件号不能为空" });
        if (request.GoodQuantity < 0 || request.DefectQuantity < 0 || request.GoodQuantity + request.DefectQuantity <= 0)
            return BadRequest(new { message = "报工数量必须大于 0" });

        var duplicate = await db.ProductionExecutionReports.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventKey == request.EventKey, cancellationToken);
        if (duplicate is not null)
        {
            var duplicateOrder = await db.ProductionOrders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
            return Ok(new { accepted = true, duplicate = true, orderNo, status = duplicateOrder?.Status.ToString(), reportedGoodQuantity = duplicateOrder?.ReportedGoodQuantity ?? 0, reportedDefectQuantity = duplicateOrder?.ReportedDefectQuantity ?? 0 });
        }

        var order = await db.ProductionOrders.Include(x => x.Operations).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "生产订单不存在" });
        if (order.Status is PlanningDocumentStatus.Cancelled or PlanningDocumentStatus.Closed or PlanningDocumentStatus.Completed)
            return Conflict(new { message = "当前生产订单不允许继续报工" });
        ProductionOrderOperation? operation = null;
        var isFinalOperation = true;
        var operationInputQuantity = order.PlannedQuantity;
        if (order.Operations.Count > 0)
        {
            operation = order.Operations.OrderBy(x => x.Sequence).SingleOrDefault(x => x.OperationCode.Equals(request.OperationCode, StringComparison.OrdinalIgnoreCase));
            if (operation is null) return Conflict(new { message = "当前生产订单不存在该工序" });
            var current = order.Operations.OrderBy(x => x.Sequence).FirstOrDefault(x => x.Status != "Completed");
            if (current?.Id != operation.Id) return Conflict(new { message = $"当前应报工序为 {current?.OperationName ?? "无"}，不能跳序报工" });
            var previous = order.Operations.OrderBy(x => x.Sequence).LastOrDefault(x => x.Sequence < operation.Sequence);
            operationInputQuantity = previous?.ReportedGoodQuantity ?? order.PlannedQuantity;
            if (operation.ReportedGoodQuantity + operation.ReportedDefectQuantity + request.GoodQuantity + request.DefectQuantity > operationInputQuantity)
                return Conflict(new { message = "本工序报工数量不能超过前道工序合格数量" });
            isFinalOperation = operation.Id == order.Operations.OrderByDescending(x => x.Sequence).First().Id;
        }
        else if (order.ReportedGoodQuantity + request.GoodQuantity > order.PlannedQuantity)
            return Conflict(new { message = "报工良品数不能超过生产订单计划数" });

        var fromStatus = order.Status;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (order.Status == PlanningDocumentStatus.Released)
            order.Status = PlanningDocumentStatus.InProgress;
        if (operation is null)
        {
            order.ReportedGoodQuantity += request.GoodQuantity;
            order.ReportedDefectQuantity += request.DefectQuantity;
        }
        else
        {
            operation.Status = "InProgress";
            operation.ReportedGoodQuantity += request.GoodQuantity;
            operation.ReportedDefectQuantity += request.DefectQuantity;
            if (operation.ReportedGoodQuantity + operation.ReportedDefectQuantity >= operationInputQuantity) operation.Status = "Completed";
            if (isFinalOperation)
            {
                order.ReportedGoodQuantity = operation.ReportedGoodQuantity;
                order.ReportedDefectQuantity = operation.ReportedDefectQuantity;
            }
        }
        db.ProductionExecutionReports.Add(new ProductionExecutionReport
        {
            EventKey = request.EventKey,
            OrderNo = orderNo,
            OperationCode = request.OperationCode,
            Workstation = request.Workstation,
            GoodQuantity = request.GoodQuantity,
            DefectQuantity = request.DefectQuantity,
            Operator = request.Operator,
            ReportedAt = request.ReportedAt
        });
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = "ProductionOrder",
            DocumentNo = orderNo,
            Action = "MesProgressReported",
            FromStatus = fromStatus.ToString(),
            ToStatus = order.Status.ToString(),
            Operator = request.Operator,
            Remark = $"MES 报工 {request.OperationCode}，良品 {request.GoodQuantity}，不良 {request.DefectQuantity}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            accepted = true,
            duplicate = false,
            orderNo,
            status = order.Status,
            reportedGoodQuantity = order.ReportedGoodQuantity,
            reportedDefectQuantity = order.ReportedDefectQuantity,
            completedQuantity = order.CompletedQuantity
        });
    }

    [HttpPost("production-orders/{orderNo}/quality")]
    public async Task<IActionResult> ReportQuality(string orderNo, MesProductionQualityRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventKey)) return BadRequest(new { message = "质检事件号不能为空" });
        var duplicate = await db.ProductionQualityReports.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventKey == request.EventKey, cancellationToken);
        if (duplicate is not null)
        {
            var duplicateOrder = await db.ProductionOrders.AsNoTracking().SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
            return Ok(new { accepted = true, duplicate = true, orderNo, qualityStatus = duplicateOrder?.QualityStatus.ToString() });
        }

        var order = await db.ProductionOrders.Include(x => x.Operations).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (order is null) return NotFound(new { message = "生产订单不存在" });
        if (order.Status is PlanningDocumentStatus.Cancelled or PlanningDocumentStatus.Closed)
            return Conflict(new { message = "当前生产订单不允许登记质检" });
        var hasReportedQuantity = order.ReportedGoodQuantity + order.ReportedDefectQuantity > 0 ||
            order.Operations.Any(x => x.ReportedGoodQuantity + x.ReportedDefectQuantity > 0);
        if (!hasReportedQuantity)
            return Conflict(new { message = "生产订单尚未报工，不能登记质检" });

        var previousStatus = order.QualityStatus;
        order.QualityStatus = request.Result switch
        {
            QualityInspectionResult.Passed => ProductionQualityStatus.Passed,
            QualityInspectionResult.Failed => ProductionQualityStatus.Failed,
            QualityInspectionResult.Rework => ProductionQualityStatus.Rework,
            _ => ProductionQualityStatus.Pending
        };
        order.QualityRemark = request.Remark ?? string.Empty;
        order.QualityUpdatedAt = DateTimeOffset.UtcNow;
        db.ProductionQualityReports.Add(new ProductionQualityReport
        {
            EventKey = request.EventKey,
            OrderNo = orderNo,
            Checkpoint = request.Checkpoint,
            Result = request.Result,
            Remark = request.Remark,
            Inspector = request.Inspector,
            InspectedAt = request.InspectedAt
        });
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = "ProductionOrder",
            DocumentNo = orderNo,
            Action = "MesQualityReported",
            FromStatus = previousStatus.ToString(),
            ToStatus = order.QualityStatus.ToString(),
            Operator = request.Inspector,
            Remark = $"MES 质检 {request.Checkpoint}：{request.Result}，{request.Remark}"
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { accepted = true, duplicate = false, orderNo, qualityStatus = order.QualityStatus, qualityRemark = order.QualityRemark });
    }

    [HttpPost("production-orders/{orderNo}/issue-materials")]
    public async Task<IActionResult> IssueMaterials(string orderNo, IssueProductionMaterialsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var operatorName = User.FindFirstValue(ClaimTypes.Name) ?? request.Operator;
            var issue = await flow.IssueMaterialsAsync(orderNo, operatorName, cancellationToken);
            await cache.RemoveAsync("wms:inventory:all", cancellationToken);
            await hub.Clients.All.SendAsync("InventoryChanged", new { action = "productionMaterialsIssued", orderNo, issueNo = issue.IssueNo }, cancellationToken);
            return Ok(new { issueNo = issue.IssueNo, orderNo, operatorName, movements = issue.Movements });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("production-orders/{orderNo}/receive")]
    public async Task<IActionResult> ReceiveProduction(string orderNo, ProductionReceiptRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await flow.ReceiveProductionAsync(orderNo, request, cancellationToken);
            await cache.RemoveAsync("wms:inventory:all", cancellationToken);
            await hub.Clients.All.SendAsync("InventoryChanged", new { action = "productionReceived", orderNo, receiptNo = receipt.ReceiptNo }, cancellationToken);
            return Ok(new { receipt.ReceiptNo, receipt.ProductionOrderId, receipt.ProductId, receipt.LocationCode, receipt.Quantity, receipt.ReceivedAt });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    private static MesProductionOrderSnapshot ToSnapshot(ProductionOrder order) => new(
        order.OrderNo,
        order.Product?.Sku ?? string.Empty,
        order.Product?.Name ?? string.Empty,
        order.Status.ToString(),
        order.PlannedQuantity,
        order.CompletedQuantity,
        order.ReportedGoodQuantity,
        order.ReportedDefectQuantity,
        order.QualityStatus.ToString(),
        order.QualityRemark,
        order.RequiredAt,
        order.Materials.Select(x => new MesProductionOrderMaterial(
            x.ComponentProduct?.Sku ?? string.Empty,
            x.ComponentProduct?.Name ?? string.Empty,
            x.RequiredQuantity,
            x.IssuedQuantity,
            Math.Max(0, x.RequiredQuantity - x.IssuedQuantity))).ToList(),
        order.RoutingCode,
        order.RoutingVersion,
        order.Operations.OrderBy(x => x.Sequence).Select(x => new MesProductionOrderOperation(x.Sequence, x.OperationCode, x.OperationName, x.WorkCenter, x.StandardMinutes, x.RequiresQuality, x.ReportedGoodQuantity, x.ReportedDefectQuantity, x.Status)).ToList());
}
