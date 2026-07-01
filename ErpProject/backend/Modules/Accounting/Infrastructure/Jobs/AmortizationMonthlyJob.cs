using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Infrastructure.Jobs;

/// <summary>
/// Hangfire job ejecutado el primer día de cada mes.
/// Genera asientos de dotación de amortización para todos los activos fijos activos
/// de todos los tenants, para el mes recién iniciado.
///
/// Registro: RecurringJob.AddOrUpdate("amortization-monthly", ..., Cron.Monthly())
/// </summary>
public class AmortizationMonthlyJob
{
    private readonly IAccountingDbContext _ctx;
    private readonly IMediator _mediator;
    private readonly ILogger<AmortizationMonthlyJob> _logger;

    public AmortizationMonthlyJob(
        IAccountingDbContext ctx,
        IMediator mediator,
        ILogger<AmortizationMonthlyJob> logger)
    {
        _ctx      = ctx;
        _mediator = mediator;
        _logger   = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        // El job corre el 1º de cada mes — amortizamos el mes anterior completo
        var now    = DateTime.UtcNow;
        var target = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var year   = target.Year;
        var month  = target.Month;

        _logger.LogInformation("AmortizationMonthlyJob: procesando {Year}-{Month:D2}", year, month);

        // Obtener todos los activos activos (sin filtro de tenant — job de sistema)
        var assets = await _ctx.FixedAssets
            .IgnoreQueryFilters()
            .Where(a => a.Status == "Active")
            .Select(a => a.Id)
            .ToListAsync(ct);

        var created = 0;
        var skipped = 0;

        foreach (var assetId in assets)
        {
            try
            {
                var entryId = await _mediator.Send(new PostMonthlyAmortizationCommand(assetId, year, month), ct);
                if (entryId.HasValue) created++;
                else skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error amortizando activo {AssetId} para {Year}-{Month:D2}", assetId, year, month);
            }
        }

        _logger.LogInformation(
            "AmortizationMonthlyJob completado: {Created} asientos creados, {Skipped} omitidos",
            created, skipped);
    }
}
