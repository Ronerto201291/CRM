using System.Text.Json;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Hangfire recurring job que marca como Expired los presupuestos enviados
/// cuya fecha de validez (ValidUntil) ha pasado.
/// Se registra en Program.cs con: RecurringJob.AddOrUpdate&lt;ExpireQuotesJob&gt;(...)
/// Cron: diario a las 01:00 (UTC)
/// </summary>
public class ExpireQuotesJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpireQuotesJob> _logger;

    public ExpireQuotesJob(IServiceScopeFactory scopeFactory, ILogger<ExpireQuotesJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var today = DateTime.UtcNow.Date;

        // Buscar presupuestos enviados vencidos — ignorar filtros de tenant para procesar todos
        var expiring = await ctx.Quotes
            .IgnoreQueryFilters()
            .Include(q => q.StatusHistory)
            .Where(q => q.Status == "Sent" && q.ValidUntil.Date < today)
            .ToListAsync();

        if (expiring.Count == 0)
        {
            _logger.LogDebug("ExpireQuotesJob: no quotes to expire.");
            return;
        }

        var metadata = JsonSerializer.Serialize(new
        {
            triggeredBy = nameof(ExpireQuotesJob),
            runAt = DateTime.UtcNow
        });

        foreach (var quote in expiring)
        {
            var prev = quote.Status;
            quote.Status = "Expired";
            quote.StatusHistory.Add(new QuoteStatusHistory
            {
                FromStatus = prev,
                ToStatus = "Expired",
                ChangedAt = DateTime.UtcNow,
                Reason = "Fecha de validez superada",
                Metadata = metadata
            });
        }

        await ctx.SaveChangesAsync();

        _logger.LogInformation("ExpireQuotesJob: expired {Count} quotes (ValidUntil < {Today}).",
            expiring.Count, today);
    }
}
