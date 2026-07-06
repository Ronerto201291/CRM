using Erp.Application.Common.Attributes;
using Erp.Modules.Purchasing.Application.Features.Invoices.Commands;
using Erp.Modules.Purchasing.Application.Features.Invoices.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Purchasing.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/purchasing/invoices")]
[ApiVersion("1.0")]
[Authorize]
[RequiredModule("Purchasing")]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.PurchaseInvoice.Read)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAllSupplierInvoicesQuery(page, pageSize, search), ct);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.PurchaseInvoice.Create)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierInvoiceCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.PurchaseInvoice.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var invoice = await _mediator.Send(new GetSupplierInvoiceQuery(id), ct);
        if (invoice == null) return NotFound(new { error = "Supplier invoice not found" });
        return Ok(invoice);
    }
}
