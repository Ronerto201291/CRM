using Erp.Application.Common.Interfaces;
using Erp.Application.Features.AuditLogs.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.AuditLogs.Handlers;

public class GetAuditLogsHandler : IRequestHandler<GetAuditLogsQuery, AuditLogsResult>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetAuditLogsHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<AuditLogsResult> Handle(GetAuditLogsQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var query = _ctx.AuditLogs
            .Include(a => a.User)
            .Where(a => a.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action.Contains(request.Action));
        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(a => a.Entity.Contains(request.EntityType));
        if (request.DateFrom.HasValue)
            query = query.Where(a => a.Timestamp >= request.DateFrom.Value.ToUniversalTime());
        if (request.DateTo.HasValue)
            query = query.Where(a => a.Timestamp <= request.DateTo.Value.ToUniversalTime().AddDays(1));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id, Action = a.Action, Entity = a.Entity, EntityId = a.EntityId,
                Timestamp = a.Timestamp, OldValues = a.OldValues, NewValues = a.NewValues,
                UserEmail = a.User != null ? a.User.Email : "system"
            })
            .ToListAsync(ct);

        return new AuditLogsResult
        {
            Total = total, Page = request.Page, PageSize = request.PageSize, Items = items
        };
    }
}
