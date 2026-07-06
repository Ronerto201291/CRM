using Erp.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Automation.Commands;

public class ToggleRuleCommand : IRequest<bool>
{
    public Guid RuleId { get; set; }
    public bool IsActive { get; set; }
}

public class ToggleRuleHandler : IRequestHandler<ToggleRuleCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public ToggleRuleHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<bool> Handle(ToggleRuleCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new UnauthorizedAccessException("No tenant context.");

        var rule = await _ctx.Rules
            .FirstOrDefaultAsync(r => r.Id == req.RuleId && r.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("Regla no encontrada.");

        rule.IsActive = req.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return rule.IsActive;
    }
}
