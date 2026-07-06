using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Application.Modularity;

public static class ErpModuleExtensions
{
    public static IMvcBuilder AddErpModuleControllers(
        this IMvcBuilder mvcBuilder,
        IErpModule module) =>
        mvcBuilder.AddApplicationPart(module.ControllersAnchorType.Assembly);

    public static IServiceCollection AddErpModule(
        this IServiceCollection services,
        IErpModule module,
        IConfiguration configuration)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(module.MediatRAnchorType.Assembly));
        module.RegisterAdditionalServices(services);
        module.RegisterInfrastructure(services, configuration);
        return services;
    }
}
