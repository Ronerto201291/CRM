using MediatR;
namespace Erp.Modules.Billing.Application.Features.Billing.Commands;
public class LockInvoiceCommand : IRequest<bool> { public Guid Id { get; set; } }
public class MarkPaidCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    /// <summary>"bank" | "cash" | "card" | "transfer" (default: "bank")</summary>
    public string PaymentMethod { get; set; } = "bank";
}
