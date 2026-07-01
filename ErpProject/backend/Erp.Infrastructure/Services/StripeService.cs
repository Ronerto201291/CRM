using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Stripe integration for SaaS subscription billing.
/// Handles checkout sessions, portal sessions, and webhook events.
/// Keys and Price IDs are read from StripeOptions (never hardcoded).
/// </summary>
public class StripeService
{
    private readonly IApplicationDbContext _ctx;
    private readonly ILicensingDbContext _licensing;
    private readonly ILogger<StripeService> _logger;
    private readonly StripeOptions _options;

    public StripeService(
        IApplicationDbContext ctx,
        ILicensingDbContext licensing,
        ILogger<StripeService> logger,
        IOptions<StripeOptions> options)
    {
        _ctx     = ctx;
        _licensing = licensing;
        _logger  = logger;
        _options = options.Value;
        StripeConfiguration.ApiKey = _options.SecretKey;
    }

    /// <summary>
    /// Creates a Stripe Checkout Session for plan subscription.
    /// Returns URL to redirect the user to Stripe's hosted payment page.
    /// </summary>
    public async Task<string> CreateCheckoutSessionAsync(
        Guid companyId, string planName, string successUrl, string cancelUrl,
        CancellationToken ct = default)
    {
        var company = await _ctx.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException($"Company {companyId} not found");

        var plan = await _ctx.Plans
            .Include(p => p.PlanModules)
            .FirstOrDefaultAsync(p => p.Name == planName && p.IsActive, ct)
            ?? throw new InvalidOperationException($"Plan '{planName}' not found");

        // Get or create Stripe customer
        var customerId = company.StripeCustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = (await _ctx.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.CompanyId == companyId)
                    .OrderBy(u => u.CreatedAt)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct)) ?? string.Empty,
                Name = company.Name,
                Metadata = new Dictionary<string, string> { ["companyId"] = companyId.ToString() }
            });
            customerId = customer.Id;
            company.StripeCustomerId = customerId;
            await _ctx.SaveChangesAsync(ct);
        }

        // Lookup Stripe Price ID from config or use plan metadata
        var priceId = GetStripePriceId(planName);

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["companyId"] = companyId.ToString(),
                ["planName"] = planName
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["companyId"] = companyId.ToString(),
                    ["planName"] = planName
                }
            }
        };

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(options);
        return session.Url;
    }

    /// <summary>
    /// Creates a Stripe Customer Portal session for self-service subscription management.
    /// </summary>
    public async Task<string> CreatePortalSessionAsync(Guid companyId, string returnUrl, CancellationToken ct = default)
    {
        var company = await _ctx.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found");

        if (string.IsNullOrEmpty(company.StripeCustomerId))
            throw new InvalidOperationException("No Stripe customer found. Subscribe first.");

        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = company.StripeCustomerId,
            ReturnUrl = returnUrl
        });
        return session.Url;
    }

    /// <summary>
    /// Handles Stripe webhook events.
    /// Activates/deactivates subscriptions based on payment events.
    /// </summary>
    public async Task HandleWebhookAsync(string payload, string stripeSignature, CancellationToken ct = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, stripeSignature, _options.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning("Stripe webhook signature validation failed: {Message}", ex.Message);
            throw;
        }

        _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompleted((Session)stripeEvent.Data.Object, ct);
                break;

            case "customer.subscription.updated":
                await HandleSubscriptionUpdated((Stripe.Subscription)stripeEvent.Data.Object, ct);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionCanceled((Stripe.Subscription)stripeEvent.Data.Object, ct);
                break;

            case "invoice.payment_failed":
                await HandlePaymentFailed((Invoice)stripeEvent.Data.Object, ct);
                break;

            case "invoice.payment_succeeded":
                await HandlePaymentSucceeded((Invoice)stripeEvent.Data.Object, ct);
                break;

            default:
                _logger.LogInformation("Unhandled Stripe event: {Type}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutCompleted(Session session, CancellationToken ct)
    {
        if (!session.Metadata.TryGetValue("companyId", out var companyIdStr)) return;
        if (!session.Metadata.TryGetValue("planName", out var planName)) return;
        if (!Guid.TryParse(companyIdStr, out var companyId)) return;

        var subscription = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);

        if (subscription == null)
        {
            _ctx.Subscriptions.Add(new Erp.Domain.Entities.Licensing.Subscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PlanName = planName,
                StripeSubscriptionId = session.SubscriptionId,
                StripeStatus = "active",
                IsActive = true,
                ExpirationDate = DateTime.UtcNow.AddYears(1)
            });
        }
        else
        {
            subscription.PlanName = planName;
            subscription.StripeSubscriptionId = session.SubscriptionId;
            subscription.StripeStatus = "active";
            subscription.IsActive = true;
            subscription.ExpirationDate = DateTime.UtcNow.AddYears(1);
        }

        await _ctx.SaveChangesAsync(ct);
        await SyncTenantModulesAsync(companyId, planName, ct);
        _logger.LogInformation("Subscription activated for company {CompanyId}, plan {Plan}", companyId, planName);
    }

    private async Task HandleSubscriptionUpdated(Stripe.Subscription stripeSubscription, CancellationToken ct)
    {
        var sub = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id, ct);

        if (sub == null) return;

        sub.StripeStatus   = stripeSubscription.Status;
        sub.IsActive       = stripeSubscription.Status == "active" || stripeSubscription.Status == "trialing";
        sub.ExpirationDate = stripeSubscription.CurrentPeriodEnd;

        // Sync plan name from Stripe metadata (covers plan upgrades/downgrades)
        if (stripeSubscription.Metadata.TryGetValue("planName", out var newPlanName)
            && !string.IsNullOrEmpty(newPlanName)
            && newPlanName != sub.PlanName)
        {
            sub.PlanName = newPlanName;
        }

        await _ctx.SaveChangesAsync(ct);

        if (sub.IsActive)
            await SyncTenantModulesAsync(sub.CompanyId, sub.PlanName, ct);
    }

    private async Task HandlePaymentSucceeded(Invoice stripeInvoice, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(stripeInvoice.SubscriptionId)) return;

        var sub = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeInvoice.SubscriptionId, ct);

        if (sub == null) return;

        // Renewal: extend expiration and re-activate
        sub.StripeStatus   = "active";
        sub.IsActive       = true;
        sub.ExpirationDate = DateTime.UtcNow.AddMonths(1);
        await _ctx.SaveChangesAsync(ct);

        await SyncTenantModulesAsync(sub.CompanyId, sub.PlanName, ct);
        _logger.LogInformation("Payment succeeded — subscription renewed for company {CompanyId}", sub.CompanyId);
    }

    private async Task HandleSubscriptionCanceled(Stripe.Subscription stripeSubscription, CancellationToken ct)
    {
        var sub = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id, ct);

        if (sub == null) return;
        sub.StripeStatus = "canceled";
        sub.IsActive = false;
        await _ctx.SaveChangesAsync(ct);
        _logger.LogWarning("Subscription canceled for company subscription {Id}", sub.CompanyId);
    }

    private async Task HandlePaymentFailed(Invoice stripeInvoice, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(stripeInvoice.SubscriptionId)) return;

        var sub = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeInvoice.SubscriptionId, ct);

        if (sub == null) return;
        sub.StripeStatus = "past_due";
        await _ctx.SaveChangesAsync(ct);
        _logger.LogWarning("Payment failed for subscription {Id}", stripeInvoice.SubscriptionId);
    }

    /// <summary>
    /// Syncs TenantModules for a company based on the plan's PlanModules.
    /// Called after every successful payment or plan change.
    /// Creates missing TenantModule records; sets IsEnabled per plan.
    /// </summary>
    private async Task SyncTenantModulesAsync(Guid companyId, string planName, CancellationToken ct)
    {
        var plan = await _licensing.Plans
            .IgnoreQueryFilters()
            .Include(p => p.PlanModules)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == planName && p.IsActive, ct);

        if (plan == null)
        {
            _logger.LogWarning("SyncTenantModules: plan '{Plan}' not found.", planName);
            return;
        }

        var existing = await _licensing.TenantModules
            .IgnoreQueryFilters()
            .Where(m => m.CompanyId == companyId)
            .ToListAsync(ct);

        foreach (var planModule in plan.PlanModules)
        {
            var tm = existing.FirstOrDefault(m => m.ModuleName == planModule.ModuleName);
            if (tm == null)
            {
                _licensing.TenantModules.Add(new TenantModule
                {
                    Id         = Guid.NewGuid(),
                    CompanyId  = companyId,
                    ModuleName = planModule.ModuleName,
                    IsEnabled  = planModule.IsIncluded,
                    CreatedAt  = DateTime.UtcNow
                });
            }
            else
            {
                tm.IsEnabled  = planModule.IsIncluded;
                tm.UpdatedAt  = DateTime.UtcNow;
            }
        }

        await _licensing.SaveChangesAsync(ct);
        _logger.LogInformation(
            "TenantModules synced for company {CompanyId} → plan {Plan} ({Count} modules)",
            companyId, planName, plan.PlanModules.Count);
    }

    private string GetStripePriceId(string planName)
    {
        var priceId = _options.PriceIds.GetByPlanName(planName);
        if (string.IsNullOrEmpty(priceId))
            throw new InvalidOperationException(
                $"Stripe Price ID for plan '{planName}' is not configured. " +
                $"Set Stripe__PriceIds__{planName} in environment variables.");
        return priceId;
    }
}
