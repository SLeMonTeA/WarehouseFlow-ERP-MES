using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Contracts;
using TraceForge.Api.Domain;
using TraceForge.Api.Infrastructure;

namespace TraceForge.Api.Controllers;

[ApiController]
[Route("api/integration/lumengrid")]
public sealed class LumenGridIntegrationController(LumenGridClient client, TraceForgeDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("equipment-availability")]
    public async Task<IActionResult> EquipmentAvailability(
        [FromQuery] string? workCenter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.GetEquipmentAvailabilityAsync(workCenter, cancellationToken);
            return result is null ? StatusCode(502, new { message = "LumenGrid 未返回设备可用性" }) : Ok(result);
        }
        catch (LumenGridIntegrationException ex)
        {
            return StatusCode(502, new { message = ex.Message });
        }
    }

    [HttpGet("equipment-events")]
    public async Task<IActionResult> EquipmentEvents(
        [FromQuery] long afterId = 0,
        [FromQuery] string? deviceCode = null,
        [FromQuery] string? workCenter = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.GetEquipmentEventsAsync(afterId, deviceCode, workCenter, limit, cancellationToken);
            return result is null ? StatusCode(502, new { message = "LumenGrid 未返回设备事件" }) : Ok(result);
        }
        catch (LumenGridIntegrationException ex)
        {
            return StatusCode(502, new { message = ex.Message });
        }
    }

    [HttpGet("execution-bindings")]
    public async Task<IActionResult> ExecutionBindings(
        [FromQuery] string? deviceCode = null,
        [FromQuery] string? workOrderNo = null,
        [FromQuery] string? workCenter = null,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.GetExecutionBindingsAsync(deviceCode, workOrderNo, workCenter, activeOnly, limit, cancellationToken);
            return result is null ? StatusCode(502, new { message = "LumenGrid 未返回执行绑定" }) : Ok(result);
        }
        catch (LumenGridIntegrationException ex)
        {
            return StatusCode(502, new { message = ex.Message });
        }
    }

    [HttpPost("execution-bindings")]
    public async Task<IActionResult> PublishExecutionBinding(
        LumenGridExecutionBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await client.PublishExecutionBindingAsync(request, cancellationToken);
            return result is null ? StatusCode(502, new { message = "LumenGrid 未返回执行绑定结果" }) : Ok(result);
        }
        catch (LumenGridIntegrationException ex)
        {
            return StatusCode(ex.StatusCode is >= 400 and < 500 ? ex.StatusCode : 502, new { message = ex.Message });
        }
    }

    [HttpPost("equipment-notifications")]
    public async Task<IActionResult> ReceiveEquipmentNotification(
        LumenGridEquipmentNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!HasValidIntegrationKey())
            return Unauthorized(new { message = "缺少或无效的 LumenGrid 集成调用密钥" });
        if (string.IsNullOrWhiteSpace(request.EventKey) || string.IsNullOrWhiteSpace(request.EventId) || string.IsNullOrWhiteSpace(request.DeviceCode))
            return BadRequest(new { message = "设备通知必须包含 EventKey、EventId 和 DeviceCode" });

        var duplicate = await db.LumenGridEquipmentNotifications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventKey == request.EventKey, cancellationToken);
        if (duplicate is not null)
            return Ok(new { accepted = true, duplicate = true, eventKey = duplicate.EventKey, receivedAt = duplicate.ReceivedAt });

        var notification = new LumenGridEquipmentNotification
        {
            EventKey = request.EventKey.Trim(),
            EventId = request.EventId.Trim(),
            EventType = request.EventType.Trim(),
            DeviceCode = request.DeviceCode.Trim(),
            WorkCenter = request.WorkCenter.Trim(),
            EquipmentStatus = request.EquipmentStatus.Trim(),
            IsOnline = request.IsOnline,
            IsAvailable = request.IsAvailable,
            CanReceiveProduction = request.CanReceiveProduction,
            OccurredAt = request.OccurredAt,
            Summary = request.Summary.Trim(),
            Payload = request.Payload,
            ReceivedAt = DateTimeOffset.UtcNow
        };
        db.LumenGridEquipmentNotifications.Add(notification);
        db.TraceEvents.Add(new TraceEvent
        {
            BatchNo = $"EQUIPMENT-{notification.DeviceCode}",
            EventType = "LumenGridEquipmentAvailabilityChanged",
            EventData = $"{notification.EventType}: {notification.EquipmentStatus}, 可用={notification.IsAvailable}, {notification.Summary}",
            OccurredAt = notification.OccurredAt
        });
        if (!notification.IsAvailable)
        {
            var activeBinding = await db.LumenGridExecutionDeliveries.AsNoTracking()
                .Where(x => x.DeviceCode == notification.DeviceCode
                    && x.EventType == "OperationStarted"
                    && x.BindingStatus == "Running"
                    && x.Status != LumenGridDeliveryStatus.Failed)
                .OrderByDescending(x => x.OccurredAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (activeBinding is not null && !await db.EquipmentPauseSuggestions.AnyAsync(x => x.EventKey == notification.EventKey, cancellationToken))
            {
                var suggestion = new EquipmentPauseSuggestion
                {
                    SuggestionKey = $"PAUSE-SUGGESTION-{notification.EventKey}",
                    EventKey = notification.EventKey,
                    DeviceCode = notification.DeviceCode,
                    WorkCenter = notification.WorkCenter,
                    WorkOrderNo = activeBinding.WorkOrderNo,
                    OperationCode = activeBinding.OperationCode,
                    Reason = $"设备 {notification.DeviceCode} 当前不可用：{notification.Summary}"
                };
                db.EquipmentPauseSuggestions.Add(suggestion);
                db.TraceEvents.Add(new TraceEvent
                {
                    BatchNo = $"EQUIPMENT-{notification.DeviceCode}",
                    EventType = "EquipmentPauseSuggested",
                    EventData = $"{suggestion.SuggestionKey}:{suggestion.WorkOrderNo}/{suggestion.OperationCode}"
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        var createdSuggestion = await db.EquipmentPauseSuggestions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventKey == notification.EventKey, cancellationToken);
        return Ok(new
        {
            accepted = true,
            duplicate = false,
            eventKey = notification.EventKey,
            receivedAt = notification.ReceivedAt,
            pauseSuggestion = createdSuggestion is null ? null : new { createdSuggestion.SuggestionKey, createdSuggestion.Status, createdSuggestion.WorkOrderNo, createdSuggestion.OperationCode }
        });
    }

    [HttpGet("equipment-notifications")]
    public async Task<IActionResult> ListEquipmentNotifications(
        [FromQuery] string? deviceCode = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = db.LumenGridEquipmentNotifications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(deviceCode))
            query = query.Where(x => x.DeviceCode == deviceCode);
        var rows = await query.OrderByDescending(x => x.ReceivedAt).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new
        {
            x.Id,
            x.EventKey,
            x.EventId,
            x.EventType,
            x.DeviceCode,
            x.WorkCenter,
            x.EquipmentStatus,
            x.IsOnline,
            x.IsAvailable,
            x.CanReceiveProduction,
            x.OccurredAt,
            x.Summary,
            x.ReceivedAt
        }));
    }

    private bool HasValidIntegrationKey()
    {
        var expected = configuration["Integration:SharedKey"];
        var provided = Request.Headers["X-Integration-Key"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(expected) &&
               !string.IsNullOrWhiteSpace(provided) &&
               string.Equals(expected, provided, StringComparison.Ordinal);
    }
}
