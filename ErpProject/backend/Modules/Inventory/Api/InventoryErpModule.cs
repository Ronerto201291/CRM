using Erp.Application.Modularity;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Inventory.Api;

public sealed class InventoryErpModule : IErpModule
{
    public static readonly InventoryErpModule Instance = new();

    public string Name => "Inventory";

    public Type ControllersAnchorType => typeof(Controllers.ProductsController);

    public Type MediatRAnchorType => typeof(GetWarehousesHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddInventoryInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
