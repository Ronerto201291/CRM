using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Infrastructure.Jobs;

/// <summary>
/// Hangfire job ejecutado el primer día de cada mes.
/// Reconoce la cuota mensual de todas las periodificaciones activas (480/485)
/// de todos los tenants.
///
/// Registro: RecurringJob.AddOrUpdate("deferred-entry-monthly", ..., Cron.Monthly())
/// </summary>
public class DeferredEntryMonthlyJob
{
    private readonly IAccountingDbContext _ctx;
    private readonly IMediator _mediator;
    private readonly ILogger<DeferredEntryMonthlyJob> _logger;

    public DeferredEntryMonthlyJob(
        IAccountingDbContext ctx,
        IMediator mediator,
        ILogger<DeferredEntryMonthlyJob> logger)
    {
        _ctx      = ctx;
        _mediator = mediator;
        _logger   = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(CancellationToken ct = default)
    {
        // El job corre el 1º de cada mes — reconocemos el mes que acaba de comenzar
        var now   = DateTime.UtcNow;
        var year  = now.Year;
        var month = now.Month;

        _logger.LogInformation("DeferredEntryMonthlyJob: procesando {Year}-{Month:D2}", year, month);

        // Todas las periodificaciones activas cuyo rango cubre el mes actual
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var entries = await _ctx.DeferredEntries
            .IgnoreQueryFilters()
            .Where(d => d.Status == "Active"
                     && d.PeriodStart <= monthStart
                     && d.PeriodEnd   >= monthStart)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var created = 0;
        var skipped = 0;

        foreach (var entryId in entries)
        {
            try
            {
                var journalId = await _mediator.Send(
                    new RecognizeDeferredEntryMonthCommand(entryId, year, month), ct);
                if (journalId.HasValue) created++;
                else skipped++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error reconociendo periodificación {EntryId} para {Year}-{Month:D2}",
                    entryId, year, month);
            }
        }

        _logger.LogInformation(
            "DeferredEntryMonthlyJob completado: {Created} asientos creados, {Skipped} omitidos",
            created, skipped);
    }
}
