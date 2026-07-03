using Erp.Modules.Sales.Application.Features.Invoices.Commands;
using Erp.Modules.Sales.Application.Features.Invoices.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Sales.Api.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/sales/invoices")]
    [ApiVersion("1.0")]
    public class CustomerInvoicesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CustomerInvoicesController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, CancellationToken ct = default)
        {
            var result = await _mediator.Send(new GetAllCustomerInvoicesQuery(page, pageSize, search), ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var invoice = await _mediator.Send(new GetCustomerInvoiceQuery(id), ct);
            if (invoice == null) return NotFound(new { error = "Invoice not found" });
            return Ok(invoice);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCustomerInvoiceCommand cmd, CancellationToken ct)
        {
            var result = await _mediator.Send(cmd, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
    }
}
