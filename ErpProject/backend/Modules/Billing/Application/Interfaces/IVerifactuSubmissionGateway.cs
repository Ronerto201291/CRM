namespace Erp.Modules.Billing.Application.Interfaces;

/// <summary>
/// Abstraction for enqueueing VerifactuSubmissionJob from Application layer.
/// Prevents Billing.Application from referencing Hangfire/Billing.Infrastructure directly.
/// </summary>
public interface IVerifactuSubmissionGateway
{
    void EnqueueVerifactuSubmission(Guid invoiceId);
}