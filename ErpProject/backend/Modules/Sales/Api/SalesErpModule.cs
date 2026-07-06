using Erp.Application.Modularity;
using Erp.Modules.Sales.Application.Features.Deliveries.Handlers;
using Erp.Modules.Sales.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Sales.Api;

public sealed class SalesErpModule : IErpModule
{
    public static readonly SalesErpModule Instance = new();

    public string Name => "Sales";

    public Type ControllersAnchorType => typeof(Controllers.SalesOrdersController);

    public Type MediatRAnchorType => typeof(CreateDeliveryNoteHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddSalesInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
