namespace Erp.Modules.Billing.Application.Interfaces;

/// <summary>Consulta la cadena VERI*FACTU (altas + anulaciones) por serie/ejercicio.</summary>
public interface IVerifactuChainQuery
{
    Task<string?> GetLastHuellaBeforeAsync(
        Guid companyId,
        string series,
        int fiscalYear,
        DateTime beforeUtc,
        CancellationToken ct = default);
}
