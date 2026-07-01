using Asp.Versioning;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Api.Controllers.Public.V1;

/// <summary>
/// Public API v1 — Invoices endpoint for external integrations.
/// Requires X-Api-Key header. Rate limited per ApiKey.RateLimit.
/// Route: /api/v1/public/invoices
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/invoices")]
public class PublicInvoicesController : ControllerBase
{
    private readonly IBillingDbContext _billingCtx;

    public PublicInvoicesController(IBillingDbContext billingCtx) => _billingCtx = billingCtx;

    /// <summary>
    /// Returns locked/issued invoices for the tenant identified by the API key.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] int? year,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        // Tenant is resolved via ApiKeyRateLimitMiddleware — CompanyId injected via query filter
        var q = _billingCtx.Invoices.AsQueryable();

        if (year.HasValue)
            q = q.Where(i => i.FiscalYear == year.Value);

        if (!string.IsNullOrEmpty(status))
            q = q.Where(i => i.Status == status);

        var invoices = await q
            .OrderByDescending(i => i.IssueDate)
            .Select(i => new
            {
                i.Id, i.Number, i.Series, i.FiscalYear,
                i.IssueDate, i.DueDate, i.Status, i.IsLocked,
                i.Subtotal, i.TaxAmount, i.Total, i.Hash
            })
            .ToListAsync(ct);

        return Ok(new { version = "1.0", data = invoices, total = invoices.Count });
    }

    /// <summary>
    /// Returns a single invoice detail by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        var invoice = await _billingCtx.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id, i.Number, i.Series, i.FiscalYear, i.InvoiceType,
                i.IssueDate, i.DueDate, i.Status, i.IsLocked,
                i.Subtotal, i.TaxAmount, i.IrpfAmount, i.Total,
                i.Hash, i.PreviousHash,
                Lines = i.InvoiceLines.Select(l => new
                {
                    l.Description, l.Quantity, l.UnitPrice, l.TaxRate, l.LineTotal
                })
            })
            .FirstOrDefaultAsync(ct);

        if (invoice == null) return NotFound();
        return Ok(invoice);
    }
}
