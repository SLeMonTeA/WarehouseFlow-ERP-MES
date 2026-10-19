using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Controllers;

[ApiController]
[Route("api/master-data")]
[Authorize(Policy = PermissionCodes.MasterDataManage)]
public sealed class MasterDataController(WmsDbContext db) : ControllerBase
{
    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Warehouses.AsNoTracking().Include(x => x.Locations).AsQueryable();
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Code)
            .Select(x => new { x.Code, x.Name, x.Address, x.IsActive, locations = x.Locations.Select(l => new { l.Code, l.Zone, l.Capacity, l.IsActive }) })
            .ToListAsync(cancellationToken));
    }

    [HttpGet("locations")]
    public async Task<IActionResult> Locations([FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Locations.AsNoTracking().Include(x => x.Warehouse).AsQueryable();
        if (activeOnly) query = query.Where(x => x.IsActive && x.Warehouse!.IsActive);
        return Ok(await query.OrderBy(x => x.Warehouse!.Code).ThenBy(x => x.Code)
            .Select(x => new { warehouseCode = x.Warehouse!.Code, x.Code, x.Zone, x.Capacity, x.IsActive })
            .ToListAsync(cancellationToken));
    }

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] string? keyword, [FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword) || x.Contact.Contains(keyword));
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Code).Select(x => new { x.Code, x.Name, x.Contact, x.Phone, x.Address, x.IsActive, x.CreatedAt }).ToListAsync(cancellationToken));
    }

    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers([FromQuery] string? keyword, [FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword) || x.Contact.Contains(keyword));
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Code).Select(x => new { x.Code, x.Name, x.Contact, x.Phone, x.Address, x.IsActive, x.CreatedAt }).ToListAsync(cancellationToken));
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] string? keyword, [FromQuery] bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword)) query = query.Where(x => x.Sku.Contains(keyword) || x.Name.Contains(keyword) || x.Barcode.Contains(keyword));
        if (activeOnly) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Sku).Select(x => new { x.Sku, x.Barcode, x.Name, x.Category, x.Specification, x.Unit, x.SafetyStock, x.MaxStock, x.IsActive }).ToListAsync(cancellationToken));
    }

    [Authorize(Policy = PermissionCodes.MasterDataManage)]
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(CreateProductRequest request, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(x => x.Sku == request.Sku || x.Barcode == request.Barcode, cancellationToken))
            return Conflict(new { message = "SKU 或条码已存在" });
        var product = new Product { Sku = request.Sku, Barcode = request.Barcode, Name = request.Name, Category = request.Category, Specification = request.Specification, Unit = request.Unit, SafetyStock = request.SafetyStock, MaxStock = request.MaxStock };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/master-data/products/{product.Sku}", product);
    }

    [HttpPut("products/{sku}")]
    public async Task<IActionResult> UpdateProduct(string sku, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == sku, cancellationToken);
        if (product is null) return NotFound(new { message = "物料不存在" });
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Unit)) return BadRequest(new { message = "物料名称和单位不能为空" });
        if (!string.Equals(product.Barcode, request.Barcode, StringComparison.OrdinalIgnoreCase) && await db.Products.AnyAsync(x => x.Id != product.Id && x.Barcode == request.Barcode, cancellationToken))
            return Conflict(new { message = "条码已存在" });
        product.Barcode = request.Barcode.Trim(); product.Name = request.Name.Trim(); product.Category = request.Category.Trim(); product.Specification = request.Specification.Trim(); product.Unit = request.Unit.Trim(); product.SafetyStock = request.SafetyStock; product.MaxStock = request.MaxStock; product.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(product);
    }

    [HttpDelete("products/{sku}")]
    public Task<IActionResult> DisableProduct(string sku, CancellationToken cancellationToken) => SetProductActive(sku, false, cancellationToken);

    [HttpPost("products/{sku}/activate")]
    public Task<IActionResult> ActivateProduct(string sku, CancellationToken cancellationToken) => SetProductActive(sku, true, cancellationToken);

    private async Task<IActionResult> SetProductActive(string sku, bool active, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == sku, cancellationToken);
        if (product is null) return NotFound(new { message = "物料不存在" });
        product.IsActive = active;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { product.Sku, product.IsActive });
    }

    [Authorize(Policy = PermissionCodes.MasterDataManage)]
    [HttpPost("warehouses")]
    public async Task<IActionResult> CreateWarehouse(CreateWarehouseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "仓库编码和名称不能为空" });
        if (await db.Warehouses.AnyAsync(x => x.Code == request.Code, cancellationToken)) return Conflict(new { message = "仓库编码已存在" });
        var warehouse = new Warehouse { Code = request.Code, Name = request.Name, Address = request.Address };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/master-data/warehouses/{warehouse.Code}", warehouse);
    }

    [HttpPut("warehouses/{code}")]
    public async Task<IActionResult> UpdateWarehouse(string code, UpdateWarehouseRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (warehouse is null) return NotFound(new { message = "仓库不存在" });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "仓库名称不能为空" });
        warehouse.Name = request.Name.Trim(); warehouse.Address = request.Address.Trim(); warehouse.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(warehouse);
    }

    [HttpDelete("warehouses/{code}")]
    public Task<IActionResult> DisableWarehouse(string code, CancellationToken cancellationToken) => SetWarehouseActive(code, false, cancellationToken);

    [HttpPost("warehouses/{code}/activate")]
    public Task<IActionResult> ActivateWarehouse(string code, CancellationToken cancellationToken) => SetWarehouseActive(code, true, cancellationToken);

    private async Task<IActionResult> SetWarehouseActive(string code, bool active, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (warehouse is null) return NotFound(new { message = "仓库不存在" });
        warehouse.IsActive = active;
        if (!active) await db.Locations.Where(x => x.WarehouseId == warehouse.Id).ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { warehouse.Code, warehouse.IsActive });
    }

    [Authorize(Policy = PermissionCodes.MasterDataManage)]
    [HttpPost("locations")]
    public async Task<IActionResult> CreateLocation(CreateLocationRequest request, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.SingleOrDefaultAsync(x => x.Code == request.WarehouseCode, cancellationToken);
        if (warehouse is null) return NotFound(new { message = "仓库不存在" });
        if (await db.Locations.AnyAsync(x => x.WarehouseId == warehouse.Id && x.Code == request.Code, cancellationToken))
            return Conflict(new { message = "库位已存在" });
        var location = new WarehouseLocation { WarehouseId = warehouse.Id, Code = request.Code, Zone = request.Zone, Capacity = request.Capacity };
        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/master-data/locations/{location.Code}", new { location.Code, warehouseCode = request.WarehouseCode, location.Zone, location.Capacity });
    }

    [HttpPut("locations/{warehouseCode}/{code}")]
    public async Task<IActionResult> UpdateLocation(string warehouseCode, string code, UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.Include(x => x.Warehouse).SingleOrDefaultAsync(x => x.Warehouse!.Code == warehouseCode && x.Code == code, cancellationToken);
        if (location is null) return NotFound(new { message = "库位不存在" });
        location.Zone = request.Zone.Trim(); location.Capacity = request.Capacity; location.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { location.Code, warehouseCode, location.Zone, location.Capacity, location.IsActive });
    }

    [HttpDelete("locations/{warehouseCode}/{code}")]
    public Task<IActionResult> DisableLocation(string warehouseCode, string code, CancellationToken cancellationToken) => SetLocationActive(warehouseCode, code, false, cancellationToken);

    [HttpPost("locations/{warehouseCode}/{code}/activate")]
    public Task<IActionResult> ActivateLocation(string warehouseCode, string code, CancellationToken cancellationToken) => SetLocationActive(warehouseCode, code, true, cancellationToken);

    private async Task<IActionResult> SetLocationActive(string warehouseCode, string code, bool active, CancellationToken cancellationToken)
    {
        var location = await db.Locations.Include(x => x.Warehouse).SingleOrDefaultAsync(x => x.Warehouse!.Code == warehouseCode && x.Code == code, cancellationToken);
        if (location is null) return NotFound(new { message = "库位不存在" });
        if (active && location.Warehouse is { IsActive: false }) return Conflict(new { message = "所属仓库已停用，不能单独启用库位" });
        location.IsActive = active;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { location.Code, warehouseCode, location.IsActive });
    }

    [Authorize(Policy = PermissionCodes.MasterDataManage)]
    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "客户编码和名称不能为空" });
        if (await db.Customers.AnyAsync(x => x.Code == request.Code, cancellationToken)) return Conflict(new { message = "客户编码已存在" });
        var customer = new Customer { Code = request.Code.Trim(), Name = request.Name.Trim(), Contact = request.Contact.Trim(), Phone = request.Phone.Trim(), Address = request.Address.Trim() };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/master-data/customers/{customer.Code}", customer);
    }

    [Authorize(Policy = PermissionCodes.MasterDataManage)]
    [HttpPut("customers/{code}")]
    public async Task<IActionResult> UpdateCustomer(string code, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (customer is null) return NotFound(new { message = "客户不存在" });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "客户名称不能为空" });
        customer.Name = request.Name.Trim(); customer.Contact = request.Contact.Trim(); customer.Phone = request.Phone.Trim(); customer.Address = request.Address.Trim(); customer.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(customer);
    }

    [Authorize(Policy = PermissionCodes.MasterDataManage)]
    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "供应商编码和名称不能为空" });
        if (await db.Suppliers.AnyAsync(x => x.Code == request.Code, cancellationToken)) return Conflict(new { message = "供应商编码已存在" });
        var supplier = new Supplier { Code = request.Code.Trim(), Name = request.Name.Trim(), Contact = request.Contact.Trim(), Phone = request.Phone.Trim(), Address = request.Address.Trim() };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/master-data/suppliers/{supplier.Code}", supplier);
    }

    [Authorize(Policy = PermissionCodes.MasterDataManage)]
    [HttpPut("suppliers/{code}")]
    public async Task<IActionResult> UpdateSupplier(string code, UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (supplier is null) return NotFound(new { message = "供应商不存在" });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "供应商名称不能为空" });
        supplier.Name = request.Name.Trim(); supplier.Contact = request.Contact.Trim(); supplier.Phone = request.Phone.Trim(); supplier.Address = request.Address.Trim(); supplier.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(supplier);
    }
}
