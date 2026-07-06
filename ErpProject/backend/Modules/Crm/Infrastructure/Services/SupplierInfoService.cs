using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Infrastructure.Services;

/// <summary>
/// Implementación de ISupplierInfoService.
/// Permite a otros módulos (ej. Purchasing) consultar datos de proveedor sin acoplamiento a ICrmDbContext.
/// </summary>
public class SupplierInfoService : ISupplierInfoService
{
    private readonly ICrmDbContext _ctx;

    public SupplierInfoService(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<SupplierInfoDto?> GetByIdAsync(Guid supplierId, CancellationToken ct = default)
    {
        return await _ctx.Suppliers
            .Where(s => s.Id == supplierId && !s.IsAnonymized && s.IsActive)
            .Select(s => new SupplierInfoDto(s.Name, s.TaxId, s.Email, s.Address))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Dictionary<Guid, SupplierInfoDto>> GetByIdsAsync(
        IEnumerable<Guid> supplierIds, CancellationToken ct = default)
    {
        var ids = supplierIds.Distinct().ToList();
        return await _ctx.Suppliers
            .Where(s => ids.Contains(s.Id) && !s.IsAnonymized && s.IsActive)
            .Select(s => new { s.Id, Info = new SupplierInfoDto(s.Name, s.TaxId, s.Email, s.Address) })
            .ToDictionaryAsync(x => x.Id, x => x.Info, ct);
    }
}
