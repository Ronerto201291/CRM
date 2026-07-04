using Erp.Application.Common.Events;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Handlers;

/// <summary>
/// Consume el evento publicado por StripeService (core) cuando se completa un pago
/// puntual de factura (ADR-0018 #39). Reenvía a MarkPaidCommand en vez de duplicar la
/// lógica de "marcar pagada + publicar PaymentReceivedEvent" — MarkPaidHandler ya es
/// idempotente y ahora usa IgnoreQueryFilters() precisamente para poder invocarse desde
/// este contexto sin tenant resuelto.
/// </summary>
public class MarkInvoicePaidFromStripeHandler : INotificationHandler<StripeInvoiceCheckoutCompletedEvent>
{
    private readonly IMediator _mediator;

    public MarkInvoicePaidFromStripeHandler(IMediator mediator) => _mediator = mediator;

    public Task Handle(StripeInvoiceCheckoutCompletedEvent notification, CancellationToken ct)
        => _mediator.Send(new MarkPaidCommand { Id = notification.InvoiceId, PaymentMethod = "card" }, ct);
}
