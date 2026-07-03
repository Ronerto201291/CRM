using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Inventory.API.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/inventory/lots")]
    [ApiVersion("1.0")]
    public class LotsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public LotsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await _mediator.Send(new GetLotsQuery(), ct));

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var lot = await _mediator.Send(new GetLotByIdQuery { Id = id }, ct);
            return lot == null ? NotFound() : Ok(lot);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLotCommand cmd, CancellationToken ct)
        {
            var lot = await _mediator.Send(cmd, ct);
            return CreatedAtAction(nameof(Get), new { id = lot.Id }, lot);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateLotCommand cmd, CancellationToken ct)
        {
            var lot = await _mediator.Send(new UpdateLotCommand
            {
                Id = id,
                LotNumber = cmd.LotNumber,
                ExpirationDate = cmd.ExpirationDate,
                Quantity = cmd.Quantity,
                UnitCost = cmd.UnitCost,
            }, ct);
            return lot == null ? NotFound() : Ok(lot);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var ok = await _mediator.Send(new DeleteLotCommand { Id = id }, ct);
            return ok ? NoContent() : NotFound();
        }
    }
}
