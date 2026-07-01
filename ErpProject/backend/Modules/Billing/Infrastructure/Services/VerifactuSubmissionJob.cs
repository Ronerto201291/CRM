using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Hangfire job que envía el XML VERI*FACTU de una factura bloqueada al registro TIKE de AEAT.
/// Reemplaza el Task.Run fire-and-forget en LockInvoiceHandler.
///
/// El envío es asíncrono: si falla se reintenta hasta 3 veces con backoff exponencial.
/// La factura queda bloqueada independientemente del resultado del envío — el log es la fuente de verdad.
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
        _billing           = billing;
        _xmlGenerator     = xmlGenerator;
        _submissionService = submissionService;
        _log               = log;
        _useProduction     = verifactuOpts.Value.UseProduction;
    }

    /// <summary>
    /// Encola el envío de VERI*FACTU para una factura. Llamado desde LockInvoiceHandler
    /// en lugar del antiguo Task.Run fire-and-forget.
    /// </summary>
    public static void Enqueue(Guid invoiceId)
    {
        BackgroundJob.Enqueue<VerifactuSubmissionJob>(
            j => j.SubmitAsync(invoiceId, CancellationToken.None));
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 10, 30, 120 })]
    public async Task SubmitAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var inv = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);

        if (inv == null)
        {
            _log.LogWarning("VerifactuSubmissionJob: invoice {InvoiceId} not found", invoiceId);
            return;
        }

        if (string.IsNullOrEmpty(inv.VerifactuHuella))
        {
            _log.LogInformation("VerifactuSubmissionJob: invoice {Number} has no VerifactuHuella, skipping", inv.Number);
            return;
        }

        _log.LogInformation(
            "VerifactuSubmissionJob: submitting invoice {Number} (Id={InvoiceId}) to AEAT TIKE",
            inv.Number, invoiceId);

        try
        {
            var xml = await _xmlGenerator.GenerateRegistroAsync(
                inv.CompanyId,
                inv.IssueDate.Year,
                inv.IssueDate.Month,
                ct);

            var result = await _submissionService.SubmitSingleAsync(xml, _useProduction, ct);

            if (result.Success)
            {
                _log.LogInformation(
                    "VerifactuSubmissionJob: invoice {Number} accepted by AEAT (EstadoEnvio={Estado})",
                    inv.Number, result.EstadoEnvio);

                // Marcar la factura como enviada exitosamente para evitar reenvíos
                inv.VerifactuSubmittedAt = DateTime.UtcNow;
                await _billing.SaveChangesAsync(ct);
            }
            else
            {
                _log.LogWarning(
                    "VerifactuSubmissionJob: invoice {Number} AEAT returned {Estado}: {Raw}",
                    inv.Number, result.EstadoEnvio, result.RawResponse);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "VerifactuSubmissionJob: failed to submit invoice {Number} to AEAT TIKE",
                inv.Number);
            throw;
        }
    }
}
