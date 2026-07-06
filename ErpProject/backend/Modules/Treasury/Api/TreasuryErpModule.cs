using Erp.Application.Modularity;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Treasury.Api;

public sealed class TreasuryErpModule : IErpModule
{
    public static readonly TreasuryErpModule Instance = new();

    public string Name => "Treasury";

    public Type ControllersAnchorType => typeof(Controllers.TreasuryController);

    public Type MediatRAnchorType => typeof(CreateBankAccountHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddTreasuryInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
