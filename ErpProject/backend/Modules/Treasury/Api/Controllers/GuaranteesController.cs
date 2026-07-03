using Erp.Modules.Treasury.Application.Features.Guarantees;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/guarantees")]
[Authorize]
public class GuaranteesController : ControllerBase
{
    private readonly IMediator _mediator;

    public GuaranteesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetGuaranteesQuery(status), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGuaranteeDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateGuaranteeCommand(
            dto.Type, dto.ReferenceNumber, dto.Amount, dto.CurrencyCode,
            dto.RelatedEntity, dto.Description, dto.IssueDate, dto.ExpiryDate), ct);
        return Created("", result);
    }

    [HttpPatch("{id:guid}/claim")]
    public async Task<IActionResult> ClaimGuarantee(Guid id, [FromBody] ClaimGuaranteeDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new ClaimGuaranteeCommand(id, dto.Amount), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPatch("{id:guid}/release")]
    public async Task<IActionResult> ReleaseGuarantee(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new ReleaseGuaranteeCommand(id), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("collateral")]
    public async Task<IActionResult> GetCollateral(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCollateralQuery(), ct));

    [HttpPost("collateral")]
    public async Task<IActionResult> CreateCollateral([FromBody] CreateCollateralDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateCollateralCommand(
            dto.Type, dto.Description, dto.Value, dto.LinkedAccount, dto.LTVRatio), ct);
        return Created("", result);
    }

    [HttpGet("bank-guarantees")]
    public async Task<IActionResult> GetBankGuarantees([FromQuery] string? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetBankGuaranteesQuery(status), ct));

    [HttpPost("bank-guarantees")]
    public async Task<IActionResult> CreateBankGuarantee([FromBody] CreateBankGuaranteeDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateBankGuaranteeCommand(
            dto.GuaranteeNumber, dto.Bank, dto.Amount, dto.Type,
            dto.IssuedDate, dto.ExpiryDate, dto.BeneficiaryId,
            dto.BeneficiaryName, dto.Fee), ct);
        return Created("", result);
    }
}

public record CreateGuaranteeDto(
    string Type, string ReferenceNumber, decimal Amount, string CurrencyCode,
    string RelatedEntity, string Description, DateTime IssueDate, DateTime ExpiryDate);

public record ClaimGuaranteeDto(decimal Amount);

public record CreateCollateralDto(
    string Type, string Description, decimal Value, string LinkedAccount, decimal LTVRatio);

public record CreateBankGuaranteeDto(
    string GuaranteeNumber, string Bank, decimal Amount, string Type,
    DateTime IssuedDate, DateTime ExpiryDate, Guid? BeneficiaryId,
    string BeneficiaryName, decimal Fee);
