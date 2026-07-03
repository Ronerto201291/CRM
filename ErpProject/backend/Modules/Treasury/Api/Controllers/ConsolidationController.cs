using Erp.Modules.Treasury.Application.Features.Consolidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/consolidation")]
[Authorize]
public class ConsolidationController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConsolidationController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetConsolidationGroupsQuery(), ct));

    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateConsolidationGroupCommand(
            dto.Name, dto.Code, dto.ConsolidationPercentage, dto.Method), ct);
        return Created("", result);
    }

    [HttpGet("{groupId:guid}/subsidiaries")]
    public async Task<IActionResult> GetSubsidiaries(Guid groupId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSubsidiariesQuery(groupId), ct));

    [HttpPost("{groupId:guid}/subsidiaries")]
    public async Task<IActionResult> AddSubsidiary(Guid groupId, [FromBody] AddSubsidiaryDto dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new AddSubsidiaryCommand(
            groupId, dto.CompanyId, dto.OwnershipPercentage,
            dto.VotingPercentage, dto.ConsolidationMethod,
            dto.AcquisitionDate, dto.AcquisitionPrice), ct);
        return Created("", result);
    }

    [HttpGet("{groupId:guid}/financial-statements")]
    public async Task<IActionResult> GetFinancialStatements(Guid groupId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetConsolidatedStatementsQuery(groupId), ct));

    [HttpPost("{groupId:guid}/consolidate")]
    public async Task<IActionResult> ConsolidateGroup(Guid groupId, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new ConsolidateGroupCommand(groupId), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{groupId:guid}/intercompany-transactions")]
    public async Task<IActionResult> GetIntercompanyTransactions(Guid groupId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetIntercompanyTransactionsQuery(groupId), ct));

    [HttpPost("{groupId:guid}/eliminate-intercompany")]
    public async Task<IActionResult> EliminateIntercompanyTransactions(Guid groupId, CancellationToken ct)
        => Ok(await _mediator.Send(new EliminateIntercompanyCommand(groupId), ct));
}

public record CreateGroupDto(string Name, string Code, decimal ConsolidationPercentage, string Method);

public record AddSubsidiaryDto(
    Guid CompanyId, decimal OwnershipPercentage, decimal VotingPercentage,
    string ConsolidationMethod, DateTime AcquisitionDate, decimal AcquisitionPrice);

public record EliminateDto(Guid GroupId);
