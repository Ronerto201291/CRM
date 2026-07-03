using Erp.Modules.Accounting.Application.Features.Vat;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/vat")]
[Authorize]
public class VatController : ControllerBase
{
    private readonly IMediator _mediator;

    public VatController(IMediator mediator) => _mediator = mediator;

    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateVat([FromBody] CalculateVatRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CalculateVatCommand
        {
            Amount = request.Amount,
            VatType = request.VatType ?? "Standard"
        }, ct);

        return Ok(new
        {
            id = result.TransactionId,
            amount = result.Amount,
            vatRate = result.VatRate,
            vatAmount = result.VatAmount,
            total = result.Total,
            status = "Calculated"
        });
    }

    [HttpGet("rates")]
    public IActionResult GetVatRates()
    {
        return Ok(new[]
        {
            new { type = "Standard", rate = 0.21m, applies = "General supplies" },
            new { type = "Reduced", rate = 0.10m, applies = "Food, books" },
            new { type = "SuperReduced", rate = 0.04m, applies = "Essential goods" },
            new { type = "Zero", rate = 0m, applies = "Exports" }
        });
    }

    [HttpGet("regime")]
    public async Task<IActionResult> GetCurrentRegime(CancellationToken ct)
    {
        var regime = await _mediator.Send(new GetCurrentVatRegimeQuery(), ct);
        return regime is null ? Ok(new { type = "Standard", isActive = true }) : Ok(regime);
    }

    [HttpGet("regimes")]
    public async Task<IActionResult> GetRegimes(CancellationToken ct)
        => Ok(await _mediator.Send(new GetVatRegimesQuery(), ct));

    [HttpPost("regime")]
    public async Task<IActionResult> SetRegime([FromBody] SetVatRegimeRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SetVatRegimeCommand(
            request.Type,
            request.EffectiveDate ?? DateTime.UtcNow), ct);
        return Ok(result);
    }

    /// <summary>
    /// Registra una autoliquidación IVA trimestral.
    /// Nombre legacy «modelo330» — el modelo vigente es el 303 (el 330 quedó obsoleto en 2014).
    /// </summary>
    [HttpPost("declare/modelo330")]
    public async Task<IActionResult> DeclareModelo330([FromBody] DeclareModelo330Request request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(
                new DeclareModelo330Command(request.Year, request.Quarter), ct);
            return Created("", result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class CalculateVatRequest
{
    public decimal Amount { get; set; }
    public decimal VatRate { get; set; } = 0.21m;
    public string? VatType { get; set; }
}

public class SetVatRegimeRequest
{
    public string Type { get; set; } = "Standard";
    public DateTime? EffectiveDate { get; set; }
}

public class DeclareModelo330Request
{
    public int Year { get; set; }
    public int Quarter { get; set; } = 1;
}
