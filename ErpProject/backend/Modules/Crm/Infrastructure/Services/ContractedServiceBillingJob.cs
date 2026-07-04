using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Crm.Infrastructure.Services;

/// <summary>
/// Hangfire recurring job que revisa ClientContractedService con NextBillingDate
/// vencida y publica ClientServiceDueForBillingEvent por cada uno (Billing genera
/// la factura, ver GenerateRecurringServiceInvoiceHandler). Se registra en
/// Program.cs con: RecurringJob.AddOrUpdate&lt;ContractedServiceBillingJob&gt;(...)
/// Cron: diario a las 02:00 (UTC), tras ExpireQuotesJob (01:00).
///
/// Un fallo al publicar/facturar un contrato concreto no bloquea el resto; al no
/// avanzar NextBillingDate, ese contrato se reintenta automáticamente al día
/// siguiente (idempotente).
/// </summary>
public class ContractedServiceBillingJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractedServiceBillingJob> _logger;

    public ContractedServiceBillingJob(IServiceScopeFactory scopeFactory, ILogger<ContractedServiceBillingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var today = DateTime.UtcNow.Date;

        // Cross-tenant: revisar todas las empresas en una sola pasada, igual que ExpireQuotesJob.
        var due = await ctx.ClientContractedServices
            .IgnoreQueryFilters()
            .Where(cs => cs.Status == "Active" && cs.NextBillingDate.Date <= today)
            .ToListAsync();

        if (due.Count == 0)
        {
            _logger.LogDebug("ContractedServiceBillingJob: no contracted services due for billing.");
            return;
        }

        var processed = 0;
        foreach (var contract in due)
        {
            try
            {
                // Scope propio por contrato: ITenantContext se fija para esta empresa
                // (mismo patrón que ApiKeyRateLimitMiddleware.SetTenant fuera de un
                // request HTTP), así CreateInvoiceCommand y el resto de handlers de
                // este scope resuelven el CompanyId correcto vía query filters.
                using var tenantScope = _scopeFactory.CreateScope();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenant(contract.CompanyId, "ContractedServiceBillingJob");
                var scopedPublisher = tenantScope.ServiceProvider.GetRequiredService<IPublisher>();

                await scopedPublisher.Publish(new ClientServiceDueForBillingEvent
                {
                    ClientContractedServiceId = contract.Id,
                    ClientId = contract.ClientId,
                    CompanyId = contract.CompanyId,
                    ServiceName = contract.ServiceName,
                    Price = contract.Price,
                    TaxRate = contract.TaxRate,
                    PeriodStart = contract.NextBillingDate,
                });
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ContractedServiceBillingJob: failed to bill contracted service {ContractId} (Company: {CompanyId}); will retry next run.",
                    contract.Id, contract.CompanyId);
            }
        }

        _logger.LogInformation("ContractedServiceBillingJob: processed {Processed}/{Total} due contracted services.",
            processed, due.Count);
    }
}
