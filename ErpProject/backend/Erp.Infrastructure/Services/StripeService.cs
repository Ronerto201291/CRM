using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Stripe integration for SaaS subscription billing, plus one-off invoice payments
/// (ADR-0018 #39, IInvoicePaymentGateway). Handles checkout sessions, portal sessions,
/// and webhook events. Keys and Price IDs are read from StripeOptions (never hardcoded).
/// </summary>
public class StripeService : ISubscriptionBillingService, IInvoicePaymentGateway
{
    private readonly IApplicationDbContext _ctx;
    private readonly ILicensingDbContext _licensing;
    private readonly ILogger<StripeService> _logger;
    private readonly StripeOptions _options;
    private readonly IPublisher _publisher;
    private readonly IGestoriaBillingBreakdownService _gestoriaBreakdown;

    public StripeService(
        IApplicationDbContext ctx,
        ILicensingDbContext licensing,
        ILogger<StripeService> logger,
        IOptions<StripeOptions> options,
        IPublisher publisher,
        IGestoriaBillingBreakdownService gestoriaBreakdown)
    {
        _ctx     = ctx;
        _licensing = licensing;
        _logger  = logger;
        _options = options.Value;
        _publisher = publisher;
        _gestoriaBreakdown = gestoriaBreakdown;
        StripeConfiguration.ApiKey = _options.SecretKey;
    }

