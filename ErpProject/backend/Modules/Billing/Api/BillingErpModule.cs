using Erp.Application.Modularity;
using Erp.Modules.Billing.Application.Handlers;
using Erp.Modules.Billing.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Billing.Api;

public sealed class BillingErpModule : IErpModule
{
    public static readonly BillingErpModule Instance = new();

    public string Name => "Billing";

    public Type ControllersAnchorType => typeof(Controllers.InvoicesController);

    public Type MediatRAnchorType => typeof(GetInvoicesByStatusHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddBillingInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
