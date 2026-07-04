using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

/// <summary>
/// Graba la última llamada de creación de sesión de checkout y devuelve una URL fija,
/// sin llamar a la API real de Stripe (mismo espíritu que FakeMediator/FakePublisher).
/// </summary>
public sealed class FakeInvoicePaymentGateway : IInvoicePaymentGateway
{
    public string CheckoutUrlToReturn { get; set; } = "https://checkout.stripe.test/session/cs_test_123";

    public Guid? LastInvoiceId { get; private set; }
    public string? LastInvoiceNumber { get; private set; }
    public decimal? LastAmount { get; private set; }
    public string? LastCurrency { get; private set; }
    public string? LastSuccessUrl { get; private set; }
    public string? LastCancelUrl { get; private set; }

    public Task<string> CreateInvoiceCheckoutSessionAsync(
        Guid invoiceId, string invoiceNumber, decimal amount, string currency,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        LastInvoiceId = invoiceId;
        LastInvoiceNumber = invoiceNumber;
        LastAmount = amount;
        LastCurrency = currency;
        LastSuccessUrl = successUrl;
        LastCancelUrl = cancelUrl;
        return Task.FromResult(CheckoutUrlToReturn);
    }
}
