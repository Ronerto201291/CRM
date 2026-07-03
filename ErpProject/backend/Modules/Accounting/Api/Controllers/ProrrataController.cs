using Erp.Modules.Accounting.Application.Features.Vat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/prorrata")]
[Authorize]
public class ProrrataController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProrrataController(IMediator mediator) => _mediator = mediator;

    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateProrrata([FromBody] CalculateProrrataApiRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CalculateProrrataCommand
        {
            FiscalYear = request.FiscalYear > 0 ? request.FiscalYear : DateTime.UtcNow.Year,
            InlandRevenue = request.InlandRevenue,
            ExemptRevenue = request.ExemptRevenue,
            Type = request.Type ?? "General"
        }, ct);

        return Created(string.Empty, result);
    }

    [HttpGet("types")]
    public IActionResult GetProrrataTypes()
    {
        return Ok(new[]
        {
            new { id = "General", name = "Prorrata General", description = "Para empresas con operaciones mixtas" },
            new { id = "Special", name = "Prorrata Especial", description = "Sectores específicos" }
        });
    }
}

public class CalculateProrrataApiRequest
{
    public int FiscalYear { get; set; }
    public decimal InlandRevenue { get; set; }
    public decimal ExemptRevenue { get; set; }
    public string? Type { get; set; }
}
