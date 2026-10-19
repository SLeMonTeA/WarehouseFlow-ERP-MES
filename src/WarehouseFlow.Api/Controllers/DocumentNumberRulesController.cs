using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Application;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/document-number-rules")]
[Authorize(Policy = PermissionCodes.MasterDataManage)]
public sealed class DocumentNumberRulesController(WmsDbContext db, DocumentNumberService numbers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.DocumentNumberRules.AsNoTracking().OrderBy(x => x.Code).ToListAsync(cancellationToken));

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, UpdateDocumentNumberRuleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prefix) || string.IsNullOrWhiteSpace(request.DateFormat) || request.SequenceLength is < 1 or > 12)
            return BadRequest(new { message = "前缀、日期格式不能为空，流水长度必须在 1-12 位之间" });
        try { _ = DateTimeOffset.Now.ToString(request.DateFormat); } catch (FormatException) { return BadRequest(new { message = "日期格式无效，例如 yyyyMMdd" }); }
        var rule = await db.DocumentNumberRules.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (rule is null) return NotFound(new { message = "单号规则不存在" });
        rule.Name = request.Name.Trim(); rule.Prefix = request.Prefix.Trim(); rule.DateFormat = request.DateFormat.Trim(); rule.Separator = request.Separator ?? string.Empty; rule.SequenceLength = request.SequenceLength; rule.IsActive = request.IsActive; rule.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(rule);
    }

    [HttpPost("{code}/preview")]
    public async Task<IActionResult> Preview(string code, CancellationToken cancellationToken)
    {
        var rule = await db.DocumentNumberRules.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (rule is null) return NotFound(new { message = "单号规则不存在" });
        var sample = await numbers.NextAsync(code, cancellationToken);
        return Ok(new { code, sample, message = "预览已占用一个流水号，正式建单不会重复使用" });
    }
}
