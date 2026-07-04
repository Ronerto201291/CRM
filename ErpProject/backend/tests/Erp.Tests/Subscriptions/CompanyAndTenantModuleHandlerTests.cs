using Erp.Application.Features.Company.Commands;
using Erp.Application.Features.Company.Handlers;
using Erp.Application.Features.Company.Queries;
using Erp.Application.Features.TenantModules.Commands;
using Erp.Application.Features.TenantModules.Handlers;
using Erp.Application.Features.TenantModules.Queries;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Subscriptions;

public class CompanyHandlerTests
{
    [Fact]
    public async Task GetCompany_ReturnsTenantCompany()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"company-get-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Mi Empresa SL",
            TaxId = "B12345674",
            Address = "Calle 1",
            Country = "ES",
            IsActive = true,
            PublicUploadToken = "token123",
            QrUploadEnabled = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetCompanyHandler(ctx, tenant);
        var dto = await handler.Handle(new GetCompanyQuery(), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("Mi Empresa SL", dto!.Name);
        Assert.True(dto.QrUploadEnabled);
    }

    [Fact]
    public async Task UpdateCompany_InvalidTaxId_Throws()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"company-update-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Mi Empresa SL",
            TaxId = "B12345674",
            Country = "ES",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateCompanyHandler(ctx, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateCompanyCommand { TaxId = "INVALID" }, CancellationToken.None));
    }
}

public class TenantModuleHandlerTests
{
    [Fact]
    public async Task GetTenantModules_ReturnsModulesForCompany()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"tenant-modules-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.TenantModules.Add(new TenantModule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ModuleName = "CRM",
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetTenantModulesHandler(ctx, tenant);
        var modules = await handler.Handle(new GetTenantModulesQuery(), CancellationToken.None);

        Assert.Single(modules);
        Assert.Equal("CRM", modules[0].ModuleName);
    }

    [Fact]
    public async Task UpdateTenantModule_TogglesEnabled()
    {
        var companyId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"tenant-module-update-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.TenantModules.Add(new TenantModule
        {
            Id = moduleId,
            CompanyId = companyId,
            ModuleName = "Billing",
            IsEnabled = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateTenantModuleHandler(ctx, tenant);
        var ok = await handler.Handle(new UpdateTenantModuleCommand { Id = moduleId, IsEnabled = false }, CancellationToken.None);

        Assert.True(ok);
        var updated = await ctx.TenantModules.SingleAsync();
        Assert.False(updated.IsEnabled);
    }
}
