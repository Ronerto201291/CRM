namespace Erp.Application.Common.Interfaces;

/// <summary>Puente OCR gastos → CRM sin acoplar Expenses a ICrmDbContext (ADR-0018 #13).</summary>
public interface IExpenseOcrCrmBridge
{
    Task<ExpenseOcrSupplierResult?> FindOrCreateSupplierAsync(
        Guid companyId, string taxId, string? name, CancellationToken ct = default);

    Task LogOcrActivityAsync(
        Guid companyId, string entityType, Guid entityId, string action, string description,
        CancellationToken ct = default);
}

public record ExpenseOcrSupplierResult(Guid SupplierId, string SupplierName);
