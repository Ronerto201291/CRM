using System.Net;
using System.Text;
using System.Text.Json;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;
using Xunit;
using LicensingPlan = Erp.Domain.Entities.Licensing.Plan;

namespace Erp.Tests.Subscriptions;

public class StripeWebhookHandlerTests
{
    private const string WebhookSecret = "whsec_test_webhook_secret";

    [Fact]
    public async Task HandleWebhook_InvalidSignature_ThrowsStripeException()
    {
        var service = CreateService($"stripe-wh-{Guid.NewGuid()}");

        await Assert.ThrowsAsync<StripeException>(() =>
            service.HandleWebhookAsync("{}", "invalid-signature", CancellationToken.None));
    }

    [Fact]
    public async Task HandleWebhook_EmptyPayload_ThrowsStripeException()
    {
        var service = CreateService($"stripe-wh-empty-{Guid.NewGuid()}");

        await Assert.ThrowsAsync<StripeException>(() =>
            service.HandleWebhookAsync(string.Empty, "t=0,v1=abc", CancellationToken.None));
    }

    [Fact]
    public async Task HandleWebhook_CheckoutSessionCompleted_ActivatesSubscription()
    {
        var companyId = Guid.NewGuid();
        var dbName = $"stripe-wh-ok-{Guid.NewGuid()}";
        var service = CreateService(dbName, async ctx =>
        {
            ctx.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Stripe Webhook Co",
                TaxId = "B12345674",
            });

            var plan = new LicensingPlan
            {
                Id = Guid.NewGuid(),
                Name = "Pro",
                MonthlyPrice = 49,
                YearlyPrice = 490,
                MaxUsers = 10,
                MaxInvoicesPerMonth = 500,
                IsActive = true,
            };
            plan.PlanModules.Add(new PlanModule
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                ModuleName = "Billing",
                IsIncluded = true,
            });
            ctx.Plans.Add(plan);
            await ctx.SaveChangesAsync();
        });

        var payload = BuildCheckoutCompletedPayload(companyId);
        var signature = StripeTestSignature.GenerateHeader(payload, WebhookSecret);

        await service.HandleWebhookAsync(payload, signature, CancellationToken.None);

        await using var verifyCtx = CreateContext(dbName);
        var subscription = await verifyCtx.Subscriptions
            .IgnoreQueryFilters()
            .SingleAsync(s => s.CompanyId == companyId);
        Assert.Equal("Pro", subscription.PlanName);
        Assert.True(subscription.IsActive);
        Assert.Equal("sub_test_e2e_subscription", subscription.StripeSubscriptionId);

        var processed = await verifyCtx.StripeWebhookEvents
            .AnyAsync(e => e.EventId == "evt_test_checkout_completed_e2e");
        Assert.True(processed);
    }

    [Fact]
    public async Task HandleWebhook_CheckoutSessionCompleted_IsIdempotent()
    {
        var companyId = Guid.NewGuid();
        var dbName = $"stripe-wh-idem-{Guid.NewGuid()}";
        var service = CreateService(dbName, async ctx =>
        {
            ctx.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674" });
            ctx.Plans.Add(new LicensingPlan
            {
                Id = Guid.NewGuid(),
                Name = "Pro",
                MonthlyPrice = 49,
                YearlyPrice = 490,
                IsActive = true,
            });
            await ctx.SaveChangesAsync();
        });

        var payload = BuildCheckoutCompletedPayload(companyId);
        var signature = StripeTestSignature.GenerateHeader(payload, WebhookSecret);

        await service.HandleWebhookAsync(payload, signature, CancellationToken.None);
        await service.HandleWebhookAsync(payload, signature, CancellationToken.None);

        await using var verifyCtx = CreateContext(dbName);
        var count = await verifyCtx.Subscriptions.IgnoreQueryFilters().CountAsync(s => s.CompanyId == companyId);
        Assert.Equal(1, count);
    }

    private static string BuildCheckoutCompletedPayload(Guid companyId)
    {
        var apiVersion = StripeConfiguration.ApiVersion;
        return "{\"id\":\"evt_test_checkout_completed_e2e\",\"object\":\"event\",\"api_version\":\"" + apiVersion
            + "\",\"created\":1710000000,\"type\":\"checkout.session.completed\",\"livemode\":false,\"pending_webhooks\":1,"
            + "\"request\":{\"id\":null,\"idempotency_key\":null},\"data\":{\"object\":{\"id\":\"cs_test_e2e_session\","
            + "\"object\":\"checkout.session\",\"mode\":\"subscription\",\"payment_status\":\"paid\",\"status\":\"complete\","
            + "\"currency\":\"eur\",\"subscription\":\"sub_test_e2e_subscription\",\"metadata\":{\"companyId\":\""
            + companyId + "\",\"planName\":\"Pro\"}}}}";
    }

    private static StripeService CreateService(string dbName, Func<ErpDbContext, Task>? seed = null)
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new ErpDbContext(options, tenant);
        if (seed != null)
            seed(ctx).GetAwaiter().GetResult();

        return new StripeService(
            ctx,
            ctx,
            NullLogger<StripeService>.Instance,
            Options.Create(new StripeOptions
            {
                SecretKey = "sk_test_dummy",
                WebhookSecret = WebhookSecret,
            }));
    }

    private static ErpDbContext CreateContext(string dbName)
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ErpDbContext(options, tenant);
    }
}
