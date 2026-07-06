using Erp.Application.Modularity;
using Erp.Modules.Payroll.Application.Features.Employees;
using Erp.Modules.Payroll.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Payroll.Api;

public sealed class PayrollErpModule : IErpModule
{
    public static readonly PayrollErpModule Instance = new();

    public string Name => "Payroll";

    public Type ControllersAnchorType => typeof(Controllers.PayrollController);

    public Type MediatRAnchorType => typeof(GetEmployeesHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddPayrollInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
