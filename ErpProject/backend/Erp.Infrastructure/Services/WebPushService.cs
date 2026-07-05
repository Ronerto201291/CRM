using Erp.Application.Common.Interfaces;
using Erp.Application.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;
using WebPushSubscription = WebPush.PushSubscription;

namespace Erp.Infrastructure.Services;

/// <summary>Envío Web Push VAPID (#42).</summary>
public sealed class WebPushService : IWebPushService
{
    private readonly IApplicationDbContext _ctx;
    private readonly WebPushOptions _options;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(IApplicationDbContext ctx, IOptions<WebPushOptions> options, ILogger<WebPushService> logger)
    {
        _ctx = ctx;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.VapidPublicKey)
        && !string.IsNullOrWhiteSpace(_options.VapidPrivateKey);

    public string? PublicKey => IsEnabled ? _options.VapidPublicKey : null;

    public async Task SendAsync(PushSubscriptionDto subscription, string title, string body, CancellationToken ct = default)
    {
        if (!IsEnabled) return;

        try
        {
            var client = new WebPushClient();
            var vapid = new VapidDetails(_options.VapidSubject, _options.VapidPublicKey, _options.VapidPrivateKey);
            var pushSub = new WebPushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body });
            await client.SendNotificationAsync(pushSub, payload, vapid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Web push failed for endpoint {Endpoint}", subscription.Endpoint[..Math.Min(40, subscription.Endpoint.Length)]);
        }
    }

    public async Task SendToUserAsync(Guid userId, string title, string body, CancellationToken ct = default)
    {
        if (!IsEnabled) return;

        var subs = await _ctx.PushSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.UserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var sub in subs)
        {
            await SendAsync(new PushSubscriptionDto(sub.Endpoint, sub.P256dh, sub.Auth), title, body, ct);
        }
    }
}
