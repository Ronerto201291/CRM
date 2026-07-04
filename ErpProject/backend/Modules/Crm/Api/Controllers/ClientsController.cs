using Erp.Application.Common.Attributes;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Application.Features.Services.Commands;
using Erp.Modules.Crm.Application.Features.Services.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequiredModule("CRM")]
public class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ClientsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Client.Read)]
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
    [RequirePermission(Permissions.Client.Create)]
    public async Task<IActionResult> Create([FromBody] CreateClientCommand command)
        => Created("", await _mediator.Send(command));

    [HttpPut("{id}")]
    [RequirePermission(Permissions.Client.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    [HttpDelete("{id}")]
    [RequirePermission(Permissions.Client.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteClientCommand { Id = id });
        return result ? NoContent() : NotFound();
    }

    /// <summary>POST /api/clients/{id}/anonymize — RGPD Art. 17 derecho de supresión.</summary>
    [HttpPost("{id}/anonymize")]
    [RequirePermission(Permissions.Client.Anonymize)]
    public async Task<IActionResult> Anonymize(Guid id)
    {
        var result = await _mediator.Send(new AnonymizeClientCommand { Id = id });
        return result ? Ok(new { message = "Datos personales anonimizados correctamente." }) : NotFound();
    }

    [HttpGet("{id}/contracted-services")]
    [RequirePermission(Permissions.ContractedService.Read)]
    public async Task<IActionResult> GetContractedServices(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetClientContractedServicesQuery { ClientId = id }, ct));

    [HttpPost("{id}/contracted-services")]
    [RequirePermission(Permissions.ContractedService.Create)]
    public async Task<IActionResult> AddContractedService(Guid id, [FromBody] CreateClientContractedServiceCommand command, CancellationToken ct)
    {
        command.ClientId = id;
        return Created("", await _mediator.Send(command, ct));
    }

    [HttpDelete("{id}/contracted-services/{contractId}")]
    [RequirePermission(Permissions.ContractedService.Delete)]
    public async Task<IActionResult> CancelContractedService(Guid id, Guid contractId, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelClientContractedServiceCommand { Id = contractId }, ct);
        return result ? NoContent() : NotFound();
    }
}
