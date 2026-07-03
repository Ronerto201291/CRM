using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ClientsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetClientsQuery
        {
            SearchTerm = search,
            Page = page,
            PageSize = pageSize,
        }, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClientCommand command)
        => Created("", await _mediator.Send(command));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteClientCommand { Id = id });
        return result ? NoContent() : NotFound();
    }

    /// <summary>POST /api/clients/{id}/anonymize — RGPD Art. 17 derecho de supresión.</summary>
    [HttpPost("{id}/anonymize")]
    public async Task<IActionResult> Anonymize(Guid id)
    {
        var result = await _mediator.Send(new AnonymizeClientCommand { Id = id });
        return result ? Ok(new { message = "Datos personales anonimizados correctamente." }) : NotFound();
    }
}
