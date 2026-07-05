using Erp.Application.Features.Subscriptions;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Subscriptions;

public class SubscriptionHandlerTests
{
    [Fact]
    public async Task GetCurrentSubscription_WhenMissing_ReturnsFreePlan()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"sub-current-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new GetCurrentSubscriptionHandler(
            ctx, tenant, new FakeCurrentUserAccessor(), new CompanyMembershipLimitService(ctx));

        var result = await handler.Handle(new GetCurrentSubscriptionQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Free", result!.Plan);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task GetCurrentSubscription_WithActivePlan_ReturnsModules()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"sub-active-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = "Pro",
            MonthlyPrice = 49,
            YearlyPrice = 490,
            MaxUsers = 10,
            MaxInvoicesPerMonth = 500,
        };
        plan.PlanModules.Add(new PlanModule { Id = Guid.NewGuid(), PlanId = plan.Id, ModuleName = "Billing", IsIncluded = true });
        ctx.Plans.Add(plan);
        ctx.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanName = "Pro",
            IsActive = true,
            StripeStatus = "active",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetCurrentSubscriptionHandler(
            ctx, tenant, new FakeCurrentUserAccessor(), new CompanyMembershipLimitService(ctx));
        var result = await handler.Handle(new GetCurrentSubscriptionQuery(), CancellationToken.None);

        Assert.Equal("Pro", result!.Plan);
        Assert.True(result.IsActive);
        Assert.Contains("Billing", result.Modules);
    }

    [Fact]
    public async Task GetSubscriptionPlans_ReturnsActivePlans()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"sub-plans-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Plans.Add(new Plan
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            MonthlyPrice = 19,
            YearlyPrice = 190,
            MaxUsers = 3,
            MaxInvoicesPerMonth = 50,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSubscriptionPlansHandler(ctx);
        var plans = await handler.Handle(new GetSubscriptionPlansQuery(), CancellationToken.None);

        Assert.Single(plans);
        Assert.Equal("Starter", plans[0].Name);
    }
}
