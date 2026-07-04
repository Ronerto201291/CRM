using Erp.Modules.Crm.Application.Features.Services.Commands;
using Erp.Modules.Crm.Application.Features.Services.Handlers;
using Erp.Modules.Crm.Application.Features.Services.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class ClientContractedServiceHandlersTests
{
    private static CrmDbContext NewContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"contracted-services-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }

    private static async Task<(Guid clientId, Guid catalogItemId)> SeedClientAndCatalogItem(CrmDbContext ctx, Guid companyId, decimal price = 100m, decimal taxRate = 21m, string periodicity = "Monthly")
    {
        var client = new Client { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Cliente Test", TaxId = "12345678Z", Email = "c@test.com" };
        var item = new ServiceCatalogItem { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Mantenimiento", DefaultPrice = price, DefaultTaxRate = taxRate, DefaultPeriodicity = periodicity, IsActive = true };
        ctx.Clients.Add(client);
        ctx.ServiceCatalogItems.Add(item);
        await ctx.SaveChangesAsync();
        return (client.Id, item.Id);
    }

    [Fact]
    public async Task Create_SnapshotsPriceTaxRateAndPeriodicityFromCatalog()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Taller Test");
        await using var ctx = NewContext(tenant);
        var (clientId, catalogItemId) = await SeedClientAndCatalogItem(ctx, companyId, price: 199.99m, taxRate: 21m, periodicity: "Yearly");

        var handler = new CreateClientContractedServiceHandler(ctx, tenant);
        var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await handler.Handle(new CreateClientContractedServiceCommand
        {
            ClientId = clientId,
            ServiceCatalogItemId = catalogItemId,
            StartDate = startDate,
        }, CancellationToken.None);

        Assert.Equal("Mantenimiento", result.ServiceName);
        Assert.Equal(199.99m, result.Price);
        Assert.Equal(21m, result.TaxRate);
        Assert.Equal("Yearly", result.Periodicity);
        Assert.Equal(startDate, result.NextBillingDate);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task Create_WithNegativePriceOverride_Throws()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa");
        await using var ctx = NewContext(tenant);
        var (clientId, catalogItemId) = await SeedClientAndCatalogItem(ctx, companyId);

        var handler = new CreateClientContractedServiceHandler(ctx, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateClientContractedServiceCommand
        {
            ClientId = clientId,
            ServiceCatalogItemId = catalogItemId,
            PriceOverride = -10m,
            StartDate = DateTime.UtcNow.Date,
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithInvalidPeriodicityOverride_Throws()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa");
        await using var ctx = NewContext(tenant);
        var (clientId, catalogItemId) = await SeedClientAndCatalogItem(ctx, companyId);

        var handler = new CreateClientContractedServiceHandler(ctx, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new CreateClientContractedServiceCommand
        {
            ClientId = clientId,
            ServiceCatalogItemId = catalogItemId,
            PeriodicityOverride = "Weekly",
            StartDate = DateTime.UtcNow.Date,
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithPriceOverride_UsesOverrideNotCatalogDefault()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa");
        await using var ctx = NewContext(tenant);
        var (clientId, catalogItemId) = await SeedClientAndCatalogItem(ctx, companyId, price: 100m);

        var handler = new CreateClientContractedServiceHandler(ctx, tenant);
        var result = await handler.Handle(new CreateClientContractedServiceCommand
        {
            ClientId = clientId,
            ServiceCatalogItemId = catalogItemId,
            PriceOverride = 75m,
            StartDate = DateTime.UtcNow.Date,
        }, CancellationToken.None);

        Assert.Equal(75m, result.Price);
    }

    [Fact]
    public async Task Cancel_SetsStatusAndEndDate()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa");
        await using var ctx = NewContext(tenant);
        var (clientId, catalogItemId) = await SeedClientAndCatalogItem(ctx, companyId);
        var created = await new CreateClientContractedServiceHandler(ctx, tenant).Handle(new CreateClientContractedServiceCommand
        {
            ClientId = clientId,
            ServiceCatalogItemId = catalogItemId,
            StartDate = DateTime.UtcNow.Date,
        }, CancellationToken.None);

        var cancelHandler = new CancelClientContractedServiceHandler(ctx);
        var ok = await cancelHandler.Handle(new CancelClientContractedServiceCommand { Id = created.Id }, CancellationToken.None);

        Assert.True(ok);
        var stored = await ctx.ClientContractedServices.SingleAsync(c => c.Id == created.Id);
        Assert.Equal("Cancelled", stored.Status);
        Assert.NotNull(stored.EndDate);
    }

    [Fact]
    public async Task Cancel_FromOtherCompany_ReturnsFalseAndDoesNotCancel()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();

        var tenantA = new FakeTenantContext();
        tenantA.SetTenant(companyA, "Empresa A");
        await using var ctx = NewContext(tenantA);

        // Contrato de otra empresa (companyB), sembrado directamente en la misma BD compartida.
        var otherCompanyClient = new Client { Id = Guid.NewGuid(), CompanyId = companyB, Name = "Cliente B", TaxId = "X", Email = "b@test.com" };
        var otherCompanyItem = new ServiceCatalogItem { Id = Guid.NewGuid(), CompanyId = companyB, Name = "Servicio B", DefaultPrice = 50, DefaultTaxRate = 21, DefaultPeriodicity = "Monthly", IsActive = true };
        var otherCompanyContract = new ClientContractedService
        {
            Id = Guid.NewGuid(), CompanyId = companyB, ClientId = otherCompanyClient.Id, ServiceCatalogItemId = otherCompanyItem.Id,
            ServiceName = "Servicio B", Price = 50, TaxRate = 21, Periodicity = "Monthly",
            StartDate = DateTime.UtcNow.Date, NextBillingDate = DateTime.UtcNow.Date, Status = "Active",
        };
        ctx.Clients.Add(otherCompanyClient);
        ctx.ServiceCatalogItems.Add(otherCompanyItem);
        ctx.ClientContractedServices.Add(otherCompanyContract);
        await ctx.SaveChangesAsync();

        // Tenant activo es companyA — el query filter no debe encontrar el contrato de companyB.
        var cancelHandler = new CancelClientContractedServiceHandler(ctx);
        var ok = await cancelHandler.Handle(new CancelClientContractedServiceCommand { Id = otherCompanyContract.Id }, CancellationToken.None);

        Assert.False(ok);
    }

    [Fact]
    public async Task GetByClient_OnlyReturnsContractsForThatClient()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa");
        await using var ctx = NewContext(tenant);
        var (clientA, itemId) = await SeedClientAndCatalogItem(ctx, companyId);
        var clientB = new Client { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Otro cliente", TaxId = "Y", Email = "y@test.com" };
        ctx.Clients.Add(clientB);
        await ctx.SaveChangesAsync();

        var createHandler = new CreateClientContractedServiceHandler(ctx, tenant);
        await createHandler.Handle(new CreateClientContractedServiceCommand { ClientId = clientA, ServiceCatalogItemId = itemId, StartDate = DateTime.UtcNow.Date }, CancellationToken.None);
        await createHandler.Handle(new CreateClientContractedServiceCommand { ClientId = clientB.Id, ServiceCatalogItemId = itemId, StartDate = DateTime.UtcNow.Date }, CancellationToken.None);

        var result = await new GetClientContractedServicesHandler(ctx).Handle(new GetClientContractedServicesQuery { ClientId = clientA }, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(clientA, result.Single().ClientId);
    }
}
