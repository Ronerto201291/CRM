namespace Erp.Modules.Billing.Application.Interfaces;

public interface IFacturaEService
{
    /// <summary>
    /// Generates a FacturaE 3.2.2 XML document for the given locked invoice.
    /// Required by Ley 18/2022 Crea y Crece for B2B electronic invoicing.
    /// Returns the UTF-8 XML bytes and suggested filename.
    /// </summary>
    Task<(byte[] XmlBytes, string FileName)> GenerateAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Genera FacturaE firmado con XAdES-BES si hay certificado Sii:CertPath configurado.</summary>
    Task<(byte[] XmlBytes, string FileName)> GenerateSignedAsync(Guid invoiceId, Guid tenantId, CancellationToken ct = default);
}
