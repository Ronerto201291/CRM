using System.IO.Compression;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.AccountantExport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Infrastructure.Jobs;

/// <summary>
/// Envío periódico ZIP a gestoría externa (ADR-0018 #42e). Hangfire: día 3 de cada mes.
/// </summary>
public class AccountantExportJob(IServiceProvider services, ILogger<AccountantExportJob> log)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var appCtx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var companies = await appCtx.Companies.IgnoreQueryFilters()
            .Where(c => c.IsActive
                        && c.AccountantExportFrequency != "disabled"
                        && !string.IsNullOrEmpty(c.AccountantEmail))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var company in companies)
        {
            if (!ShouldRun(company.AccountantExportFrequency, now, company.AccountantExportLastRunAt))
                continue;

            try
            {
                tenant.SetTenant(company.Id, company.Name);
                var prevMonth = now.AddMonths(-1);
                await mediator.Send(new ExportAccountantPackageCommand(
                    prevMonth.Year,
                    prevMonth.Month,
                    null,
                    SendEmail: true), ct);
                log.LogInformation("AccountantExportJob: paquete enviado para empresa {CompanyId}.", company.Id);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "AccountantExportJob falló para empresa {CompanyId}.", company.Id);
            }
        }
    }

    public static bool ShouldRun(string frequency, DateTime now, DateTime? lastRun)
    {
        if (frequency == "monthly")
            return lastRun is null || lastRun.Value.Month != now.Month || lastRun.Value.Year != now.Year;

        if (frequency == "quarterly")
        {
            var q = (now.Month - 1) / 3;
            return lastRun is null
                   || ((lastRun.Value.Month - 1) / 3) != q
                   || lastRun.Value.Year != now.Year;
        }

        return false;
    }
}
