using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface IModelo111Reader
{
    Task<Modelo111Result> GetAsync(Guid tenantId, int year, int quarter, CancellationToken ct);
}
