using Erp.Application.Common.Interfaces;
using Erp.Application.Options;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Services;

/// <summary>No-op cuando WebPush:Enabled=false (#42).</summary>
public sealed class DisabledWebPushService : IWebPushService
{
    public bool IsEnabled => false;
    public string? PublicKey => null;

    public Task SendAsync(PushSubscriptionDto subscription, string title, string body, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendToUserAsync(Guid userId, string title, string body, CancellationToken ct = default)
        => Task.CompletedTask;
}
