using Erp.Application.Features.Subscriptions;
using Erp.Application.Features.Company.Commands;
using Erp.Application.Features.Company.Handlers;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

/// <summary>Fase 13: subscriptions checkout/portal/history, regenerate upload token.</summary>
public class Phase13SubscriptionHandlerTests
{
    [Fact]
    public async Task GetBillingHistory_ReturnsEmpty_WhenNoStripeCustomer()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"bill-hist-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674", IsActive = true, Country = "ES" });
        await ctx.SaveChangesAsync();

        var handler = new GetBillingHistoryHandler(ctx, new FakeSubscriptionBillingService(), tenant);
        var invoices = await handler.Handle(new GetBillingHistoryQuery(), CancellationToken.None);

        Assert.Empty(invoices);
    }

    [Fact]
    public async Task GetBillingHistory_ReturnsInvoices_WhenStripeCustomerExists()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"bill-hist2-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId, Name = "Co", TaxId = "B12345674", IsActive = true, Country = "ES",
            StripeCustomerId = "cus_test",
        });
        await ctx.SaveChangesAsync();

        var billing = new FakeSubscriptionBillingService();
        var handler = new GetBillingHistoryHandler(ctx, billing, tenant);
        var invoices = await handler.Handle(new GetBillingHistoryQuery(), CancellationToken.None);

        Assert.Single(invoices);
        Assert.Equal(49m, invoices[0].AmountEur);
    }

    [Fact]
    public async Task CreateCheckoutSession_DelegatesToBillingService()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billing = new FakeSubscriptionBillingService();
        var handler = new CreateCheckoutSessionHandler(billing, tenant);
        var url = await handler.Handle(new CreateCheckoutSessionCommand("Pro", "https://ok", "https://cancel"), CancellationToken.None);

        Assert.Equal(billing.CheckoutUrl, url);
    }

    [Fact]
    public async Task CreatePortalSession_DelegatesToBillingService()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billing = new FakeSubscriptionBillingService();
        var handler = new CreatePortalSessionHandler(billing, tenant);
        var url = await handler.Handle(new CreatePortalSessionCommand("https://return"), CancellationToken.None);

        Assert.Equal(billing.PortalUrl, url);
    }

    [Fact]
    public async Task RegenerateToken_ReturnsNewUploadToken()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"company-token-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Co",
            TaxId = "B12345674",
            IsActive = true,
            Country = "ES",
            PublicUploadToken = "old-token",
        });
        await ctx.SaveChangesAsync();

        var handler = new RegenerateTokenHandler(ctx, tenant);
        var token = await handler.Handle(new RegenerateTokenCommand(), CancellationToken.None);

        Assert.NotEqual("old-token", token);
        Assert.Equal(token, (await ctx.Companies.IgnoreQueryFilters().SingleAsync()).PublicUploadToken);
    }
}