    /// <summary>
    /// Creates a Stripe Checkout Session (mode "payment", not "subscription") for the exact
    /// amount of a single tenant-issued Invoice. Unlike CreateCheckoutSessionAsync above,
    /// this has no Stripe Customer/subscription concept — it's a one-off charge initiated
    /// anonymously from the public invoice portal (ADR-0018 #39).
    /// </summary>
    public async Task<string> CreateInvoiceCheckoutSessionAsync(
        Guid invoiceId, string invoiceNumber, decimal amount, string currency,
        string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Factura {invoiceNumber}",
                        },
                    },
                    Quantity = 1,
                },
            },
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                ["invoiceId"] = invoiceId.ToString(),
            },
        };

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(options, cancellationToken: ct);
        return session.Url;
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

        var companies = plan.MaxCompanies > 1
            ? await _gestoriaBreakdown.GetCompaniesForBillingAccountAsync(companyId, ct)
            : Array.Empty<GestoriaCompanyBillingLine>();

        var subscriptionMetadata = GestoriaStripeBilling.BuildSubscriptionMetadata(companyId, planName, companies);

        var lineItems = plan.MaxCompanies > 1 && companies.Count > 0
            ? companies.Select(c => new SessionLineItemOptions
            {
                Price = priceId,
                Quantity = 1,
            }).ToList()
            : new List<SessionLineItemOptions> { new() { Price = priceId, Quantity = 1 } };

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = lineItems,
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = subscriptionMetadata,
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = subscriptionMetadata,
                Description = plan.MaxCompanies > 1
                    ? GestoriaStripeBilling.BuildCheckoutDescription(companies)
                    : null,
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

        _logger.LogInformation("Stripe webhook received: {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);

        var alreadyProcessed = await _licensing.StripeWebhookEvents
            .AnyAsync(e => e.EventId == stripeEvent.Id, ct);
        if (alreadyProcessed)
        {
            _logger.LogInformation("Stripe webhook {EventId} already processed — skipping", stripeEvent.Id);
            return;
        }

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

            case "invoice.created":
                await HandleInvoiceCreated((Invoice)stripeEvent.Data.Object, ct);
                break;

            default:
                _logger.LogInformation("Unhandled Stripe event: {Type}", stripeEvent.Type);
                break;
        }

        _licensing.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            ProcessedAt = DateTime.UtcNow
        });
        await _licensing.SaveChangesAsync(ct);
    }

    private async Task HandleCheckoutCompleted(Session session, CancellationToken ct)
    {
        // One-off invoice payment (ADR-0018 #39) — distinct metadata shape from the
        // subscription checkout below, so branch on it first and return early. Published
        // as a domain event instead of calling Billing's MarkPaidCommand directly, since
        // Erp.Infrastructure must not depend on Erp.Modules.Billing.Application.
        if (session.Metadata.TryGetValue("invoiceId", out var invoiceIdStr)
            && Guid.TryParse(invoiceIdStr, out var invoiceId))
        {
            await _publisher.Publish(new StripeInvoiceCheckoutCompletedEvent { InvoiceId = invoiceId }, ct);
            _logger.LogInformation("Invoice checkout completed for invoice {InvoiceId}", invoiceId);
            return;
        }

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

        if (!string.IsNullOrEmpty(session.SubscriptionId))
            await SyncGestoriaSubscriptionQuantityAsync(session.SubscriptionId, companyId, planName, ct);
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

        if (!string.IsNullOrEmpty(stripeSubscription.Id))
            await SyncGestoriaSubscriptionQuantityAsync(stripeSubscription.Id, sub.CompanyId, sub.PlanName, ct);
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

    private async Task HandleInvoiceCreated(Invoice stripeInvoice, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(stripeInvoice.SubscriptionId)) return;

        var sub = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeInvoice.SubscriptionId, ct);

        if (sub == null) return;

        await SyncGestoriaSubscriptionQuantityAsync(stripeInvoice.SubscriptionId!, sub.CompanyId, sub.PlanName, ct);
    }

    private async Task SyncGestoriaSubscriptionQuantityAsync(
        string stripeSubscriptionId,
        Guid billingCompanyId,
        string planName,
        CancellationToken ct)
    {
        var plan = await _ctx.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == planName && p.IsActive, ct);

        if (plan is null || plan.MaxCompanies <= 1)
            return;

        var companies = await _gestoriaBreakdown.GetCompaniesForBillingAccountAsync(billingCompanyId, ct);
        if (companies.Count == 0)
        {
            var billingCo = await _ctx.Companies.IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == billingCompanyId, ct);
            if (billingCo is not null)
                companies = [new GestoriaCompanyBillingLine(billingCo.Id, billingCo.Name, billingCo.TaxId)];
        }

        var priceId = GetStripePriceId(planName);
        var metadata = GestoriaStripeBilling.BuildSubscriptionMetadata(billingCompanyId, planName, companies);

        try
        {
            var subService = new SubscriptionService();
            var stripeSub = await subService.GetAsync(stripeSubscriptionId, cancellationToken: ct);
            var itemService = new SubscriptionItemService();

            var existingByCompany = stripeSub.Items.Data
                .Where(i => i.Metadata?.ContainsKey(GestoriaStripeBilling.CompanyIdMetadataKey) == true)
                .ToDictionary(
                    i => Guid.Parse(i.Metadata[GestoriaStripeBilling.CompanyIdMetadataKey]),
                    i => i);

            var legacySingleItem = stripeSub.Items.Data.Count == 1
                && !existingByCompany.Any()
                && stripeSub.Items.Data[0].Quantity > 1;

            if (legacySingleItem)
            {
                await itemService.DeleteAsync(stripeSub.Items.Data[0].Id, cancellationToken: ct);
                existingByCompany = new Dictionary<Guid, SubscriptionItem>();
            }

            // Decisión de qué crear/actualizar/borrar es una función pura y testeable
            // (GestoriaStripeBilling.PlanSubscriptionItemSync) — aquí solo se ejecuta el plan
            // contra el SDK de Stripe. Antes esta decisión y las llamadas al SDK estaban
            // entremezcladas en un único bloque sin ningún test para MaxCompanies>1.
            var existingForPlan = existingByCompany.ToDictionary(
                kv => kv.Key,
                kv => new ExistingSubscriptionItem(kv.Value.Id, kv.Value.Quantity ?? 0));
            var syncPlan = GestoriaStripeBilling.PlanSubscriptionItemSync(existingForPlan, companies);

            foreach (var action in syncPlan)
            {
                switch (action.Kind)
                {
                    case SubscriptionItemSyncKind.Create:
                        await itemService.CreateAsync(new SubscriptionItemCreateOptions
                        {
                            Subscription = stripeSubscriptionId,
                            Price = priceId,
                            Quantity = 1,
                            Metadata = GestoriaStripeBilling.BuildCompanyItemMetadata(action.Company!),
                        }, cancellationToken: ct);
                        break;
                    case SubscriptionItemSyncKind.UpdateQuantity:
                        await itemService.UpdateAsync(action.ItemId!, new SubscriptionItemUpdateOptions
                        {
                            Quantity = 1,
                            Metadata = GestoriaStripeBilling.BuildCompanyItemMetadata(action.Company!),
                        }, cancellationToken: ct);
                        break;
                    case SubscriptionItemSyncKind.Delete:
                        await itemService.DeleteAsync(action.ItemId!, cancellationToken: ct);
                        break;
                }
            }

            await subService.UpdateAsync(stripeSubscriptionId, new SubscriptionUpdateOptions
            {
                Metadata = metadata,
                ProrationBehavior = "create_prorations",
            }, cancellationToken: ct);

            _logger.LogInformation(
                "Gestoría subscription {SubId} synced to {Qty} line items for company {CompanyId}",
                stripeSubscriptionId, companies.Count, billingCompanyId);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex,
                "No se pudo sincronizar line items Gestoría en Stripe para suscripción {SubId}",
                stripeSubscriptionId);
        }
    }

    private static bool MetadataMatches(
        IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, string> expected,
        string key)
    {
        if (!expected.TryGetValue(key, out var expectedVal))
            return true;
        return current.TryGetValue(key, out var currentVal) && currentVal == expectedVal;
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

    public async Task<IReadOnlyList<SubscriptionInvoiceDto>> ListInvoicesAsync(
        Guid companyId, CancellationToken ct = default)
    {
        var company = await _ctx.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        if (string.IsNullOrEmpty(company?.StripeCustomerId))
            return Array.Empty<SubscriptionInvoiceDto>();

        var svc = new InvoiceService();
        var list = await svc.ListAsync(new InvoiceListOptions
        {
            Customer = company.StripeCustomerId,
            Limit = 12
        }, cancellationToken: ct);

        return list.Select(i =>
        {
            var breakdown = GestoriaStripeBilling.ParseBreakdownFromInvoice(i);
            var lineItems = GestoriaStripeBilling.ParseLineItemsFromInvoice(i);
            var qty = lineItems.Count > 1 ? lineItems.Count : (int)(i.Lines?.Data?.FirstOrDefault()?.Quantity ?? 1);
            return new SubscriptionInvoiceDto(
                i.Id,
                i.Created,
                i.Total / 100m,
                i.Currency?.ToUpperInvariant(),
                i.Status,
                i.InvoicePdf,
                GestoriaStripeBilling.BuildInvoiceDescription(i, breakdown),
                breakdown,
                qty > 1 ? qty : null,
                lineItems.Count > 0 ? lineItems : null);
        }).ToList();
    }
}
