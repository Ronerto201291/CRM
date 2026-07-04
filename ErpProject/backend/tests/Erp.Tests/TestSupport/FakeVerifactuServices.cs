using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeVerifactuService : IVerifactuService
{
    public (string Huella, string QrUrl) Compute(
        string nifEmisor,
        string numSerieFactura,
        DateOnly fechaExpedicion,
        string tipoFactura,
        decimal cuotaTotal,
        decimal importeTotal,
        string? huellaAnterior,
        int numeroRegistro,
        DateTimeOffset fechaHoraHuella)
        => ($"huella-{numSerieFactura}", $"https://qr.test/{numSerieFactura}");
}

public sealed class FakeVerifactuSubmissionService : IVerifactuSubmissionService
{
    public Task<VerifactuSubmitResult> SubmitSingleAsync(string verifactuXml, bool useProd = false, CancellationToken ct = default)
        => Task.FromResult(new VerifactuSubmitResult(true, "Correcto", "OK"));
}

public sealed class FakeVerifactuSubmissionGateway : IVerifactuSubmissionGateway
{
    public List<Guid> EnqueuedInvoices { get; } = new();
    public List<Guid> EnqueuedAnulaciones { get; } = new();

    public void EnqueueVerifactuSubmission(Guid invoiceId) => EnqueuedInvoices.Add(invoiceId);
    public void EnqueueVerifactuAnulacion(Guid invoiceId) => EnqueuedAnulaciones.Add(invoiceId);
}

public sealed class FakeVerifactuModeSettings : IVerifactuModeSettings
{
    public bool RealtimeSubmissionEnabled { get; init; }
}
