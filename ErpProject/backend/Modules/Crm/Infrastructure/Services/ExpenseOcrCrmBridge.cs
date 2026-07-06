using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Infrastructure.Services;

public sealed class ExpenseOcrCrmBridge : IExpenseOcrCrmBridge
{
    private readonly ICrmDbContext _crm;

    public ExpenseOcrCrmBridge(ICrmDbContext crm) => _crm = crm;

    public async Task<ExpenseOcrSupplierResult?> FindOrCreateSupplierAsync(
        Guid companyId, string taxId, string? name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return null;

        var existing = await _crm.Suppliers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.TaxId == taxId, ct);

        if (existing is not null)
            return new ExpenseOcrSupplierResult(existing.Id, existing.Name);

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = name ?? $"Proveedor {taxId}",
            TaxId = taxId
        };
        _crm.Suppliers.Add(supplier);
        await LogOcrActivityAsync(companyId, "Supplier", supplier.Id, "AutoCreated",
            $"Proveedor creado automáticamente desde OCR: {supplier.TaxId}", ct);
        await _crm.SaveChangesAsync(ct);

        return new ExpenseOcrSupplierResult(supplier.Id, supplier.Name);
    }

    public async Task LogOcrActivityAsync(
        Guid companyId, string entityType, Guid entityId, string action, string description,
        CancellationToken ct = default)
    {
        _crm.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Description = description
        });
        await _crm.SaveChangesAsync(ct);
    }
}
