using Erp.Application.Features.Notifications;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Notifications;

/// <summary>
/// NotificationHandlers.cs (settings + suscripción push) no tenía ningún test — contra-auditoría
/// jul 2026. Cubre los 5 handlers reales (no solo GetPushPublicKeyHandler trivial).
/// </summary>
public class NotificationHandlersTests
{
    [Fact]
    public async Task GetNotificationSettings_ReturnsCompanyFrequencyAndPushState()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Empresa test", TaxId = "B11111111", IsActive = true, Country = "ES", ProactiveNotificationsFrequency = "weekly" });
        await ctx.SaveChangesAsync();

        var push = new FakeWebPushService { IsEnabled = true, PublicKey = "pub-key" };
        var handler = new GetNotificationSettingsHandler(ctx, tenant, push);

        var result = await handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        Assert.Equal("weekly", result.Frequency);
        Assert.True(result.PushEnabled);
        Assert.Equal("pub-key", result.PushPublicKey);
    }

    [Fact]
    public async Task GetNotificationSettings_DefaultsToDaily_WhenNotSet()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Empresa test", TaxId = "B11111111", IsActive = true, Country = "ES" });
        await ctx.SaveChangesAsync();

        var handler = new GetNotificationSettingsHandler(ctx, tenant, new FakeWebPushService());
        var result = await handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        Assert.Equal("daily", result.Frequency);
    }

    [Fact]
    public async Task UpdateNotificationSettings_ValidFrequency_PersistsChange()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Empresa test", TaxId = "B11111111", IsActive = true, Country = "ES" });
        await ctx.SaveChangesAsync();

        var handler = new UpdateNotificationSettingsHandler(ctx, tenant);
        var ok = await handler.Handle(new UpdateNotificationSettingsCommand("weekly"), CancellationToken.None);

        Assert.True(ok);
        var stored = await ctx.Companies.SingleAsync(c => c.Id == companyId);
        Assert.Equal("weekly", stored.ProactiveNotificationsFrequency);
    }

    [Fact]
    public async Task UpdateNotificationSettings_InvalidFrequency_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        await using var ctx = CreateContext(tenant);
        var handler = new UpdateNotificationSettingsHandler(ctx, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new UpdateNotificationSettingsCommand("cada-hora"), CancellationToken.None));
    }

    [Fact]
    public async Task SubscribePush_NewEndpoint_CreatesSubscription()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new SubscribePushHandler(ctx, tenant, new FakeCurrentUserAccessor(userId));

        var ok = await handler.Handle(new SubscribePushCommand("https://push.example/ep1", "p256dh-key", "auth-key"), CancellationToken.None);

        Assert.True(ok);
        var stored = await ctx.PushSubscriptions.SingleAsync();
        Assert.Equal(userId, stored.UserId);
        Assert.Equal(companyId, stored.CompanyId);
        Assert.Equal("https://push.example/ep1", stored.Endpoint);
    }

    [Fact]
    public async Task SubscribePush_ExistingEndpoint_UpdatesInPlace_DoesNotDuplicate()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), CompanyId = companyId,
            Endpoint = "https://push.example/ep1", P256dh = "old", Auth = "old", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new SubscribePushHandler(ctx, tenant, new FakeCurrentUserAccessor(userId));
        await handler.Handle(new SubscribePushCommand("https://push.example/ep1", "new-p256dh", "new-auth"), CancellationToken.None);

        var stored = await ctx.PushSubscriptions.SingleAsync();
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("new-p256dh", stored.P256dh);
    }

    [Fact]
    public async Task UnsubscribePush_RemovesOwnSubscription()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(), UserId = userId, CompanyId = companyId,
            Endpoint = "https://push.example/ep1", P256dh = "x", Auth = "y", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new UnsubscribePushHandler(ctx, new FakeCurrentUserAccessor(userId));
        var ok = await handler.Handle(new UnsubscribePushCommand("https://push.example/ep1"), CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(await ctx.PushSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task UnsubscribePush_OtherUsersSubscription_ReturnsFalse_DoesNotDelete()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), CompanyId = companyId,
            Endpoint = "https://push.example/ep1", P256dh = "x", Auth = "y", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new UnsubscribePushHandler(ctx, new FakeCurrentUserAccessor(Guid.NewGuid()));
        var ok = await handler.Handle(new UnsubscribePushCommand("https://push.example/ep1"), CancellationToken.None);

        Assert.False(ok);
        Assert.Single(await ctx.PushSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task GetPushPublicKey_ReturnsServiceValue()
    {
        var handler = new GetPushPublicKeyHandler(new FakeWebPushService { PublicKey = "abc123" });
        var result = await handler.Handle(new GetPushPublicKeyQuery(), CancellationToken.None);
        Assert.Equal("abc123", result);
    }

    private static ErpDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"notifications-{Guid.NewGuid()}")
            .Options;
        return new ErpDbContext(options, tenant);
    }
}
