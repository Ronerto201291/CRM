using Erp.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Notifications;

public record NotificationSettingsDto(string Frequency, bool PushEnabled, string? PushPublicKey);

public record GetNotificationSettingsQuery : IRequest<NotificationSettingsDto>;

public record UpdateNotificationSettingsCommand(string Frequency) : IRequest<bool>;

public record SubscribePushCommand(string Endpoint, string P256dh, string Auth) : IRequest<bool>;

public record UnsubscribePushCommand(string Endpoint) : IRequest<bool>;

public record GetPushPublicKeyQuery : IRequest<string?>;

public sealed class GetNotificationSettingsHandler : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IWebPushService _push;

    public GetNotificationSettingsHandler(IApplicationDbContext ctx, ITenantContext tenant, IWebPushService push)
    {
        _ctx = ctx;
        _tenant = tenant;
        _push = push;
    }

    public async Task<NotificationSettingsDto> Handle(GetNotificationSettingsQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var freq = await _ctx.Companies.IgnoreQueryFilters()
            .Where(c => c.Id == companyId)
            .Select(c => c.ProactiveNotificationsFrequency)
            .FirstOrDefaultAsync(ct) ?? "daily";
        return new NotificationSettingsDto(freq, _push.IsEnabled, _push.PublicKey);
    }
}

public sealed class UpdateNotificationSettingsHandler : IRequestHandler<UpdateNotificationSettingsCommand, bool>
{
    private static readonly HashSet<string> Allowed = ["disabled", "daily", "weekly"];
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public UpdateNotificationSettingsHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<bool> Handle(UpdateNotificationSettingsCommand request, CancellationToken ct)
    {
        if (!Allowed.Contains(request.Frequency))
            throw new InvalidOperationException("Frecuencia inválida. Use: disabled, daily, weekly.");

        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException();
        var company = await _ctx.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company is null) return false;

        company.ProactiveNotificationsFrequency = request.Frequency;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class SubscribePushHandler : IRequestHandler<SubscribePushCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IHttpContextCurrentUserAccessor _currentUser;

    public SubscribePushHandler(
        IApplicationDbContext ctx, ITenantContext tenant, IHttpContextCurrentUserAccessor currentUser)
    {
        _ctx = ctx;
        _tenant = tenant;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(SubscribePushCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException();

        var existing = await _ctx.PushSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint, ct);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.CompanyId = companyId;
            existing.P256dh = request.P256dh;
            existing.Auth = request.Auth;
        }
        else
        {
            _ctx.PushSubscriptions.Add(new Erp.Domain.Entities.Core.PushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CompanyId = companyId,
                Endpoint = request.Endpoint,
                P256dh = request.P256dh,
                Auth = request.Auth,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class UnsubscribePushHandler : IRequestHandler<UnsubscribePushCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IHttpContextCurrentUserAccessor _currentUser;

    public UnsubscribePushHandler(IApplicationDbContext ctx, IHttpContextCurrentUserAccessor currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(UnsubscribePushCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var sub = await _ctx.PushSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint && s.UserId == userId, ct);
        if (sub is null) return false;

        _ctx.PushSubscriptions.Remove(sub);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class GetPushPublicKeyHandler : IRequestHandler<GetPushPublicKeyQuery, string?>
{
    private readonly IWebPushService _push;

    public GetPushPublicKeyHandler(IWebPushService push) => _push = push;

    public Task<string?> Handle(GetPushPublicKeyQuery request, CancellationToken ct)
        => Task.FromResult(_push.PublicKey);
}
