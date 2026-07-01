using Erp.Modules.Sales.Application.Features.Deliveries.Commands;
using Erp.Modules.Sales.Application.Features.Deliveries.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Sales.Api.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/sales/deliveries")]
    [ApiVersion("1.0")]
    public class DeliveriesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DeliveriesController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null, CancellationToken ct = default)
        {
            var result = await _mediator.Send(new GetAllDeliveryNotesQuery(page, pageSize, search), ct);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var note = await _mediator.Send(new GetDeliveryNoteQuery(id), ct);
            if (note == null) return NotFound(new { error = "Delivery note not found" });
            return Ok(note);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDeliveryNoteCommand cmd, CancellationToken ct)
        {
            var id = await _mediator.Send(cmd, ct);
            return CreatedAtAction(nameof(Get), new { id }, new { id });
        }
    }
}
