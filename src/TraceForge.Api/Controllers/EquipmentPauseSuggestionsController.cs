using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Contracts;
using TraceForge.Api.Domain;
using TraceForge.Api.Infrastructure;

namespace TraceForge.Api.Controllers;

[ApiController]
[Route("api/integration/equipment-pause-suggestions")]
public sealed class EquipmentPauseSuggestionsController(
    TraceForgeDbContext db,
    LumenGridExecutionDeliveryService lumenGridDelivery) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipmentPauseSuggestionResponse>>> List(
        [FromQuery] EquipmentPauseSuggestionStatus? status = null,
        [FromQuery] string? deviceCode = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = db.EquipmentPauseSuggestions.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(deviceCode)) query = query.Where(x => x.DeviceCode == deviceCode.Trim());
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        return Ok(rows.Select(ToResponse).ToList());
    }

    [HttpPost("{suggestionKey}/accept")]
    public async Task<IActionResult> Accept(string suggestionKey, EquipmentPauseDecisionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Operator)) return BadRequest(new { message = "操作员不能为空" });
        var suggestion = await db.EquipmentPauseSuggestions.SingleOrDefaultAsync(x => x.SuggestionKey == suggestionKey, cancellationToken);
        if (suggestion is null) return NotFound(new { message = "暂停建议不存在" });
        if (suggestion.Status != EquipmentPauseSuggestionStatus.Pending)
            return Conflict(new { message = $"暂停建议当前状态为 {suggestion.Status}，不能重复处理" });

        var workOrder = await db.WorkOrders.Include(x => x.Operations)
            .SingleOrDefaultAsync(x => x.OrderNo == suggestion.WorkOrderNo, cancellationToken);
        if (workOrder is null) return Conflict(new { message = "建议关联的工单不存在，无法暂停" });
        var operation = workOrder.Operations.SingleOrDefault(x => x.OperationCode.Equals(suggestion.OperationCode, StringComparison.OrdinalIgnoreCase));
        if (operation is null) return Conflict(new { message = "建议关联的工序不存在，无法暂停" });
        if (operation.Status != "InProgress")
            return Conflict(new { message = $"关联工序当前状态为 {operation.Status}，只有执行中的工序可以暂停" });

        var now = DateTimeOffset.UtcNow;
        var eventKey = $"MES-PAUSE-SUGGESTION-{Guid.NewGuid():N}";
        operation.Status = "Paused";
        suggestion.Status = EquipmentPauseSuggestionStatus.Accepted;
        suggestion.DecidedAt = now;
        suggestion.DecidedBy = request.Operator.Trim();
        suggestion.DecisionRemark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        var delivery = lumenGridDelivery.Enqueue(workOrder, operation, eventKey, "OperationPaused", suggestion.DeviceCode, "Paused", now);
        db.TraceEvents.Add(new TraceEvent
        {
            BatchNo = workOrder.BatchNo,
            EventType = "EquipmentPauseSuggestionAccepted",
            EventData = $"{suggestion.SuggestionKey}:{request.Operator.Trim()}"
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            suggestion = ToResponse(suggestion),
            workOrderNo = workOrder.OrderNo,
            operationCode = operation.OperationCode,
            lumengrid = new { status = "Queued", eventKey = delivery.EventKey }
        });
    }

    [HttpPost("{suggestionKey}/reject")]
    public async Task<IActionResult> Reject(string suggestionKey, EquipmentPauseDecisionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Operator)) return BadRequest(new { message = "操作员不能为空" });
        var suggestion = await db.EquipmentPauseSuggestions.SingleOrDefaultAsync(x => x.SuggestionKey == suggestionKey, cancellationToken);
        if (suggestion is null) return NotFound(new { message = "暂停建议不存在" });
        if (suggestion.Status != EquipmentPauseSuggestionStatus.Pending)
            return Conflict(new { message = $"暂停建议当前状态为 {suggestion.Status}，不能重复处理" });

        suggestion.Status = EquipmentPauseSuggestionStatus.Rejected;
        suggestion.DecidedAt = DateTimeOffset.UtcNow;
        suggestion.DecidedBy = request.Operator.Trim();
        suggestion.DecisionRemark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        db.TraceEvents.Add(new TraceEvent
        {
            BatchNo = $"EQUIPMENT-{suggestion.DeviceCode}",
            EventType = "EquipmentPauseSuggestionRejected",
            EventData = $"{suggestion.SuggestionKey}:{request.Operator.Trim()}"
        });
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { suggestion = ToResponse(suggestion) });
    }

    private static EquipmentPauseSuggestionResponse ToResponse(EquipmentPauseSuggestion row) => new(
        row.Id, row.SuggestionKey, row.EventKey, row.DeviceCode, row.WorkCenter,
        row.WorkOrderNo, row.OperationCode, row.Reason, row.Status.ToString(),
        row.CreatedAt, row.DecidedAt, row.DecidedBy, row.DecisionRemark);
}
