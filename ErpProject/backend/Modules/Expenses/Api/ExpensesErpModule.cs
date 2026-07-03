using Erp.Application.Modularity;
using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Expenses.Api;

public sealed class ExpensesErpModule : IErpModule
{
    public static readonly ExpensesErpModule Instance = new();

    public string Name => "Expenses";

    public Type ControllersAnchorType => typeof(Controllers.ExpensesController);

    public Type MediatRAnchorType => typeof(GetExpenseUploadsHandler);

    public void RegisterInfrastructure(IServiceCollection services, IConfiguration configuration) =>
        services.AddExpensesInfrastructure(configuration);

    public void RegisterAdditionalServices(IServiceCollection services) { }
}
