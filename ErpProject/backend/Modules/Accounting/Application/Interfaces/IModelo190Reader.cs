using Erp.Modules.Accounting.Application.Features.Export;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface IModelo190Reader
{
    Task<Modelo190Result> GetAsync(Guid tenantId, int year, CancellationToken ct);
}
