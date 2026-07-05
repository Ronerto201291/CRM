using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeWebPushService : IWebPushService
{
    public bool IsEnabled { get; set; }
    public string? PublicKey { get; set; }
    public List<(Guid UserId, string Title, string Body)> SentToUser { get; } = [];

    public Task SendAsync(PushSubscriptionDto subscription, string title, string body, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendToUserAsync(Guid userId, string title, string body, CancellationToken ct = default)
    {
        SentToUser.Add((userId, title, body));
        return Task.CompletedTask;
    }
}
