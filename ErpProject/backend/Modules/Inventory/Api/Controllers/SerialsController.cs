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
    [Route("api/v{version:apiVersion}/inventory/serials")]
    [ApiVersion("1.0")]
    [Authorize]
    [RequiredModule("Inventory")]
    public class SerialsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SerialsController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        [RequirePermission(Permissions.Serial.Read)]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => Ok(await _mediator.Send(new GetSerialsQuery(), ct));

        [HttpGet("{id}")]
        [RequirePermission(Permissions.Serial.Read)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        {
            var serial = await _mediator.Send(new GetSerialByIdQuery { Id = id }, ct);
            return serial == null ? NotFound() : Ok(serial);
        }

        [HttpPost]
        [RequirePermission(Permissions.Serial.Create)]
        public async Task<IActionResult> Create([FromBody] CreateSerialCommand cmd, CancellationToken ct)
        {
            var serial = await _mediator.Send(cmd, ct);
            return CreatedAtAction(nameof(Get), new { id = serial.Id }, serial);
        }

        [HttpPut("{id}/status")]
        [RequirePermission(Permissions.Serial.Update)]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSerialStatusBody body, CancellationToken ct)
        {
            var serial = await _mediator.Send(new UpdateSerialStatusCommand { Id = id, Status = body.Status }, ct);
            return serial == null ? NotFound() : Ok(serial);
        }

        [HttpDelete("{id}")]
        [RequirePermission(Permissions.Serial.Delete)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var ok = await _mediator.Send(new DeleteSerialCommand { Id = id }, ct);
            return ok ? NoContent() : NotFound();
        }
    }

    public class UpdateSerialStatusBody
    {
        public string Status { get; set; } = string.Empty;
    }
}
