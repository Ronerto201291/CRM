using Asp.Versioning;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IMediator _mediator;

    public PublicInvoicesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] int? year,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var invoices = await _mediator.Send(new GetPublicInvoicesQuery { Year = year, Status = status }, ct);
        return Ok(new { version = "1.0", data = invoices, total = invoices.Count });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        var invoice = await _mediator.Send(new GetPublicInvoiceByIdQuery { Id = id }, ct);
        return invoice == null ? NotFound() : Ok(invoice);
    }
}
