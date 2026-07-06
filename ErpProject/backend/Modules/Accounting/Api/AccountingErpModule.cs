using Erp.Application.Modularity;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Services;
using Erp.Modules.Accounting.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Accounting.Api;

/// <summary>Auto-registro del módulo Accounting (ADR-0018 #19e).</summary>
public sealed class AccountingErpModule : IErpModule
{
    public static readonly AccountingErpModule Instance = new();

    public string Name => "Accounting";

    public Type ControllersAnchorType => typeof(Controllers.AccountingController);

    public Type MediatRAnchorType => typeof(InvoiceApprovedEventHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddAccountingInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) =>
        services.AddScoped<AccountingService>();
}
