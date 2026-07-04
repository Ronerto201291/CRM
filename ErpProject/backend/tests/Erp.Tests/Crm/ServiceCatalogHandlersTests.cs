using Erp.Modules.Crm.Application.Features.Services.Commands;
using Erp.Modules.Crm.Application.Features.Services.Handlers;
using Erp.Modules.Crm.Application.Features.Services.Queries;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class ServiceCatalogHandlersTests
{
    private static CrmDbContext NewContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"service-catalog-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }

    [Fact]
    public async Task Create_PersistsItemForCompany()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Taller Test");
        await using var ctx = NewContext(tenant);
        var handler = new CreateServiceCatalogItemHandler(ctx, tenant);

        var result = await handler.Handle(new CreateServiceCatalogItemCommand
        {
            Name = "Mantenimiento anual",
            DefaultPrice = 250m,
            DefaultTaxRate = 21m,
            DefaultPeriodicity = "Yearly",
        }, CancellationToken.None);

        Assert.Equal("Mantenimiento anual", result.Name);
        Assert.True(result.IsActive);
        var stored = await ctx.ServiceCatalogItems.SingleAsync();
        Assert.Equal(companyId, stored.CompanyId);
    }

    // La validación de entrada (nombre/precio/IVA/periodicidad) ya no vive en
    // el handler: la ejecuta ValidationBehavior vía CreateServiceCatalogItemValidator
    // (ver ServiceCatalogValidatorsTests.cs) antes de que MediatR llegue a este
    // handler. Este handler asume datos ya válidos, igual que CreateClientHandler.

    [Fact]
    public async Task Update_TogglesIsActive()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa");
        await using var ctx = NewContext(tenant);
        var createHandler = new CreateServiceCatalogItemHandler(ctx, tenant);
        var created = await createHandler.Handle(new CreateServiceCatalogItemCommand
        {
            Name = "ITV",
            DefaultPrice = 40m,
            DefaultTaxRate = 21m,
            DefaultPeriodicity = "Yearly",
        }, CancellationToken.None);

        var updateHandler = new UpdateServiceCatalogItemHandler(ctx);
        var updated = await updateHandler.Handle(new UpdateServiceCatalogItemCommand
        {
            Id = created.Id,
            Name = "ITV",
            DefaultPrice = 45m,
            DefaultTaxRate = 21m,
            DefaultPeriodicity = "Yearly",
            IsActive = false,
        }, CancellationToken.None);

        Assert.False(updated.IsActive);
        Assert.Equal(45m, updated.DefaultPrice);
    }

    [Fact]
    public async Task GetServiceCatalog_ExcludesInactiveByDefault()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa");
        await using var ctx = NewContext(tenant);
        var createHandler = new CreateServiceCatalogItemHandler(ctx, tenant);
        var active = await createHandler.Handle(new CreateServiceCatalogItemCommand { Name = "Activo", DefaultPrice = 10, DefaultTaxRate = 21, DefaultPeriodicity = "Monthly" }, CancellationToken.None);
        var toDeactivate = await createHandler.Handle(new CreateServiceCatalogItemCommand { Name = "Inactivo", DefaultPrice = 10, DefaultTaxRate = 21, DefaultPeriodicity = "Monthly" }, CancellationToken.None);
        await new UpdateServiceCatalogItemHandler(ctx).Handle(new UpdateServiceCatalogItemCommand
        {
            Id = toDeactivate.Id, Name = "Inactivo", DefaultPrice = 10, DefaultTaxRate = 21, DefaultPeriodicity = "Monthly", IsActive = false,
        }, CancellationToken.None);

        var queryHandler = new GetServiceCatalogHandler(ctx);
        var onlyActive = await queryHandler.Handle(new GetServiceCatalogQuery(), CancellationToken.None);
        var all = await queryHandler.Handle(new GetServiceCatalogQuery { IncludeInactive = true }, CancellationToken.None);

        Assert.Single(onlyActive);
        Assert.Equal(active.Id, onlyActive.Single().Id);
        Assert.Equal(2, all.Count);
    }
}
