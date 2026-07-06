namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Generates VERI*FACTU XML for AEAT TIKE registration (RD 1007/2023).
/// Implementation lives in Billing.Infrastructure.
/// </summary>
public interface IVerifactuXmlGenerator
{
    Task<string> GenerateRegistroAsync(Guid companyId, int year, int month, CancellationToken ct = default);
    Task<string> GenerateSingleInvoiceRegistroAsync(Guid invoiceId, CancellationToken ct = default);
    Task<string> GenerateAnulacionRegistroAsync(Guid invoiceId, CancellationToken ct = default);
}
