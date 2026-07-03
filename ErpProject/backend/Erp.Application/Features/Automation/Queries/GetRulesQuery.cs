using Erp.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Automation.Queries;

public class GetRulesQuery : IRequest<List<RuleDto>>
{
}

public class RuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ConditionsSummary { get; set; } = string.Empty;
    public string ActionsSummary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class GetRulesHandler : IRequestHandler<GetRulesQuery, List<RuleDto>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetRulesHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<List<RuleDto>> Handle(GetRulesQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        return await _ctx.Rules
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new RuleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                TriggerEvent = r.TriggerEvent,
                IsActive = r.IsActive,
                ConditionsSummary = r.Conditions.Any()
                    ? string.Join("; ", r.Conditions.Select(c => $"{c.Field} {c.Operator} {c.Value}"))
                    : "—",
                ActionsSummary = r.Actions.Any()
                    ? string.Join("; ", r.Actions.OrderBy(a => a.ExecutionOrder).Select(a => a.Type))
                    : "—",
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }
}
