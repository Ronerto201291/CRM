namespace Erp.Application.Common.Interfaces;

public interface ISubscriptionBillingService
{
    Task<string> CreateCheckoutSessionAsync(
        Guid companyId, string planName, string successUrl, string cancelUrl, CancellationToken ct = default);

    Task<string> CreatePortalSessionAsync(Guid companyId, string returnUrl, CancellationToken ct = default);

    Task<IReadOnlyList<SubscriptionInvoiceDto>> ListInvoicesAsync(Guid companyId, CancellationToken ct = default);
}

public sealed record SubscriptionInvoiceDto(
    string Id,
    DateTime Date,
    decimal AmountEur,
    string? Currency,
    string? Status,
    string? PdfUrl,
    string? Description);
