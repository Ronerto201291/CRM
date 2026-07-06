using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Domain.Entities;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Erp.Infrastructure.Services;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Hangfire job: envío VERI*FACTU (alta y anulación) con auditoría en BD.
/// </summary>
public class VerifactuSubmissionJob
{
    private readonly IBillingDbContext _billing;
    private readonly IVerifactuXmlGenerator _xmlGenerator;
    private readonly IVerifactuSubmissionService _submissionService;
    private readonly ILogger<VerifactuSubmissionJob> _log;
    private readonly bool _useProduction;

    public VerifactuSubmissionJob(
        IBillingDbContext billing,
        IVerifactuXmlGenerator xmlGenerator,
        IVerifactuSubmissionService submissionService,
        ILogger<VerifactuSubmissionJob> log,
        IOptions<VerifactuOptions> verifactuOpts)
    {
        _billing = billing;
        _xmlGenerator = xmlGenerator;
        _submissionService = submissionService;
        _log = log;
        _useProduction = verifactuOpts.Value.UseProduction;
    }

    public static void Enqueue(Guid invoiceId) =>
        BackgroundJob.Enqueue<VerifactuSubmissionJob>(j => j.SubmitAltaAsync(invoiceId, CancellationToken.None));

    public static void EnqueueAnulacion(Guid invoiceId) =>
        BackgroundJob.Enqueue<VerifactuSubmissionJob>(j => j.SubmitAnulacionAsync(invoiceId, CancellationToken.None));

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 120 })]
    public Task SubmitAltaAsync(Guid invoiceId, CancellationToken ct) =>
        SubmitCoreAsync(invoiceId, "Alta", _xmlGenerator.GenerateSingleInvoiceRegistroAsync, ct);

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 120 })]
    public Task SubmitAnulacionAsync(Guid invoiceId, CancellationToken ct) =>
        SubmitCoreAsync(invoiceId, "Anulacion", _xmlGenerator.GenerateAnulacionRegistroAsync, ct);

    private async Task SubmitCoreAsync(
        Guid invoiceId,
        string submissionType,
        Func<Guid, CancellationToken, Task<string>> xmlFactory,
        CancellationToken ct)
    {
        var inv = await _billing.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (inv == null)
        {
            _log.LogWarning("VerifactuSubmissionJob: invoice {InvoiceId} not found", invoiceId);
            return;
        }

        if (string.IsNullOrEmpty(inv.VerifactuHuella))
        {
            _log.LogInformation("VerifactuSubmissionJob: invoice {Number} sin huella, omitiendo", inv.Number);
            return;
        }

        var isLocalOnly = !inv.VerifactuRealtimeSubmission;
        if (submissionType == "Anulacion" && string.IsNullOrEmpty(inv.VerifactuAnulacionHuella))
        {
            _log.LogWarning("VerifactuSubmissionJob: factura {Number} sin huella de anulación", inv.Number);
            return;
        }

        _log.LogInformation(
            "VerifactuSubmissionJob: {Type} factura {Number} (localOnly={Local})",
            submissionType, inv.Number, isLocalOnly);

        try
        {
            var xml = await xmlFactory(invoiceId, ct);

            if (isLocalOnly)
            {
                _billing.VerifactuSubmissionLogs.Add(new VerifactuSubmissionLog
                {
                    Id = Guid.NewGuid(),
                    CompanyId = inv.CompanyId,
                    InvoiceId = inv.Id,
                    InvoiceNumber = inv.Number,
                    SubmissionType = submissionType,
                    EstadoEnvio = "ConservacionLocal",
                    Success = true,
                    IsProduction = false,
                    RawResponse = xml.Length > 4000 ? xml[..4000] : xml,
                    SubmittedAt = DateTime.UtcNow
                });
                await _billing.SaveChangesAsync(ct);
                _log.LogInformation(
                    "VerifactuSubmissionJob: {Type} {Number} archivado localmente (RRSIF)",
                    submissionType, inv.Number);
                return;
            }

            var result = await _submissionService.SubmitSingleAsync(xml, _useProduction, ct);
            var success = result.Success || result.EstadoEnvio == "AceptadoConErrores";

            _billing.VerifactuSubmissionLogs.Add(new VerifactuSubmissionLog
            {
                Id = Guid.NewGuid(),
                CompanyId = inv.CompanyId,
                InvoiceId = inv.Id,
                InvoiceNumber = inv.Number,
                SubmissionType = submissionType,
                EstadoEnvio = result.EstadoEnvio,
                Success = success,
                IsProduction = _useProduction,
                RawResponse = result.RawResponse?.Length > 4000
                    ? result.RawResponse[..4000]
                    : result.RawResponse,
                SubmittedAt = DateTime.UtcNow
            });

            if (success && submissionType == "Alta")
                inv.VerifactuSubmittedAt = DateTime.UtcNow;
            if (success && submissionType == "Anulacion")
                inv.VerifactuAnulacionSubmittedAt = DateTime.UtcNow;

            await _billing.SaveChangesAsync(ct);

            if (success)
                _log.LogInformation("VerifactuSubmissionJob: {Type} {Number} aceptado ({Estado})",
                    submissionType, inv.Number, result.EstadoEnvio);
            else
                _log.LogWarning("VerifactuSubmissionJob: {Type} {Number} rechazado ({Estado})",
                    submissionType, inv.Number, result.EstadoEnvio);
        }
        catch (Exception ex)
        {
            _billing.VerifactuSubmissionLogs.Add(new VerifactuSubmissionLog
            {
                Id = Guid.NewGuid(),
                CompanyId = inv.CompanyId,
                InvoiceId = inv.Id,
                InvoiceNumber = inv.Number,
                SubmissionType = submissionType,
                EstadoEnvio = "Exception",
                Success = false,
                IsProduction = _useProduction,
                RawResponse = ex.Message,
                SubmittedAt = DateTime.UtcNow
            });
            await _billing.SaveChangesAsync(ct);
            _log.LogError(ex, "VerifactuSubmissionJob: fallo {Type} factura {Number}", submissionType, inv.Number);
            throw;
        }
    }
}
