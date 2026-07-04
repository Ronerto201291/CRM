using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Tenancy;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Modules.Crm.Infrastructure.Services;
using Erp.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Crm;

public class ContractedServiceBillingJobTests
{
    private static (ServiceProvider provider, FakePublisher publisher) BuildProvider(string dbName)
    {
        var publisher = new FakePublisher();
        var services = new ServiceCollection();
        services.AddDbContext<CrmDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<IPublisher>(publisher);
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        return (services.BuildServiceProvider(), publisher);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyPublishesForDueContracts()
    {
        var dbName = $"billing-job-{Guid.NewGuid()}";
        var (provider, publisher) = BuildProvider(dbName);
        var companyId = Guid.NewGuid();

        using (var scope = provider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenant.SetTenant(companyId, "Empresa Test");

            var client = new Client { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Cliente", TaxId = "X", Email = "c@test.com" };
            ctx.Clients.Add(client);

            var due = new ClientContractedService
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ClientId = client.Id, ServiceCatalogItemId = Guid.NewGuid(),
                ServiceName = "Vencido", Price = 100, TaxRate = 21, Periodicity = "Monthly",
                StartDate = DateTime.UtcNow.Date.AddMonths(-1), NextBillingDate = DateTime.UtcNow.Date, Status = "Active",
            };
            var notDue = new ClientContractedService
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ClientId = client.Id, ServiceCatalogItemId = Guid.NewGuid(),
                ServiceName = "No vencido", Price = 100, TaxRate = 21, Periodicity = "Monthly",
                StartDate = DateTime.UtcNow.Date, NextBillingDate = DateTime.UtcNow.Date.AddMonths(1), Status = "Active",
            };
            var cancelled = new ClientContractedService
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ClientId = client.Id, ServiceCatalogItemId = Guid.NewGuid(),
                ServiceName = "Cancelado", Price = 100, TaxRate = 21, Periodicity = "Monthly",
                StartDate = DateTime.UtcNow.Date.AddMonths(-1), NextBillingDate = DateTime.UtcNow.Date, Status = "Cancelled",
            };
            ctx.ClientContractedServices.AddRange(due, notDue, cancelled);
            await ctx.SaveChangesAsync();
        }

        var job = new ContractedServiceBillingJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ContractedServiceBillingJob>.Instance);

        await job.ExecuteAsync();

        var published = Assert.Single(publisher.Published);
        var evt = Assert.IsType<ClientServiceDueForBillingEvent>(published);
        Assert.Equal("Vencido", evt.ServiceName);
        Assert.Equal(companyId, evt.CompanyId);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoDueContracts_PublishesNothing()
    {
        var dbName = $"billing-job-empty-{Guid.NewGuid()}";
        var (provider, publisher) = BuildProvider(dbName);

        var job = new ContractedServiceBillingJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ContractedServiceBillingJob>.Instance);

        await job.ExecuteAsync();

        Assert.Empty(publisher.Published);
    }
}
