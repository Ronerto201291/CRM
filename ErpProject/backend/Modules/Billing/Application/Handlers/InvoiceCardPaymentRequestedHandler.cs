using Erp.Application.Common.Events;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using MediatR;

namespace Erp.Modules.Billing.Application.Handlers;

/// <summary>
/// Marca factura pagada cuando un TPV registra cobro con tarjeta (ADR-0018 #41).
/// </summary>
public class InvoiceCardPaymentRequestedHandler(IMediator mediator)
    : INotificationHandler<InvoiceCardPaymentRequestedEvent>
{
    public async Task Handle(InvoiceCardPaymentRequestedEvent notification, CancellationToken ct)
    {
        var paid = await mediator.Send(new MarkPaidCommand
        {
            Id = notification.InvoiceId,
            PaymentMethod = "card",
        }, ct);

        if (!paid)
            throw new InvalidOperationException($"Factura {notification.InvoiceId} no encontrada al registrar cobro TPV.");
    }
}
