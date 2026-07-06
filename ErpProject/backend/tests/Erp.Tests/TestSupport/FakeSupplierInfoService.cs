using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeSupplierInfoService : ISupplierInfoService
{
    private readonly Dictionary<Guid, SupplierInfoDto> _suppliers;

    public FakeSupplierInfoService(Dictionary<Guid, SupplierInfoDto>? suppliers = null)
    {
        _suppliers = suppliers ?? new Dictionary<Guid, SupplierInfoDto>();
    }

    public Task<SupplierInfoDto?> GetByIdAsync(Guid supplierId, CancellationToken ct = default)
        => Task.FromResult(_suppliers.GetValueOrDefault(supplierId));

    public Task<Dictionary<Guid, SupplierInfoDto>> GetByIdsAsync(IEnumerable<Guid> supplierIds, CancellationToken ct = default)
        => Task.FromResult(supplierIds.Where(_suppliers.ContainsKey).ToDictionary(id => id, id => _suppliers[id]));
}
