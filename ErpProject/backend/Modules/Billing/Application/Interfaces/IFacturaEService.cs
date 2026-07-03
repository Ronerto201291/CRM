namespace Erp.Modules.Billing.Application.Interfaces;

public interface IFacturaEService
{
    Task<(byte[] XmlBytes, string FileName)> GenerateAsync(
        Guid invoiceId, Guid tenantId, CancellationToken ct = default);

    Task<(byte[] XmlBytes, string FileName)> GenerateSignedAsync(
        Guid invoiceId, Guid tenantId, CancellationToken ct = default);
}
