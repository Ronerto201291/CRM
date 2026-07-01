using Erp.Application.Features.Company.Commands;
using Erp.Application.Features.Company.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class CompanyController : ControllerBase
{
    private readonly IMediator _mediator;
    public CompanyController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCompanyQuery(), ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCompanyCommand cmd, CancellationToken ct)
    {
        var ok = await _mediator.Send(cmd, ct);
        return ok ? Ok(new { message = "Empresa actualizada correctamente." }) : NotFound();
    }

    [HttpPost("regenerate-token")]
    public async Task<IActionResult> RegenerateToken(CancellationToken ct)
    {
        var token = await _mediator.Send(new RegenerateTokenCommand(), ct);
        return Ok(new { publicUploadToken = token, message = "Token regenerado. El QR anterior ya no es válido." });
    }
}
