using Erp.Application.Features.ApiKeys.Commands;
using Erp.Application.Features.ApiKeys.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class ApiKeysController : ControllerBase
{
    private readonly IMediator _mediator;
    public ApiKeysController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetApiKeysQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApiKeyCommand cmd, CancellationToken ct)
        => Ok(await _mediator.Send(cmd, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new RevokeApiKeyCommand { Id = id }, ct);
        return ok ? Ok(new { message = "API key revocada correctamente." }) : NotFound();
    }
}
