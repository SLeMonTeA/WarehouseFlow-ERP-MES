using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Application.Partners;

public sealed class PartnerLookupService(WmsDbContext db)
{
    public async Task<(string Code, string Name)> ResolveCustomerAsync(string? code, string fallbackName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) return (string.Empty, fallbackName.Trim());
        var customer = await db.Customers.SingleOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new KeyNotFoundException("客户编码不存在");
        if (!customer.IsActive) throw new InvalidOperationException("客户已停用");
        return (customer.Code, customer.Name);
    }

    public async Task<(string Code, string Name)> ResolveSupplierAsync(string? code, string fallbackName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) return (string.Empty, fallbackName.Trim());
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Code == code, cancellationToken)
            ?? throw new KeyNotFoundException("供应商编码不存在");
        if (!supplier.IsActive) throw new InvalidOperationException("供应商已停用");
        return (supplier.Code, supplier.Name);
    }
}
