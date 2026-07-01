using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Accounting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountingModule(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<InvoiceApprovedEventHandler>());

        services.AddScoped<AccountingService>();

        return services;
    }
}
