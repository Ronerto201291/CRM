using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Alerts;

public record AlertDto(
    Guid Id, string Title, string? Description, DateTime ScheduledAt,
    Guid? ClientId, string? ClientName, bool IsAcknowledged, DateTime? AcknowledgedAt,
    DateTime? SnoozedUntil, DateTime CreatedAt, DateTime? UpdatedAt);

public record GetAlertsQuery : IRequest<IReadOnlyList<AlertDto>>;
public record GetPendingAlertsQuery : IRequest<IReadOnlyList<AlertDto>>;
public record CreateAlertCommand(string Title, string? Description, DateTime ScheduledAt, Guid? ClientId) : IRequest<AlertDto>;
public record UpdateAlertCommand(Guid Id, string Title, string? Description, DateTime ScheduledAt, Guid? ClientId) : IRequest<AlertDto>;
public record AcknowledgeAlertCommand(Guid Id) : IRequest<Unit>;
public record SnoozeAlertCommand(Guid Id, DateTime SnoozedUntil) : IRequest<Unit>;
public record DeleteAlertCommand(Guid Id) : IRequest<Unit>;

public class GetAlertsHandler : IRequestHandler<GetAlertsQuery, IReadOnlyList<AlertDto>>
{
    private readonly ICrmDbContext _db;

    public GetAlertsHandler(ICrmDbContext db) => _db = db;

    public async Task<IReadOnlyList<AlertDto>> Handle(GetAlertsQuery request, CancellationToken ct)
    {
        var alerts = await _db.ScheduledAlerts
            .OrderByDescending(a => a.ScheduledAt)
            .ToListAsync(ct);
        return alerts.Select(AlertMapper.ToDto).ToList();
    }
}

public class GetPendingAlertsHandler : IRequestHandler<GetPendingAlertsQuery, IReadOnlyList<AlertDto>>
{
    private readonly ICrmDbContext _db;

    public GetPendingAlertsHandler(ICrmDbContext db) => _db = db;

    public async Task<IReadOnlyList<AlertDto>> Handle(GetPendingAlertsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var pending = await _db.ScheduledAlerts
            .Where(a => !a.IsAcknowledged &&
                        (a.SnoozedUntil == null ? a.ScheduledAt <= now : a.SnoozedUntil <= now))
            .OrderBy(a => a.ScheduledAt)
            .ToListAsync(ct);
        return pending.Select(AlertMapper.ToDto).ToList();
    }
}

public class CreateAlertHandler : IRequestHandler<CreateAlertCommand, AlertDto>
{
    private readonly ICrmDbContext _db;
    private readonly ITenantContext _tenant;

    public CreateAlertHandler(ICrmDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<AlertDto> Handle(CreateAlertCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Title is required");

        string? clientName = null;
        if (request.ClientId.HasValue)
            clientName = await _db.Clients
                .Where(c => c.Id == request.ClientId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct);

        var alert = new ScheduledAlert
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved"),
            Title = request.Title,
            Description = request.Description,
            ScheduledAt = request.ScheduledAt.ToUniversalTime(),
            ClientId = request.ClientId,
            ClientName = clientName,
        };

        _db.ScheduledAlerts.Add(alert);
        await _db.SaveChangesAsync(ct);
        return AlertMapper.ToDto(alert);
    }
}

public class UpdateAlertHandler : IRequestHandler<UpdateAlertCommand, AlertDto>
{
    private readonly ICrmDbContext _db;

    public UpdateAlertHandler(ICrmDbContext db) => _db = db;

    public async Task<AlertDto> Handle(UpdateAlertCommand request, CancellationToken ct)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Alert not found");

        if (request.ClientId.HasValue && request.ClientId != alert.ClientId)
            alert.ClientName = await _db.Clients
                .Where(c => c.Id == request.ClientId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct);
        else if (!request.ClientId.HasValue)
            alert.ClientName = null;

        alert.Title = request.Title;
        alert.Description = request.Description;
        alert.ScheduledAt = request.ScheduledAt.ToUniversalTime();
        alert.ClientId = request.ClientId;
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return AlertMapper.ToDto(alert);
    }
}

public class AcknowledgeAlertHandler : IRequestHandler<AcknowledgeAlertCommand, Unit>
{
    private readonly ICrmDbContext _db;

    public AcknowledgeAlertHandler(ICrmDbContext db) => _db = db;

    public async Task<Unit> Handle(AcknowledgeAlertCommand request, CancellationToken ct)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Alert not found");

        alert.IsAcknowledged = true;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class SnoozeAlertHandler : IRequestHandler<SnoozeAlertCommand, Unit>
{
    private readonly ICrmDbContext _db;

    public SnoozeAlertHandler(ICrmDbContext db) => _db = db;

    public async Task<Unit> Handle(SnoozeAlertCommand request, CancellationToken ct)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Alert not found");

        alert.SnoozedUntil = request.SnoozedUntil.ToUniversalTime();
        alert.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class DeleteAlertHandler : IRequestHandler<DeleteAlertCommand, Unit>
{
    private readonly ICrmDbContext _db;

    public DeleteAlertHandler(ICrmDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteAlertCommand request, CancellationToken ct)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException("Alert not found");

        _db.ScheduledAlerts.Remove(alert);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

internal static class AlertMapper
{
    internal static AlertDto ToDto(ScheduledAlert a) => new(
        a.Id, a.Title, a.Description, a.ScheduledAt,
        a.ClientId, a.ClientName, a.IsAcknowledged, a.AcknowledgedAt,
        a.SnoozedUntil, a.CreatedAt, a.UpdatedAt);
}
