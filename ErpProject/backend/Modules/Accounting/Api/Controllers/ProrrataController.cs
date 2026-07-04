using Erp.Application.Common.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Erp.Modules.Accounting.Application.Features.Vat;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/prorrata")]
[Authorize]
[RequiredModule("Accounting")]
public class ProrrataController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProrrataController(IMediator mediator) => _mediator = mediator;

    [HttpPost("calculate")]
    [RequirePermission(Permissions.Prorrata.Manage)]
    public async Task<IActionResult> CalculateProrrata([FromBody] CalculateProrrataCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Created("", result);
    }

    [HttpGet("types")]
    [RequirePermission(Permissions.Prorrata.Read)]
    public IActionResult GetProrrataTypes()
    {
        return Ok(new[]
        {
            new { id = "General", name = "Prorrata General", description = "Para empresas con operaciones mixtas" },
            new { id = "Special", name = "Prorrata Especial", description = "Sectores específicos" }
        });
    }
}

