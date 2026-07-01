using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Automation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Automation.Commands;

public class CreateRuleCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
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
    public CreateRuleHandler(IApplicationDbContext ctx) => _ctx = ctx;
    public async Task<Guid> Handle(CreateRuleCommand req, CancellationToken ct)
    {
        // We need a DbSet for Rules - for now use direct access through DbContext
        var rule = new Rule
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            IsActive = req.IsActive
        };
        // Will be enhanced when we add Rules to IApplicationDbContext
        await _ctx.SaveChangesAsync(ct);
        return rule.Id;
    }
}
