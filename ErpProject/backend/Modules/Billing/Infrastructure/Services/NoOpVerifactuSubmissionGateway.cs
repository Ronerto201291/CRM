using Erp.Modules.Billing.Application.Interfaces;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Gateway VeriFactu sin Hangfire — usado en entorno IntegrationTests.
/// </summary>
public sealed class NoOpVerifactuSubmissionGateway : IVerifactuSubmissionGateway
{
    public void EnqueueVerifactuSubmission(Guid invoiceId) { }
    public void EnqueueVerifactuAnulacion(Guid invoiceId) { }
}
