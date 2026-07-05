using Erp.Application.Common.Interfaces;
using Erp.Application.Options;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erp.Tests.Notifications;

/// <summary>
/// La implementación REAL de <see cref="WebPushService"/> no tenía ningún test — solo
/// `DisabledWebPushService` estaba cubierto (contra-auditoría jul 2026). No se ejercita el
/// envío real contra la red (dependería de un endpoint push real), pero sí toda la lógica
/// determinista: resolución de `IsEnabled`/`PublicKey` a partir de configuración, y que el
/// servicio no intenta enviar nada (ni lanza) cuando está deshabilitado.
/// </summary>
public class WebPushServiceRealImplementationTests
{
    [Fact]
    public void IsEnabled_False_WhenOptionDisabled()
    {
        var service = CreateService(new WebPushOptions { Enabled = false });
        Assert.False(service.IsEnabled);
        Assert.Null(service.PublicKey);
    }

    [Fact]
    public void IsEnabled_False_WhenEnabledButMissingVapidKeys()
    {
        var service = CreateService(new WebPushOptions { Enabled = true, VapidPublicKey = "", VapidPrivateKey = "" });
        Assert.False(service.IsEnabled);
        Assert.Null(service.PublicKey);
    }

    [Fact]
    public void IsEnabled_True_WhenEnabledWithBothVapidKeys()
    {
        var service = CreateService(new WebPushOptions
        {
            Enabled = true,
            VapidPublicKey = "public-key",
            VapidPrivateKey = "private-key",
        });

        Assert.True(service.IsEnabled);
        Assert.Equal("public-key", service.PublicKey);
    }

    [Fact]
    public async Task SendToUserAsync_Disabled_DoesNothing_NoException()
    {
        var service = CreateService(new WebPushOptions { Enabled = false });

        await service.SendToUserAsync(Guid.NewGuid(), "Título", "Cuerpo", CancellationToken.None);
        // No debe lanzar ni intentar resolver suscripciones cuando está deshabilitado.
    }

    [Fact]
    public async Task SendAsync_Disabled_DoesNothing_NoException()
    {
        var service = CreateService(new WebPushOptions { Enabled = false });

        await service.SendAsync(
            new PushSubscriptionDto("https://example.com/endpoint", "p256dh", "auth"),
            "Título", "Cuerpo", CancellationToken.None);
    }

    private static WebPushService CreateService(WebPushOptions options)
    {
        var tenant = new FakeTenantContext();
        var dbOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"webpush-{Guid.NewGuid()}")
            .Options;
        var ctx = new ErpDbContext(dbOptions, tenant);
        return new WebPushService(ctx, Options.Create(options), NullLogger<WebPushService>.Instance);
    }
}
