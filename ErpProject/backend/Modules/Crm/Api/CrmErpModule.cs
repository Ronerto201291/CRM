using Erp.Application.Modularity;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Crm.Api;

public sealed class CrmErpModule : IErpModule
{
    public static readonly CrmErpModule Instance = new();

    public string Name => "Crm";

    public Type ControllersAnchorType => typeof(Controllers.ClientsController);

    public Type MediatRAnchorType => typeof(GetClientsHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddCrmInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
