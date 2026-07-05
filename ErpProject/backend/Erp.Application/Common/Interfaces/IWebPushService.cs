namespace Erp.Application.Common.Interfaces;

public sealed record PushSubscriptionDto(string Endpoint, string P256dh, string Auth);

public interface IWebPushService
{
    bool IsEnabled { get; }

    string? PublicKey { get; }

    Task SendAsync(PushSubscriptionDto subscription, string title, string body, CancellationToken ct = default);

    Task SendToUserAsync(Guid userId, string title, string body, CancellationToken ct = default);
}
