using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services;
using Erp.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Controllers.Subscriptions;

/// <summary>
/// SaaS subscription management: checkout, portal, current plan.
/// </summary>
[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly StripeService _stripeService;
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenantContext;

    public SubscriptionController(StripeService stripe, IApplicationDbContext ctx, ITenantContext tenantContext)
    {
        _stripeService = stripe;
        _ctx = ctx;
        _tenantContext = tenantContext;
    }

    /// <summary>Returns current subscription and plan details for the tenant.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var sub = await _ctx.Subscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == tenantId, ct);

        if (sub == null)
            return Ok(new { plan = "Free", isActive = false, message = "No active subscription." });

        var plan = await _ctx.Plans
            .Include(p => p.PlanModules)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == sub.PlanName, ct);

        return Ok(new
        {
            plan = sub.PlanName,
            isActive = sub.IsActive,
            stripeStatus = sub.StripeStatus,
            expirationDate = sub.ExpirationDate,
            modules = plan?.PlanModules.Where(m => m.IsIncluded).Select(m => m.ModuleName).ToList()
        });
    }

    /// <summary>
    /// Creates Stripe Checkout session to subscribe to a plan.
    /// Returns URL to redirect user to Stripe's hosted payment page.
    /// </summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = await _stripeService.CreateCheckoutSessionAsync(
            tenantId,
            request.PlanName,
            $"{baseUrl}/dashboard?subscription=success",
            $"{baseUrl}/dashboard?subscription=canceled",
            ct);

        return Ok(new { checkoutUrl = url });
    }

    /// <summary>
    /// Creates Stripe Customer Portal session for self-service subscription management.
    /// </summary>
    [HttpPost("portal")]
    public async Task<IActionResult> CreatePortal(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = await _stripeService.CreatePortalSessionAsync(tenantId, $"{baseUrl}/dashboard", ct);
        return Ok(new { portalUrl = url });
    }

    /// <summary>GET /api/subscription/plans — plan comparison for self-service portal.</summary>
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _ctx.Plans
            .Include(p => p.PlanModules)
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .AsNoTracking()
            .Select(p => new
            {
                p.Id, p.Name, p.Description,
                p.MonthlyPrice, p.YearlyPrice,
                p.MaxUsers, p.MaxInvoicesPerMonth,
                Modules = p.PlanModules
                    .Where(m => m.IsIncluded)
                    .Select(m => m.ModuleName)
                    .ToList()
            })
            .ToListAsync(ct);

        return Ok(plans);
    }

    /// <summary>GET /api/subscription/invoices — Stripe billing history (last 12).</summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetBillingHistory(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var company = await _ctx.Companies
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);

        if (string.IsNullOrEmpty(company?.StripeCustomerId))
            return Ok(new { invoices = Array.Empty<object>() });

        var svc = new Stripe.InvoiceService();
        var list = await svc.ListAsync(new Stripe.InvoiceListOptions
        {
            Customer = company.StripeCustomerId,
            Limit    = 12
        }, cancellationToken: ct);

        var result = list.Select(i => new
        {
            i.Id,
            Date        = i.Created,
            AmountEur   = i.Total / 100m,
            Currency    = i.Currency?.ToUpperInvariant(),
            Status      = i.Status,
            PdfUrl      = i.InvoicePdf,
            Description = i.Description ?? i.Lines.FirstOrDefault()?.Description
        });

        return Ok(new { invoices = result });
    }
}

public record CheckoutRequest(string PlanName);
