using Erp.Application.Features.Subscriptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Subscriptions;

/// <summary>
/// SaaS subscription management: checkout, portal, current plan.
/// </summary>
[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly IMediator _mediator;

    public SubscriptionController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var sub = await _mediator.Send(new GetCurrentSubscriptionQuery(), ct);
        return Ok(new
        {
            plan = sub?.Plan,
            isActive = sub?.IsActive,
            stripeStatus = sub?.StripeStatus,
            expirationDate = sub?.ExpirationDate,
            modules = sub?.Modules,
            message = sub?.Message,
            maxCompanies = sub?.MaxCompanies,
            companiesUsed = sub?.CompaniesUsed,
            gestoriaCompanies = sub?.GestoriaCompanies?.Select(c => new
            {
                c.CompanyId,
                c.Name,
                c.TaxId,
            }),
        });
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = await _mediator.Send(new CreateCheckoutSessionCommand(
            request.PlanName,
            $"{baseUrl}/dashboard?subscription=success",
            $"{baseUrl}/dashboard?subscription=canceled"), ct);

        return Ok(new { checkoutUrl = url });
    }

    [HttpPost("portal")]
    public async Task<IActionResult> CreatePortal(CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = await _mediator.Send(new CreatePortalSessionCommand($"{baseUrl}/dashboard"), ct);
        return Ok(new { portalUrl = url });
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var plans = await _mediator.Send(new GetSubscriptionPlansQuery(), ct);
        return Ok(plans.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.MonthlyPrice,
            p.YearlyPrice,
            p.MaxUsers,
            p.MaxInvoicesPerMonth,
            maxCompanies = p.MaxCompanies,
            Modules = p.Modules
        }));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetBillingHistory(CancellationToken ct)
    {
        var invoices = await _mediator.Send(new GetBillingHistoryQuery(), ct);
        return Ok(invoices.Select(i => new
        {
            i.Id,
            Date = i.Date,
            created = i.Date,
            amount = (long)Math.Round(i.AmountEur * 100m),
            AmountEur = i.AmountEur,
            i.Currency,
            currency = i.Currency?.ToLowerInvariant() ?? "eur",
            i.Status,
            status = i.Status,
            PdfUrl = i.PdfUrl,
            invoiceUrl = i.PdfUrl,
            i.Description,
            billedCompanyCount = i.BilledCompanyCount,
            companyBreakdown = i.CompanyBreakdown?.Select(c => new
            {
                c.CompanyId,
                c.Name,
                c.TaxId,
            }),
            lineItems = i.LineItems?.Select(l => new
            {
                l.CompanyId,
                companyName = l.CompanyName,
                l.TaxId,
                amountEur = l.AmountEur,
                l.Description,
            }),
        }));
    }
}

public record CheckoutRequest(string PlanName);
