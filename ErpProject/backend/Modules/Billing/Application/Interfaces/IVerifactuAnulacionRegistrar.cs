namespace Erp.Modules.Billing.Application.Interfaces;

/// <summary>
/// Registra anulación VERI*FACTU: huella encadenada, envío o conservación local.
/// </summary>
public interface IVerifactuAnulacionRegistrar
{
    /// <returns>false si la factura no existe.</returns>
    Task<bool> RegisterAsync(Guid invoiceId, CancellationToken ct = default);
}
