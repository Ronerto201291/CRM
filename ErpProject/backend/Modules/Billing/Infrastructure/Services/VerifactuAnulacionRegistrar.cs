using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Billing.Infrastructure.Services;

public class VerifactuAnulacionRegistrar : IVerifactuAnulacionRegistrar
{
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;
    private readonly IVerifactuService _verifactu;
    private readonly IVerifactuSubmissionGateway _gateway;
    private readonly ILogger<VerifactuAnulacionRegistrar> _log;

    public VerifactuAnulacionRegistrar(
        IBillingDbContext billing,
        IApplicationDbContext app,
        IVerifactuService verifactu,
        IVerifactuSubmissionGateway gateway,
        ILogger<VerifactuAnulacionRegistrar> log)
    {
        _billing = billing;
        _app = app;
        _verifactu = verifactu;
        _gateway = gateway;
        _log = log;
    }

    public async Task<bool> RegisterAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var inv = await _billing.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (inv is null) return false;

        if (string.IsNullOrEmpty(inv.VerifactuHuella))
            throw new InvalidOperationException("La factura no tiene huella VeriFactu — no se puede anular.");

        if (!string.IsNullOrEmpty(inv.VerifactuAnulacionHuella))
        {
            _log.LogInformation("Verifactu anulación ya registrada para {Number}", inv.Number);
            return true;
        }

        var company = await _app.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Id == inv.CompanyId)
            .Select(c => new { c.TaxId })
            .FirstOrDefaultAsync(ct);

        if (company is null || string.IsNullOrEmpty(company.TaxId))
            throw new InvalidOperationException("Empresa sin NIF — no se puede registrar anulación VeriFactu.");

        var anulacionAt = DateTimeOffset.UtcNow;
        var previous = await VerifactuChainHelper.GetLastEntryBeforeAsync(
            _billing, inv.CompanyId, inv.Series, inv.FiscalYear, anulacionAt.UtcDateTime, ct);

        inv.VerifactuAnulacionHuella = _verifactu.ComputeAnulacionHuella(
            company.TaxId,
            inv.Number,
            DateOnly.FromDateTime(inv.IssueDate),
            previous?.Huella,
            anulacionAt);
        inv.VerifactuAnulacionAt = anulacionAt.UtcDateTime;

        await _billing.SaveChangesAsync(ct);

        _gateway.EnqueueVerifactuAnulacion(inv.Id);
        _log.LogInformation(
            "Verifactu anulación registrada para {Number} (encadenada a {Prev})",
            inv.Number,
            previous?.NumSerieFactura ?? "primer registro");

        return true;
    }
}
