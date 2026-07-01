using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class ContactsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ContactsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? supplierId,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetContactsQuery { ClientId = clientId, SupplierId = supplierId, Search = search }, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetContactByIdQuery { Id = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Created($"/api/contacts/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        return await _mediator.Send(cmd, ct) ? Ok(new { message = "Contacto actualizado." }) : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _mediator.Send(new DeleteContactCommand { Id = id }, ct) ? NoContent() : NotFound();

    /// <summary>POST /api/contacts/{id}/anonymize — RGPD Art. 17 derecho de supresión.</summary>
    [HttpPost("{id:guid}/anonymize")]
    public async Task<IActionResult> Anonymize(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new AnonymizeContactCommand { Id = id }, ct);
        return result ? Ok(new { message = "Datos personales anonimizados correctamente." }) : NotFound();
    }
}
