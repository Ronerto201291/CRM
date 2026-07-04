using Erp.Application.Common.Attributes;
using Erp.Modules.Purchasing.Application.Features.Invoices.Commands;
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

    [HttpPost]
    [RequirePermission(Permissions.PurchaseInvoice.Create)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierInvoiceCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    [RequirePermission(Permissions.PurchaseInvoice.Read)]
    public IActionResult Get(Guid id) => Ok(new { id });
}
