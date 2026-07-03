using Erp.Modules.Treasury.Application.Features.Financing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/financing")]
[Authorize]
public class FinancingController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinancingController(IMediator mediator) => _mediator = mediator;

    [HttpGet("confirming")]
    public async Task<IActionResult> GetConfirming([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetConfirmingQuery(status), ct));

    [HttpPost("confirming")]
    public async Task<IActionResult> CreateConfirming([FromBody] CreateConfirmingDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateConfirmingCommand(
            dto.SupplierId, dto.InvoiceId, dto.InvoiceAmount,
            dto.AdvancePercentage, dto.Fee, dto.DueDate, dto.FinancingProvider), ct);
        return Created("", result);
    }

    [HttpPatch("confirming/{id:guid}/pay")]
    public async Task<IActionResult> PayConfirming(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new PayConfirmingCommand(id), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("factoring")]
    public async Task<IActionResult> GetFactoring([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetFactoringQuery(status), ct));

    [HttpPost("factoring")]
    public async Task<IActionResult> CreateFactoring([FromBody] CreateFactoringDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateFactoringCommand(
            dto.ClientId, dto.InvoiceId, dto.InvoiceAmount,
            dto.AdvancePercentage, dto.DiscountFee, dto.CommissionAmount,
            dto.DueDate, dto.FactoringProvider, dto.IsWithRecourse), ct);
        return Created("", result);
    }

    [HttpPatch("factoring/{id:guid}/pay")]
    public async Task<IActionResult> PayFactoring(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new PayFactoringCommand(id), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("credit-lines")]
    public async Task<IActionResult> GetCreditLines(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCreditLinesQuery(), ct));

    [HttpPost("credit-lines")]
    public async Task<IActionResult> CreateCreditLine([FromBody] CreateCreditLineDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCreditLineCommand(
            dto.Type, dto.Limit, dto.InterestRate,
            dto.StartDate, dto.ExpiryDate, dto.Provider), ct);
        return Created("", result);
    }
}

public record CreateConfirmingDto(
    Guid SupplierId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal Fee, DateTime DueDate, string FinancingProvider);

public record CreateFactoringDto(
    Guid ClientId, Guid InvoiceId, decimal InvoiceAmount,
    decimal AdvancePercentage, decimal DiscountFee, decimal CommissionAmount,
    DateTime DueDate, string FactoringProvider, bool IsWithRecourse);

public record CreateCreditLineDto(
    string Type, decimal Limit, decimal InterestRate,
    DateTime StartDate, DateTime ExpiryDate, string Provider);
