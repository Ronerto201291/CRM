using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Infrastructure.Services;

/// <summary>
/// Implementación de IClientInfoService.
/// Permite a otros módulos (ej. Billing) consultar datos de cliente sin acoplamiento directo a ICrmDbContext.
/// </summary>
public class ClientInfoService : IClientInfoService
{
    private readonly ICrmDbContext _ctx;

    public ClientInfoService(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<ClientInfoDto?> GetByIdAsync(Guid clientId, CancellationToken ct = default)
    {
        return await _ctx.Clients
            .Where(c => c.Id == clientId && !c.IsAnonymized)
            .Select(c => new ClientInfoDto(c.Name, c.TaxId, c.Email, c.Address))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Dictionary<Guid, ClientInfoDto>> GetByIdsAsync(
        IEnumerable<Guid> clientIds, CancellationToken ct = default)
    {
        var ids = clientIds.Distinct().ToList();
        return await _ctx.Clients
            .Where(c => ids.Contains(c.Id) && !c.IsAnonymized)
            .Select(c => new { c.Id, Info = new ClientInfoDto(c.Name, c.TaxId, c.Email, c.Address) })
            .ToDictionaryAsync(x => x.Id, x => x.Info, ct);
    }
}
