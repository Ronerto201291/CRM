namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Pasarela de pago de facturas (ADR-0018 #39) — distinta de
/// <see cref="ISubscriptionBillingService"/>, que cobra la suscripción SaaS del propio
/// tenant. Este gateway crea un cobro puntual (Stripe Checkout en modo "payment") por el
/// importe exacto de una factura concreta, iniciado desde el portal público del cliente.
/// </summary>
public interface IInvoicePaymentGateway
{
    Task<string> CreateInvoiceCheckoutSessionAsync(
        Guid invoiceId, string invoiceNumber, decimal amount, string currency,
        string successUrl, string cancelUrl, CancellationToken ct = default);
}
