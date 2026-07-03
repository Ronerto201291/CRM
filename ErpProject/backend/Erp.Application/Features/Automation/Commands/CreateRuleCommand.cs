using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Automation;
using MediatR;

namespace Erp.Application.Features.Automation.Commands;

public class CreateRuleCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerEvent { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string ConditionField { get; set; } = string.Empty;
    public string ConditionOperator { get; set; } = string.Empty;
    public string ConditionValue { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? ActionConfiguration { get; set; }
}

public class CreateRuleHandler : IRequestHandler<CreateRuleCommand, Guid>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateRuleHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateRuleCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = req.Name,
            Description = req.Description ?? string.Empty,
            TriggerEvent = req.TriggerEvent,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(req.ConditionField))
        {
            rule.Conditions.Add(new Condition
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                Field = req.ConditionField,
                Operator = req.ConditionOperator,
                Value = req.ConditionValue
            });
        }

        if (!string.IsNullOrWhiteSpace(req.ActionType))
        {
            rule.Actions.Add(new Erp.Domain.Entities.Automation.Action
            {
                Id = Guid.NewGuid(),
                RuleId = rule.Id,
                Type = req.ActionType,
                Configuration = req.ActionConfiguration ?? "{}",
                ExecutionOrder = 0
            });
        }

        _ctx.Rules.Add(rule);
        await _ctx.SaveChangesAsync(ct);
        return rule.Id;
    }
}
