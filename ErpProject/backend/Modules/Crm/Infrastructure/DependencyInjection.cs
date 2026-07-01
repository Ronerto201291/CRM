using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Modules.Crm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCrmInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection missing.");

        services.AddDbContext<CrmDbContext>(options =>
            options.UseNpgsql(connectionString)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<ICrmDbContext>(p => p.GetRequiredService<CrmDbContext>());
        services.AddScoped<IClientInfoService, ClientInfoService>();

        return services;
    }
}
