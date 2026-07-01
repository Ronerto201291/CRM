namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Abstracción cross-módulo para consultar datos básicos de un cliente.
/// Implementada en Crm.Infrastructure para evitar acoplamiento directo entre módulos.
/// </summary>
public interface IClientInfoService
{
    Task<ClientInfoDto?> GetByIdAsync(Guid clientId, CancellationToken ct = default);

    /// <summary>
    /// Batch lookup: returns a dictionary keyed by ClientId.
    /// More efficient than calling GetByIdAsync in a loop.
    /// </summary>
    Task<Dictionary<Guid, ClientInfoDto>> GetByIdsAsync(IEnumerable<Guid> clientIds, CancellationToken ct = default);
}

public record ClientInfoDto(
    string Name,
    string TaxId,
    string Email,
    string Address);
