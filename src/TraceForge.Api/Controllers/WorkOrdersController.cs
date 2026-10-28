using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Contracts;
using TraceForge.Api.Domain;
using TraceForge.Api.Infrastructure;
using TraceForge.Api.Realtime;

namespace TraceForge.Api.Controllers;

[ApiController]
[Route("api/work-orders")]
public sealed class WorkOrdersController(
    TraceForgeDbContext db,
    IHubContext<ProductionHub> hub,
    WarehouseFlowClient erp,
    LumenGridClient lumenGrid,
    LumenGridExecutionDeliveryService lumenGridDelivery) : ControllerBase
{
    /// <summary>查询有效生产工单、工序报工进度及过程质检记录。</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await db.WorkOrders.AsNoTracking().Where(x => x.ArchivedAt == null).OrderByDescending(x => x.PlannedStart)
            .Select(x => new { x.OrderNo, x.ProductCode, x.BatchNo, x.RoutingCode, x.RoutingVersion, x.PlannedQuantity, x.CompletedQuantity, x.ReportedGoodQuantity, x.ReportedDefectQuantity, x.ErpCompletedQuantity, x.Status, x.ErpStarted, x.ErpMaterialIssued, x.ErpQualityStatus, x.PlannedStart, operations = x.Operations.OrderBy(o => o.Sequence).Select(o => new { o.Sequence, o.OperationCode, o.OperationName, o.WorkCenter, o.RequiresQuality, o.ReportedGoodQuantity, o.ReportedDefectQuantity, o.Status }), inspections = x.Inspections.OrderBy(i => i.InspectedAt).Select(i => new { i.OperationCode, i.Checkpoint, result = i.Result.ToString(), i.Inspector, i.InspectedAt, i.Remark }) })
            .ToListAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>创建 MES 本地工单及其工序路线；ERP 同步工单使用独立同步接口。</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.PlannedQuantity <= 0) return BadRequest(new { message = "计划数量必须大于 0" });
        var requestedOperations = request.Operations ?? [];
        if (requestedOperations.Any(x => x.Sequence <= 0 || x.StandardMinutes < 0))
            return BadRequest(new { message = "工序顺序必须大于 0，标准工时不能为负数" });
        if (requestedOperations.GroupBy(x => x.Sequence).Any(x => x.Count() > 1))
            return BadRequest(new { message = "工序顺序不能重复" });
        if (requestedOperations.GroupBy(x => x.OperationCode.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            return BadRequest(new { message = "工序编码不能重复" });
        if (requestedOperations.Any(x => string.IsNullOrWhiteSpace(x.OperationCode) || string.IsNullOrWhiteSpace(x.OperationName) || string.IsNullOrWhiteSpace(x.WorkCenter)))
            return BadRequest(new { message = "工序编码、名称和工作中心不能为空" });
        if (await db.WorkOrders.AnyAsync(x => x.OrderNo == request.OrderNo, cancellationToken))
            return Conflict(new { message = "工单号已存在" });

        var workOrder = new WorkOrder
        {
            OrderNo = request.OrderNo, ProductCode = request.ProductCode, BatchNo = request.BatchNo,
            PlannedQuantity = request.PlannedQuantity, PlannedStart = request.PlannedStart,
            Status = WorkOrderStatus.Released
        };
        workOrder.Operations.AddRange(requestedOperations.OrderBy(x => x.Sequence).Select(x => new WorkOrderOperation
        {
            Sequence = x.Sequence,
            OperationCode = x.OperationCode.Trim(),
            OperationName = x.OperationName.Trim(),
            WorkCenter = x.WorkCenter.Trim(),
            StandardMinutes = x.StandardMinutes,
            RequiresQuality = x.RequiresQuality,
            Status = "Pending"
        }));
        db.WorkOrders.Add(workOrder);
        db.TraceEvents.Add(new TraceEvent { BatchNo = workOrder.BatchNo, EventType = "WorkOrderReleased", EventData = workOrder.OrderNo });
        await db.SaveChangesAsync(cancellationToken);
        await NotifyBatchAsync(workOrder.BatchNo, "workOrderReleased", workOrder.OrderNo, cancellationToken);
        return CreatedAtAction(nameof(GetByOrderNo), new { orderNo = workOrder.OrderNo }, new
        {
            workOrder.Id,
            workOrder.OrderNo,
            workOrder.ProductCode,
            workOrder.BatchNo,
            workOrder.PlannedQuantity,
            workOrder.PlannedStart,
            workOrder.Status,
            operations = workOrder.Operations.OrderBy(x => x.Sequence).Select(x => new
            {
                x.Sequence,
                x.OperationCode,
                x.OperationName,
                x.WorkCenter,
                x.StandardMinutes,
                x.RequiresQuality,
                x.Status
            })
        });
    }

    /// <summary>按工单号查询工单、报工、质检及工序明细。</summary>
    [HttpGet("{orderNo}")]
    public async Task<IActionResult> GetByOrderNo(string orderNo, CancellationToken cancellationToken)
    {
        var result = await db.WorkOrders.AsNoTracking().Include(x => x.Reports).Include(x => x.Inspections).Include(x => x.Operations)
            .SingleOrDefaultAsync(x => x.OrderNo == orderNo && x.ArchivedAt == null, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>校验设备可用状态并启动当前工序，同时向数字孪生派发执行事件。</summary>
    [HttpPost("{orderNo}/operations/{operationCode}/start")]
    public async Task<IActionResult> StartOperation(string orderNo, string operationCode, OperationExecutionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventKey)) return BadRequest(new { message = "EventKey 不能为空" });
        if (string.IsNullOrWhiteSpace(request.DeviceCode)) return BadRequest(new { message = "DeviceCode 不能为空" });
        var workOrder = await db.WorkOrders.Include(x => x.Operations).Include(x => x.Inspections).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (workOrder is null) return NotFound(new { message = "工单不存在" });
        if (workOrder.Status is WorkOrderStatus.Completed or WorkOrderStatus.Closed or WorkOrderStatus.Cancelled)
            return Conflict(new { message = $"工单当前状态为 {workOrder.Status}，不能开工" });
        var operation = FindOperation(workOrder, operationCode);
        if (operation is null) return NotFound(new { message = "工序不存在" });
        var current = workOrder.Operations.OrderBy(x => x.Sequence).FirstOrDefault(x => x.Status != "Completed");
        if (current?.Id != operation.Id) return Conflict(new { message = $"当前应执行工序为 {current?.OperationName ?? "无"}" });
        if (operation.Status is "InProgress") return Conflict(new { message = "工序已经在执行中" });

        LumenGridEquipmentAvailabilityResponse? availability;
        try
        {
            availability = await lumenGrid.GetEquipmentAvailabilityAsync(operation.WorkCenter, cancellationToken);
        }
        catch (Exception ex) when (ex is LumenGridIntegrationException or HttpRequestException)
        {
            return ToLumenGridProblem(ex);
        }
        var device = availability?.Items.FirstOrDefault(x => x.DeviceCode.Equals(request.DeviceCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (device is null) return Conflict(new { message = $"设备 {request.DeviceCode} 不属于工序工作中心 {operation.WorkCenter}" });
        if (!device.IsAvailable) return Conflict(new { message = $"设备 {device.DeviceCode} 当前不可用，不能开工" });

        var occurredAt = DateTimeOffset.UtcNow;
        operation.Status = "InProgress";
        workOrder.Status = WorkOrderStatus.InProgress;
        var delivery = lumenGridDelivery.Enqueue(workOrder, operation, request.EventKey, "OperationStarted", device.DeviceCode, "Running", occurredAt);
        db.TraceEvents.Add(new TraceEvent { BatchNo = workOrder.BatchNo, EventType = "OperationStarted", EventData = $"{operation.OperationCode}:{device.DeviceCode}" });
        await db.SaveChangesAsync(cancellationToken);
        await NotifyBatchAsync(workOrder.BatchNo, "operationStarted", workOrder.OrderNo, cancellationToken);
        return Ok(new { workOrder.OrderNo, operation.OperationCode, operation.Status, deviceCode = device.DeviceCode, lumengrid = new { status = "Queued", eventKey = delivery.EventKey } });
    }

    /// <summary>暂停当前工序并将暂停事件写入追溯及数字孪生待发送队列。</summary>
    [HttpPost("{orderNo}/operations/{operationCode}/pause")]
    public async Task<IActionResult> PauseOperation(string orderNo, string operationCode, OperationExecutionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventKey)) return BadRequest(new { message = "EventKey 不能为空" });
        if (string.IsNullOrWhiteSpace(request.DeviceCode)) return BadRequest(new { message = "DeviceCode 不能为空" });
        var workOrder = await db.WorkOrders.Include(x => x.Operations).Include(x => x.Inspections).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (workOrder is null) return NotFound(new { message = "工单不存在" });
        var operation = FindOperation(workOrder, operationCode);
        if (operation is null) return NotFound(new { message = "工序不存在" });
        if (operation.Status != "InProgress") return Conflict(new { message = "只有执行中的工序可以暂停" });

        var occurredAt = DateTimeOffset.UtcNow;
        operation.Status = "Paused";
        var delivery = lumenGridDelivery.Enqueue(workOrder, operation, request.EventKey, "OperationPaused", request.DeviceCode, "Paused", occurredAt);
        db.TraceEvents.Add(new TraceEvent { BatchNo = workOrder.BatchNo, EventType = "OperationPaused", EventData = $"{operation.OperationCode}:{request.DeviceCode}" });
        await db.SaveChangesAsync(cancellationToken);
        await NotifyBatchAsync(workOrder.BatchNo, "operationPaused", workOrder.OrderNo, cancellationToken);
        return Ok(new { workOrder.OrderNo, operation.OperationCode, operation.Status, deviceCode = request.DeviceCode, lumengrid = new { status = "Queued", eventKey = delivery.EventKey } });
    }

    /// <summary>登记指定工序的良品与不良品报工，并按工艺路线执行质检门禁。</summary>
    [HttpPost("{orderNo}/reports")]
    public async Task<IActionResult> Report(string orderNo, ReportOperationRequest request, CancellationToken cancellationToken)
    {
        if (request.GoodQuantity < 0 || request.DefectQuantity < 0 || request.GoodQuantity + request.DefectQuantity == 0)
            return BadRequest(new { message = "报工数量必须为正数" });
        if (string.IsNullOrWhiteSpace(request.OperationCode))
            return BadRequest(new { message = "工序编码不能为空" });
        if (string.IsNullOrWhiteSpace(request.Workstation))
            return BadRequest(new { message = "工作站不能为空" });
        if (string.IsNullOrWhiteSpace(request.Operator))
            return BadRequest(new { message = "操作员不能为空" });
        var workOrder = await db.WorkOrders.Include(x => x.Operations).Include(x => x.Inspections).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (workOrder is null) return NotFound(new { message = "工单不存在" });
        if (workOrder.Status is WorkOrderStatus.Completed or WorkOrderStatus.Closed or WorkOrderStatus.Cancelled)
            return Conflict(new { message = $"工单当前状态为 {workOrder.Status}，不能继续报工" });
        if (workOrder.BatchNo.StartsWith("ERP-", StringComparison.OrdinalIgnoreCase)
            && (!workOrder.ErpStarted || !workOrder.ErpMaterialIssued))
            return Conflict(new { message = "ERP 工单必须先完成开工和领料后才能报工" });
        WorkOrderOperation? operation = null;
        var isFinalOperation = true;
        var operationInputQuantity = workOrder.PlannedQuantity;
        if (workOrder.Operations.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(request.OperationCode)) return BadRequest(new { message = "请选择工序后再报工" });
            operation = workOrder.Operations.OrderBy(x => x.Sequence).SingleOrDefault(x => x.OperationCode.Equals(request.OperationCode, StringComparison.OrdinalIgnoreCase));
            if (operation is null) return Conflict(new { message = "当前工单不存在该工序" });
            var current = workOrder.Operations.OrderBy(x => x.Sequence).FirstOrDefault(x => x.Status != "Completed");
            if (current?.Id != operation.Id) return Conflict(new { message = $"当前应报工序为 {current?.OperationName ?? "无"}，不能跳序报工" });
            var previous = workOrder.Operations.OrderBy(x => x.Sequence).LastOrDefault(x => x.Sequence < operation.Sequence);
            operationInputQuantity = previous?.ReportedGoodQuantity ?? workOrder.PlannedQuantity;
            var qualityGate = workOrder.Operations
                .Where(x => x.RequiresQuality && x.Sequence < operation.Sequence)
                .OrderByDescending(x => x.Sequence)
                .FirstOrDefault();
            if (qualityGate is not null && workOrder.Inspections
                    .Where(x => x.OperationCode == qualityGate.OperationCode)
                    .OrderByDescending(x => x.InspectedAt)
                    .FirstOrDefault()?.Result != InspectionResult.Passed)
                return Conflict(new { message = $"工序 {qualityGate.OperationName} 要求先完成合格质检，才能继续下一工序" });
            if (operation.ReportedGoodQuantity + operation.ReportedDefectQuantity + request.GoodQuantity + request.DefectQuantity > operationInputQuantity)
                return Conflict(new { message = "本工序报工数量不能超过前道工序合格数量" });
            isFinalOperation = operation.Id == workOrder.Operations.OrderByDescending(x => x.Sequence).First().Id;
        }
        else if (workOrder.CompletedQuantity + request.GoodQuantity > workOrder.PlannedQuantity)
            return Conflict(new { message = "报工良品数不能超过工单计划数" });

        if (operation is null)
        {
            workOrder.CompletedQuantity += request.GoodQuantity;
            workOrder.ReportedGoodQuantity += request.GoodQuantity;
            workOrder.ReportedDefectQuantity += request.DefectQuantity;
        }
        else
        {
            operation.Status = "InProgress";
            operation.ReportedGoodQuantity += request.GoodQuantity;
            operation.ReportedDefectQuantity += request.DefectQuantity;
            if (operation.ReportedGoodQuantity + operation.ReportedDefectQuantity >= operationInputQuantity) operation.Status = "Completed";
            if (isFinalOperation)
            {
                workOrder.CompletedQuantity = operation.ReportedGoodQuantity;
                workOrder.ReportedGoodQuantity = operation.ReportedGoodQuantity;
                workOrder.ReportedDefectQuantity = operation.ReportedDefectQuantity;
            }
        }
        var routeCompleted = operation is not null && workOrder.Operations.All(x => x.Status == "Completed");
        workOrder.Status = operation is null
            ? (workOrder.CompletedQuantity >= workOrder.PlannedQuantity ? WorkOrderStatus.Completed : WorkOrderStatus.InProgress)
            : (routeCompleted ? WorkOrderStatus.Completed : WorkOrderStatus.InProgress);
        if (workOrder.Status == WorkOrderStatus.Completed) workOrder.CompletedAt = DateTimeOffset.UtcNow;
        var report = new OperationReport
        {
            WorkOrderId = workOrder.Id, OperationCode = request.OperationCode, Workstation = request.Workstation,
            GoodQuantity = request.GoodQuantity, DefectQuantity = request.DefectQuantity, Operator = request.Operator
        };
        db.OperationReports.Add(report);
        db.TraceEvents.Add(new TraceEvent { BatchNo = workOrder.BatchNo, EventType = "OperationReported", EventData = $"{request.OperationCode}:{request.GoodQuantity}" });
        await db.SaveChangesAsync(cancellationToken);
        await NotifyBatchAsync(workOrder.BatchNo, "operationReported", workOrder.OrderNo, cancellationToken);

        object? lumengridSync = null;
        if (operation is not null && !string.IsNullOrWhiteSpace(request.DeviceCode))
        {
            var eventType = operation.Status == "Completed" ? "OperationCompleted" : "OperationStarted";
            var delivery = lumenGridDelivery.Enqueue(
                workOrder,
                operation,
                $"MES-EXECUTION-{report.Id}",
                eventType,
                request.DeviceCode,
                operation.Status == "Completed" ? "Completed" : "Running",
                report.ReportedAt);
            lumengridSync = new { status = "Queued", eventType, eventKey = delivery.EventKey };
        }
        if (lumengridSync is not null)
            await db.SaveChangesAsync(cancellationToken);

        var erpSyncStatus = "NotRequired";
        string? erpSyncError = null;
        string? erpEventKey = null;
        if (workOrder.BatchNo.StartsWith("ERP-", StringComparison.OrdinalIgnoreCase))
        {
            erpEventKey = $"MES-REPORT-{Guid.NewGuid():N}";
            var delivery = new ErpReportDelivery
            {
                EventKey = erpEventKey,
                WorkOrderId = workOrder.Id,
                OperationReportId = report.Id
            };
            db.ErpReportDeliveries.Add(delivery);
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                delivery.AttemptCount++;
                await erp.ReportProgressAsync(workOrder.OrderNo, new ErpProductionProgressRequest(
                    erpEventKey, report.OperationCode, report.Workstation, report.GoodQuantity,
                    report.DefectQuantity, report.Operator, report.ReportedAt), cancellationToken);
                delivery.Status = ErpReportDeliveryStatus.Sent;
                delivery.SentAt = DateTimeOffset.UtcNow;
                erpSyncStatus = "Sent";
            }
            catch (Exception ex)
            {
                delivery.Status = ErpReportDeliveryStatus.Failed;
                delivery.LastError = ex.Message;
                erpSyncStatus = "Failed";
                erpSyncError = ex.Message;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        var requiresErpReceipt = workOrder.BatchNo.StartsWith("ERP-", StringComparison.OrdinalIgnoreCase)
            && request.GoodQuantity > 0 && isFinalOperation;
        return Ok(new
        {
            workOrder.OrderNo,
            workOrder.Status,
            workOrder.CompletedQuantity,
            request.DefectQuantity,
            requiresErpReceipt,
            suggestedReceiptQuantity = requiresErpReceipt ? request.GoodQuantity : 0,
            erpSync = new { status = erpSyncStatus, eventKey = erpEventKey, error = erpSyncError },
            lumengridSync
        });
    }

    /// <summary>登记工序完成后的过程质检；质检要求由工艺路线配置决定。</summary>
    [HttpPost("{orderNo}/inspections")]
    public async Task<IActionResult> Inspect(string orderNo, CreateInspectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Checkpoint))
            return BadRequest(new { message = "检验项目不能为空" });
        if (string.IsNullOrWhiteSpace(request.Inspector))
            return BadRequest(new { message = "检验员不能为空" });
        if (request.Result == InspectionResult.Pending)
            return BadRequest(new { message = "质检结果不能为待检" });
        var workOrder = await db.WorkOrders.Include(x => x.Operations).Include(x => x.Inspections).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken);
        if (workOrder is null) return NotFound(new { message = "工单不存在" });
        if (workOrder.Status is WorkOrderStatus.Closed or WorkOrderStatus.Cancelled)
            return Conflict(new { message = $"工单当前状态为 {workOrder.Status}，不能登记质检" });
        var hasReportedQuantity = workOrder.ReportedGoodQuantity + workOrder.ReportedDefectQuantity > 0
            || workOrder.Operations.Any(x => x.ReportedGoodQuantity + x.ReportedDefectQuantity > 0)
            || await db.OperationReports.AnyAsync(x => x.WorkOrderId == workOrder.Id, cancellationToken);
        if (!hasReportedQuantity)
            return Conflict(new { message = "工单尚未报工，不能登记质检" });
        var qualityOperations = workOrder.Operations.Where(x => x.RequiresQuality).OrderBy(x => x.Sequence).ToList();
        var inspectionOperation = qualityOperations.FirstOrDefault(operation => operation.Status == "Completed"
            && workOrder.Inspections.Where(x => x.OperationCode == operation.OperationCode).OrderByDescending(x => x.InspectedAt).FirstOrDefault()?.Result != InspectionResult.Passed);
        var routeCompleted = workOrder.Operations.Count == 0
            ? workOrder.ReportedGoodQuantity + workOrder.ReportedDefectQuantity > 0 || await db.OperationReports.AnyAsync(x => x.WorkOrderId == workOrder.Id, cancellationToken)
            : workOrder.Operations.All(x => x.Status == "Completed");
        var finalOperation = workOrder.Operations.OrderByDescending(x => x.Sequence).FirstOrDefault();
        var finalInspectionDue = routeCompleted && finalOperation?.RequiresQuality != true;
        if (inspectionOperation is null && !finalInspectionDue)
            return Conflict(new { message = "质检仅在标记了质检要求的工序报工完成后执行；无工序质检点时，请在全部工序完成后做最终检验" });
        if (inspectionOperation is not null && !string.Equals(request.OperationCode, inspectionOperation.OperationCode, StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = $"当前待检工序为 {inspectionOperation.OperationName}（{inspectionOperation.OperationCode}）" });
        var inspectionOperationCode = inspectionOperation?.OperationCode ?? string.Empty;
        var isFinalInspection = inspectionOperation is null || inspectionOperation.Id == finalOperation?.Id;
        if (isFinalInspection && workOrder.Inspections.Where(x => x.OperationCode == inspectionOperationCode).OrderByDescending(x => x.InspectedAt).FirstOrDefault()?.Result == InspectionResult.Passed)
            return Conflict(new { message = "最终质检已通过，不能重复登记" });

        var inspection = new QualityInspection
        {
            WorkOrderId = workOrder.Id, OperationCode = inspectionOperationCode, Checkpoint = request.Checkpoint, Result = request.Result,
            Remark = request.Remark, Inspector = request.Inspector
        };
        if (isFinalInspection)
        {
            workOrder.ErpQualityStatus = request.Result.ToString();
            if (request.Result == InspectionResult.Passed && routeCompleted && workOrder.Status == WorkOrderStatus.InProgress)
            {
                workOrder.Status = WorkOrderStatus.Completed;
                workOrder.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
        db.QualityInspections.Add(inspection);
        db.TraceEvents.Add(new TraceEvent { BatchNo = workOrder.BatchNo, EventType = "QualityInspected", EventData = $"{request.Checkpoint}:{request.Result}" });
        await db.SaveChangesAsync(cancellationToken);
        await NotifyBatchAsync(workOrder.BatchNo, "qualityInspected", workOrder.OrderNo, cancellationToken);

        var erpSyncStatus = "NotRequired";
        string? erpSyncError = null;
        string? erpEventKey = null;
        if (isFinalInspection && workOrder.BatchNo.StartsWith("ERP-", StringComparison.OrdinalIgnoreCase))
        {
            erpEventKey = $"MES-QUALITY-{Guid.NewGuid():N}";
            var delivery = new ErpQualityDelivery
            {
                EventKey = erpEventKey,
                WorkOrderId = workOrder.Id,
                QualityInspectionId = inspection.Id
            };
            db.ErpQualityDeliveries.Add(delivery);
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                delivery.AttemptCount++;
                await erp.ReportQualityAsync(workOrder.OrderNo, new ErpProductionQualityRequest(
                    erpEventKey, inspection.Checkpoint, inspection.Result.ToString(), inspection.Remark,
                    inspection.Inspector, inspection.InspectedAt), cancellationToken);
                delivery.Status = ErpReportDeliveryStatus.Sent;
                delivery.SentAt = DateTimeOffset.UtcNow;
                erpSyncStatus = "Sent";
            }
            catch (Exception ex)
            {
                delivery.Status = ErpReportDeliveryStatus.Failed;
                delivery.LastError = ex.Message;
                erpSyncStatus = "Failed";
                erpSyncError = ex.Message;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { workOrder.OrderNo, workOrder.BatchNo, request.Checkpoint, request.Result, erpSync = new { status = erpSyncStatus, eventKey = erpEventKey, error = erpSyncError } });
    }

    private Task NotifyBatchAsync(string batchNo, string action, string orderNo, CancellationToken cancellationToken) =>
        hub.Clients.Group($"batch:{batchNo}").SendAsync("ProductionChanged", new { batchNo, orderNo, action, changedAt = DateTimeOffset.UtcNow }, cancellationToken);

    private static WorkOrderOperation? FindOperation(WorkOrder workOrder, string operationCode) =>
        workOrder.Operations.SingleOrDefault(x => x.OperationCode.Equals(operationCode, StringComparison.OrdinalIgnoreCase));

    private static ObjectResult ToLumenGridProblem(Exception exception)
    {
        var statusCode = exception is LumenGridIntegrationException remote && remote.StatusCode is >= 400 and < 500
            ? remote.StatusCode
            : StatusCodes.Status502BadGateway;
        return new ObjectResult(new { message = $"LumenGrid 对接失败：{exception.Message}" }) { StatusCode = statusCode };
    }
}
