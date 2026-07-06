using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeClientInfoService : IClientInfoService
{
    private readonly Dictionary<Guid, ClientInfoDto> _clients;

    public FakeClientInfoService(Dictionary<Guid, ClientInfoDto>? clients = null)
    {
        _clients = clients ?? new Dictionary<Guid, ClientInfoDto>();
    }

    public Task<ClientInfoDto?> GetByIdAsync(Guid clientId, CancellationToken ct = default)
        => Task.FromResult(_clients.GetValueOrDefault(clientId));

    public Task<Dictionary<Guid, ClientInfoDto>> GetByIdsAsync(IEnumerable<Guid> clientIds, CancellationToken ct = default)
        => Task.FromResult(clientIds.Where(_clients.ContainsKey).ToDictionary(id => id, id => _clients[id]));
}
