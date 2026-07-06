using Erp.Application.Common.Attributes;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Modules.Inventory.Api.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/inventory/lots")]
    [ApiVersion("1.0")]
    [Authorize]
    [RequiredModule("Inventory")]
    public class LotsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public LotsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [RequirePermission(Permissions.Lot.Read)]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await _mediator.Send(new GetLotsQuery(), ct));

        [HttpGet("{id}")]
        [RequirePermission(Permissions.Lot.Read)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var lot = await _mediator.Send(new GetLotByIdQuery { Id = id }, ct);
            return lot == null ? NotFound() : Ok(lot);
        }

        [HttpPost]
        [RequirePermission(Permissions.Lot.Create)]
        public async Task<IActionResult> Create([FromBody] CreateLotCommand cmd, CancellationToken ct)
        {
            var lot = await _mediator.Send(cmd, ct);
            return CreatedAtAction(nameof(Get), new { id = lot.Id }, lot);
        }

        [HttpPut("{id}")]
        [RequirePermission(Permissions.Lot.Update)]
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
        [RequirePermission(Permissions.Lot.Delete)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var ok = await _mediator.Send(new DeleteLotCommand { Id = id }, ct);
            return ok ? NoContent() : NotFound();
        }
    }
}
