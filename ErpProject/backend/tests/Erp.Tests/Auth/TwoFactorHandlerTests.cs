using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Auth.Handlers;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace Erp.Tests.Auth;

public class TwoFactorHandlerTests
{
    private static async Task<(ErpDbContext Ctx, User User)> SeedUserAsync(bool twoFactorEnabled = false)
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa 2FA");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"2fa-{Guid.NewGuid()}")
            .Options;

        var ctx = new ErpDbContext(options, tenant);
        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = "user@2fa.test",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            TwoFactorEnabled = twoFactorEnabled,
            TotpSecret = twoFactorEnabled ? "SECRET" : null,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return (ctx, user);
    }

    [Fact]
    public async Task Setup2Fa_StoresSecretAndReturnsBackupCodes()
    {
        var (ctx, user) = await SeedUserAsync();
        await using (ctx)
        {
            var totp = new FakeTotpService();
            var handler = new Setup2FaHandler(ctx, totp);

            var result = await handler.Handle(new Setup2FaCommand(user.Id), CancellationToken.None);

            Assert.Equal(totp.FixedSecret, result.Secret);
            Assert.Equal(2, result.BackupCodes.Count);
            var stored = await ctx.Users.FindAsync(user.Id);
            Assert.Equal(totp.FixedSecret, stored!.TotpSecret);
            Assert.False(stored.TwoFactorEnabled);
        }
    }

    [Fact]
    public async Task Setup2Fa_UserNotFound_Throws()
    {
        var (ctx, _) = await SeedUserAsync();
        await using (ctx)
        {
            var handler = new Setup2FaHandler(ctx, new FakeTotpService());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new Setup2FaCommand(Guid.NewGuid()), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Confirm2Fa_EnablesTwoFactor_WhenCodeValid()
    {
        var (ctx, user) = await SeedUserAsync();
        await using (ctx)
        {
            user.TotpSecret = "PENDING";
            await ctx.SaveChangesAsync();

            var totp = new FakeTotpService { ValidCode = "654321" };
            var handler = new Confirm2FaHandler(ctx, totp);

            var ok = await handler.Handle(new Confirm2FaCommand(user.Id, "654321"), CancellationToken.None);

            Assert.True(ok);
            var stored = await ctx.Users.FindAsync(user.Id);
            Assert.True(stored!.TwoFactorEnabled);
        }
    }

    [Fact]
    public async Task Confirm2Fa_ReturnsFalse_WhenCodeInvalid()
    {
        var (ctx, user) = await SeedUserAsync();
        await using (ctx)
        {
            user.TotpSecret = "PENDING";
            await ctx.SaveChangesAsync();

            var totp = new FakeTotpService { VerifyResult = false };
            var handler = new Confirm2FaHandler(ctx, totp);

            var ok = await handler.Handle(new Confirm2FaCommand(user.Id, "000000"), CancellationToken.None);

            Assert.False(ok);
            Assert.False((await ctx.Users.FindAsync(user.Id))!.TwoFactorEnabled);
        }
    }

    [Fact]
    public async Task Confirm2Fa_Throws_WhenSetupNotInitiated()
    {
        var (ctx, user) = await SeedUserAsync();
        await using (ctx)
        {
            var handler = new Confirm2FaHandler(ctx, new FakeTotpService());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new Confirm2FaCommand(user.Id, "123456"), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Disable2Fa_ClearsSecrets_WhenTotpValid()
    {
        var (ctx, user) = await SeedUserAsync(twoFactorEnabled: true);
        await using (ctx)
        {
            user.TotpBackupCodes = JsonSerializer.Serialize(new[] { "backup-1" });
            await ctx.SaveChangesAsync();

            var handler = new Disable2FaHandler(ctx, new FakeTotpService { ValidCode = "123456" });
            var ok = await handler.Handle(new Disable2FaCommand(user.Id, "123456"), CancellationToken.None);

            Assert.True(ok);
            var stored = await ctx.Users.FindAsync(user.Id);
            Assert.False(stored!.TwoFactorEnabled);
            Assert.Null(stored.TotpSecret);
            Assert.Null(stored.TotpBackupCodes);
        }
    }

    [Fact]
    public async Task Disable2Fa_AcceptsBackupCode()
    {
        var (ctx, user) = await SeedUserAsync(twoFactorEnabled: true);
        await using (ctx)
        {
            user.TotpBackupCodes = JsonSerializer.Serialize(new List<string> { "backup-xyz" });
            await ctx.SaveChangesAsync();

            var totp = new FakeTotpService { VerifyResult = false };
            var handler = new Disable2FaHandler(ctx, totp);
            var ok = await handler.Handle(new Disable2FaCommand(user.Id, "backup-xyz"), CancellationToken.None);

            Assert.True(ok);
            Assert.False((await ctx.Users.FindAsync(user.Id))!.TwoFactorEnabled);
        }
    }

    [Fact]
    public async Task VerifyTotp_ReturnsJwt_WhenCodeValid()
    {
        var (ctx, user) = await SeedUserAsync(twoFactorEnabled: true);
        await using (ctx)
        {
            var handler = new VerifyTotpHandler(ctx, new FakeTotpService(), new FakeJwtProvider());
            var result = await handler.Handle(new VerifyTotpCommand(user.Id, "123456"), CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(user.Email, result!.Email);
            Assert.False(string.IsNullOrEmpty(result.Token));
        }
    }

    [Fact]
    public async Task VerifyTotp_ReturnsNull_WhenUserNot2FaEnabled()
    {
        var (ctx, user) = await SeedUserAsync();
        await using (ctx)
        {
            var handler = new VerifyTotpHandler(ctx, new FakeTotpService(), new FakeJwtProvider());
            var result = await handler.Handle(new VerifyTotpCommand(user.Id, "123456"), CancellationToken.None);
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task VerifyTotp_ConsumesBackupCode_OnSuccess()
    {
        var (ctx, user) = await SeedUserAsync(twoFactorEnabled: true);
        await using (ctx)
        {
            user.TotpBackupCodes = JsonSerializer.Serialize(new List<string> { "one-time-code" });
            await ctx.SaveChangesAsync();

            var totp = new FakeTotpService { VerifyResult = false };
            var handler = new VerifyTotpHandler(ctx, totp, new FakeJwtProvider());
            var result = await handler.Handle(new VerifyTotpCommand(user.Id, "one-time-code"), CancellationToken.None);

            Assert.NotNull(result);
            var stored = await ctx.Users.FindAsync(user.Id);
            var remaining = JsonSerializer.Deserialize<List<string>>(stored!.TotpBackupCodes!) ?? [];
            Assert.Empty(remaining);
        }
    }

    private sealed class FakeJwtProvider : IJwtProvider
    {
        public string Generate(User user, Guid? activeCompanyId = null) => $"jwt-for-{user.Id}";
    }
}
