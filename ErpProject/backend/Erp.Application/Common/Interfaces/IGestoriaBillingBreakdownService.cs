namespace Erp.Application.Common.Interfaces;

public sealed record GestoriaCompanyBillingLine(Guid CompanyId, string Name, string TaxId);

public sealed record GestoriaInvoiceLineDto(
    Guid CompanyId,
    string CompanyName,
    string TaxId,
    decimal AmountEur,
    string? Description);

public interface IGestoriaBillingBreakdownService
{
    Task<IReadOnlyList<GestoriaCompanyBillingLine>> GetCompaniesForBillingAccountAsync(
        Guid billingCompanyId, CancellationToken ct = default);
}
