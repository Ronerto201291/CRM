using Erp.Application.Features.Auth.Commands;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erp.Tests.Auth;

public class AuthEmailAndPermissionTests
{
    [Fact]
    public async Task ForgotPassword_SendsEmail_WhenUserExists()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"forgot-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = "reset@test.com",
            PasswordHash = "hash",
            FirstName = "Ana",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var email = new FakeEmailService();
        var cache = new FakeDistributedCache();
        var handler = new ForgotPasswordHandler(
            ctx, email, cache, Options.Create(new EmailAuthOptions { AppBaseUrl = "https://app.test" }));

        await handler.Handle(new ForgotPasswordCommand("reset@test.com"), CancellationToken.None);

        Assert.Equal("reset@test.com", email.LastPasswordResetEmail);
    }

    [Fact]
    public async Task ForgotPassword_NoOp_WhenUserMissing()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"forgot-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var email = new FakeEmailService();
        var handler = new ForgotPasswordHandler(
            ctx, email, new FakeDistributedCache(), Options.Create(new EmailAuthOptions()));

        await handler.Handle(new ForgotPasswordCommand("ghost@test.com"), CancellationToken.None);

        Assert.Null(email.LastPasswordResetEmail);
    }

    [Fact]
    public async Task ResetPassword_UpdatesHash_WhenTokenValid()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"reset-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "reset@test.com",
            PasswordHash = "old-hash",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var cache = new FakeDistributedCache();
        const string token = "valid-token";
        await cache.SetStringAsync($"pwd-reset:{token}", userId.ToString(),
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions());

        var handler = new ResetPasswordHandler(ctx, cache);
        var ok = await handler.Handle(new ResetPasswordCommand(token, "NewSecurePass1!"), CancellationToken.None);

        Assert.True(ok);
        var user = await ctx.Users.FindAsync(userId);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewSecurePass1!", user!.PasswordHash));
        Assert.Null(await cache.GetStringAsync($"pwd-reset:{token}"));
    }

    [Fact]
    public async Task ConfirmEmail_SetsEmailConfirmed()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"confirm-email-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "confirm@test.com",
            PasswordHash = "hash",
            EmailConfirmed = false,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var cache = new FakeDistributedCache();
        const string token = "email-token";
        await cache.SetStringAsync($"email-confirm:{token}", userId.ToString(),
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions());

        var handler = new ConfirmEmailHandler(ctx, cache);
        var ok = await handler.Handle(new ConfirmEmailCommand(token), CancellationToken.None);

        Assert.True(ok);
        Assert.True((await ctx.Users.FindAsync(userId))!.EmailConfirmed);
    }

    [Fact]
    public async Task GetMyPermissions_DelegatesToService()
    {
        var fake = new FakePermissionService { Permissions = ["Admin:All"] };
        var handler = new GetMyPermissionsHandler(fake);
        var perms = await handler.Handle(new GetMyPermissionsQuery(), CancellationToken.None);
        Assert.Contains("Admin:All", perms);
    }

    [Fact]
    public async Task ListPermissions_ReturnsOrdered()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"perms-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Permissions.AddRange(
            new Permission { Id = Guid.NewGuid(), Resource = "Crm", Action = "Read", Description = "Ver CRM" },
            new Permission { Id = Guid.NewGuid(), Resource = "Billing", Action = "Write", Description = "Facturar" });
        await ctx.SaveChangesAsync();

        var handler = new ListPermissionsHandler(ctx);
        var list = await handler.Handle(new ListPermissionsQuery(), CancellationToken.None);

        Assert.Equal(2, list.Count);
        Assert.Equal("Billing", list[0].Resource);
        Assert.Equal("Crm", list[1].Resource);
    }

    [Fact]
    public async Task GrantPermission_ReplacesExistingGrant()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"grant-perm-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.UserPermissions.Add(new UserPermission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PermissionId = permId,
            IsGranted = false,
        });
        await ctx.SaveChangesAsync();

        var accessor = new FakeCurrentUserAccessor { UserId = adminId };
        var handler = new GrantPermissionHandler(ctx, accessor);
        var ok = await handler.Handle(new GrantPermissionCommand(userId, permId), CancellationToken.None);

        Assert.True(ok);
        var up = await ctx.UserPermissions.SingleAsync();
        Assert.True(up.IsGranted);
        Assert.Equal(adminId.ToString(), up.GrantedBy);
    }

    [Fact]
    public async Task DenyPermission_SetsExplicitDeny()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"deny-perm-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new DenyPermissionHandler(ctx, new FakeCurrentUserAccessor { UserId = Guid.NewGuid() });
        var ok = await handler.Handle(new DenyPermissionCommand(userId, permId), CancellationToken.None);

        Assert.True(ok);
        Assert.False((await ctx.UserPermissions.SingleAsync()).IsGranted);
    }

    [Fact]
    public async Task RevokePermission_RemovesEntries()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var permId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"revoke-perm-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.UserPermissions.Add(new UserPermission
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PermissionId = permId,
            IsGranted = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new RevokePermissionHandler(ctx);
        var ok = await handler.Handle(new RevokePermissionCommand(userId, permId), CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(await ctx.UserPermissions.ToListAsync());
    }

    [Fact]
    public async Task RefreshToken_RotatesTokens()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"refresh-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var user = new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "refresh@test.com",
            PasswordHash = "hash",
            IsActive = true,
        };
        ctx.Users.Add(user);
        const string oldToken = "old-refresh-token";
        ctx.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = oldToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            User = user,
        });
        await ctx.SaveChangesAsync();

        var handler = new RefreshTokenHandler(ctx, new FakeJwtProvider());
        var result = await handler.Handle(new RefreshTokenCommand { Token = oldToken }, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.NotEqual(oldToken, result.RefreshToken);
        Assert.True((await ctx.RefreshTokens.FirstAsync(r => r.Token == oldToken)).IsRevoked);
    }

    private sealed class FakeJwtProvider : Erp.Application.Common.Interfaces.IJwtProvider
    {
        public string Generate(User user, Guid? activeCompanyId = null) => $"access-{user.Id}";
    }
}
