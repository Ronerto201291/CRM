using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Application.Modularity;

/// <summary>
/// Contrato de auto-registro modular (ADR-0018 #19e).
/// Cada módulo expone ensamblados y cableado DI desde su propia capa Api.
/// </summary>
public interface IErpModule
{
    string Name { get; }

    Type ControllersAnchorType { get; }

    Type MediatRAnchorType { get; }

    void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration);

    void RegisterAdditionalServices(IServiceCollection services);
}
