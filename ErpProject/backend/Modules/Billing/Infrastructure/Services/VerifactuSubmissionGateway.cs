using Erp.Modules.Billing.Application.Interfaces;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Concrete implementation of IVerifactuSubmissionGateway.
/// Bridges Application layer (which must not reference Hangfire) to
/// VerifactuSubmissionJob in the same Infrastructure assembly.
/// </summary>
public class VerifactuSubmissionGateway : IVerifactuSubmissionGateway
{
    public void EnqueueVerifactuSubmission(Guid invoiceId) =>
        VerifactuSubmissionJob.Enqueue(invoiceId);

    public void EnqueueVerifactuAnulacion(Guid invoiceId) =>
        VerifactuSubmissionJob.EnqueueAnulacion(invoiceId);
}