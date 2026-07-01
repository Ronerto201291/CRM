using Erp.Modules.Billing.Application.Interfaces;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IVerifactuSubmissionGateway.
/// Bridges Application layer (which must not reference Hangfire) to
/// VerifactuSubmissionJob in the same Infrastructure assembly.
/// </summary>
public class VerifactuSubmissionGateway : IVerifactuSubmissionGateway
{
    public void EnqueueVerifactuSubmission(Guid invoiceId)
    {
        // Static method on the Hangfire job — resolved via DI is not needed here
        // because BackgroundJob.Enqueue works with the type directly.
        VerifactuSubmissionJob.Enqueue(invoiceId);
    }
}