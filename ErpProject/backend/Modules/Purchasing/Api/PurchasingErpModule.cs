using Erp.Application.Modularity;
using Erp.Modules.Purchasing.Application.Features.Receipts.Handlers;
using Erp.Modules.Purchasing.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Purchasing.Api;

public sealed class PurchasingErpModule : IErpModule
{
    public static readonly PurchasingErpModule Instance = new();

    public string Name => "Purchasing";

    public Type ControllersAnchorType => typeof(Controllers.ReceiptsController);

    public Type MediatRAnchorType => typeof(CreateGoodsReceiptHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddPurchasingInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
