using Erp.Application.Common.Events;
using Erp.Application.Features.Licensing.Handlers;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Licensing;

public class SeedTenantModulesHandlerTests
{
    private static ErpDbContext NewContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"seed-tenant-modules-{Guid.NewGuid()}")
            .Options;
        return new ErpDbContext(options, tenant);
    }

    private static async Task<(Guid companyId, Guid planId)> SeedPlanAndSubscription(
        ErpDbContext ctx, params (string module, bool included)[] planModules)
    {
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        ctx.Plans.Add(new Plan
        {
            Id = planId, Name = "Starter", Description = "test", IsActive = true,
        });
        foreach (var (module, included) in planModules)
        {
            ctx.PlanModules.Add(new PlanModule
            {
                Id = Guid.NewGuid(), PlanId = planId, ModuleName = module, IsIncluded = included,
            });
        }
        ctx.Companies.Add(new Company
        {
            Id = companyId, Name = "Test SL", TaxId = $"B{Guid.NewGuid():N}"[..9], IsActive = true, Country = "ES",
        });
        ctx.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(), CompanyId = companyId, PlanName = "Starter",
            IsActive = true, ExpirationDate = DateTime.UtcNow.AddYears(1), StripeStatus = "active",
        });
        await ctx.SaveChangesAsync();
        return (companyId, planId);
    }

    [Fact]
    public async Task Handle_EnablesOnlyModulesIncludedInPlan()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var (companyId, _) = await SeedPlanAndSubscription(ctx,
            ("Billing", true), ("CRM", true), ("Inventory", false));

        var handler = new SeedTenantModulesHandler(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<SeedTenantModulesHandler>.Instance);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);

        var enabled = await ctx.TenantModules.IgnoreQueryFilters()
            .Where(m => m.CompanyId == companyId)
            .Select(m => m.ModuleName)
            .ToListAsync();

        Assert.Contains("Billing", enabled);
        Assert.Contains("CRM", enabled);
        Assert.DoesNotContain("Inventory", enabled);
        Assert.Equal(2, enabled.Count);
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_DoesNotDuplicateRows()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var (companyId, _) = await SeedPlanAndSubscription(ctx, ("Billing", true));

        var handler = new SeedTenantModulesHandler(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<SeedTenantModulesHandler>.Instance);
        var evt = new CompanyCreatedEvent { CompanyId = companyId };
        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt, CancellationToken.None);

        var count = await ctx.TenantModules.IgnoreQueryFilters()
            .CountAsync(m => m.CompanyId == companyId && m.ModuleName == "Billing");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Handle_WithoutActiveSubscription_SeedsNothing()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var companyId = Guid.NewGuid();
        ctx.Companies.Add(new Company { Id = companyId, Name = "Sin sub", TaxId = "B99999999", IsActive = true, Country = "ES" });
        await ctx.SaveChangesAsync();

        var handler = new SeedTenantModulesHandler(ctx, Microsoft.Extensions.Logging.Abstractions.NullLogger<SeedTenantModulesHandler>.Instance);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);

        Assert.Empty(await ctx.TenantModules.IgnoreQueryFilters().Where(m => m.CompanyId == companyId).ToListAsync());
    }
}
