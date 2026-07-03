using Erp.Application.Features.Automation.Commands;
using Erp.Application.Features.Automation.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/automation"), Authorize]
public class AutomationController : ControllerBase
{
    private readonly IMediator _mediator;

    public AutomationController(IMediator mediator) => _mediator = mediator;

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules(CancellationToken ct)
        => Ok(await _mediator.Send(new GetRulesQuery(), ct));

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleCommand cmd, CancellationToken ct)
    {
        var id = await _mediator.Send(cmd, ct);
        return Ok(new { id, message = "Regla creada." });
    }

    [HttpPut("rules/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleRule(Guid id, [FromBody] ToggleRuleRequest body, CancellationToken ct)
    {
        var isActive = await _mediator.Send(new ToggleRuleCommand { RuleId = id, IsActive = body.IsActive }, ct);
        return Ok(new { isActive });
    }
}

public class ToggleRuleRequest
{
    public bool IsActive { get; set; }
}
