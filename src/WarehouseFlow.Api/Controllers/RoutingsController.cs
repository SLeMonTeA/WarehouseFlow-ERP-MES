using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/routings")]
[Authorize(Policy = PermissionCodes.PlanningOperate)]
public sealed class RoutingsController(WmsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await db.RoutingHeaders.AsNoTracking().Include(x => x.Product).Include(x => x.Operations).OrderBy(x => x.Product!.Sku).ThenByDescending(x => x.EffectiveFrom).Select(x => new
    {
        x.Id, productSku = x.Product!.Sku, productName = x.Product.Name, x.Code, x.Version, x.EffectiveFrom, x.IsDefault, x.IsActive,
        operations = x.Operations.OrderBy(o => o.Sequence).Select(o => new { o.Sequence, o.OperationCode, o.OperationName, o.WorkCenter, o.StandardMinutes, o.RequiresQuality })
    }).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoutingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Version)) return BadRequest(new { message = "路线编码和版本不能为空" });
        if (!ValidateOperations(request.Operations, out var message)) return BadRequest(new { message });
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.ProductSku && x.IsActive, cancellationToken);
        if (product is null) return NotFound(new { message = "成品物料不存在或已停用" });
        if (!await db.BomHeaders.AnyAsync(x => x.ProductId == product.Id && x.IsActive, cancellationToken)) return Conflict(new { message = "该物料没有有效 BOM，不能维护生产工艺路线" });
        if (await db.RoutingHeaders.AnyAsync(x => x.ProductId == product.Id && x.Code == request.Code && x.Version == request.Version, cancellationToken)) return Conflict(new { message = "工艺路线编码和版本已存在" });
        if (request.IsDefault) await db.RoutingHeaders.Where(x => x.ProductId == product.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
        var routing = new RoutingHeader { ProductId = product.Id, Code = request.Code.Trim(), Version = request.Version.Trim(), EffectiveFrom = request.EffectiveFrom, IsDefault = request.IsDefault, IsActive = true };
        routing.Operations.AddRange(request.Operations.OrderBy(x => x.Sequence).Select(ToEntity));
        db.RoutingHeaders.Add(routing);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/routings/{routing.Id}", new { routing.Id, routing.Code, routing.Version });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateRoutingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Version)) return BadRequest(new { message = "路线版本不能为空" });
        if (!ValidateOperations(request.Operations, out var message)) return BadRequest(new { message });
        var routing = await db.RoutingHeaders.Include(x => x.Operations).Include(x => x.Product).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (routing is null) return NotFound(new { message = "工艺路线不存在" });
        if (!await db.BomHeaders.AnyAsync(x => x.ProductId == routing.ProductId && x.IsActive, cancellationToken)) return Conflict(new { message = "该物料没有有效 BOM，不能维护生产工艺路线" });
        var isDefault = request.IsDefault && request.IsActive;
        if (isDefault) await db.RoutingHeaders.Where(x => x.ProductId == routing.ProductId && x.Id != id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
        db.RoutingOperations.RemoveRange(routing.Operations);
        routing.Version = request.Version.Trim(); routing.EffectiveFrom = request.EffectiveFrom; routing.IsDefault = isDefault; routing.IsActive = request.IsActive;
        routing.Operations = request.Operations.OrderBy(x => x.Sequence).Select(ToEntity).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { routing.Id, routing.Code, routing.Version, routing.IsActive });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Disable(int id, CancellationToken cancellationToken)
    {
        var routing = await db.RoutingHeaders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (routing is null) return NotFound(new { message = "工艺路线不存在" });
        routing.IsActive = false; routing.IsDefault = false;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { routing.Id, routing.IsActive });
    }

    private static RoutingOperation ToEntity(RoutingOperationRequest operation) => new() { Sequence = operation.Sequence, OperationCode = operation.OperationCode.Trim(), OperationName = operation.OperationName.Trim(), WorkCenter = operation.WorkCenter.Trim(), StandardMinutes = operation.StandardMinutes, RequiresQuality = operation.RequiresQuality };

    private static bool ValidateOperations(IReadOnlyList<RoutingOperationRequest>? operations, out string message)
    {
        message = string.Empty;
        if (operations is null || operations.Count == 0) { message = "工艺路线至少需要一道工序"; return false; }
        if (operations.Any(x => x.Sequence <= 0 || string.IsNullOrWhiteSpace(x.OperationCode) || string.IsNullOrWhiteSpace(x.OperationName))) { message = "工序顺序、编码和名称不能为空"; return false; }
        if (operations.Select(x => x.Sequence).Distinct().Count() != operations.Count || operations.Select(x => x.OperationCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != operations.Count) { message = "工序顺序和编码不能重复"; return false; }
        return true;
    }
}
