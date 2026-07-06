namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Abstracción cross-módulo para consultar datos básicos de un proveedor.
/// Implementada en Crm.Infrastructure para evitar acoplamiento directo entre módulos.
/// </summary>
public interface ISupplierInfoService
{
    Task<SupplierInfoDto?> GetByIdAsync(Guid supplierId, CancellationToken ct = default);

    /// <summary>
    /// Batch lookup: returns a dictionary keyed by SupplierId.
    /// </summary>
    Task<Dictionary<Guid, SupplierInfoDto>> GetByIdsAsync(IEnumerable<Guid> supplierIds, CancellationToken ct = default);
}

public record SupplierInfoDto(
    string Name,
    string TaxId,
    string Email,
    string Address);
