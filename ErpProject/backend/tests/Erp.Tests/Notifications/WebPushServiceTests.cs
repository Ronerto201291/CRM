using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Services;
using Xunit;

namespace Erp.Tests.Notifications;

public class WebPushServiceTests
{
    [Fact]
    public void DisabledWebPushService_IsNotEnabled()
    {
        var svc = new DisabledWebPushService();
        Assert.False(svc.IsEnabled);
        Assert.Null(svc.PublicKey);
    }

    [Fact]
    public async Task DisabledWebPushService_SendIsNoOp()
    {
        var svc = new DisabledWebPushService();
        await svc.SendAsync(new PushSubscriptionDto("https://x", "k", "a"), "t", "b");
        await svc.SendToUserAsync(Guid.NewGuid(), "t", "b");
    }
}
