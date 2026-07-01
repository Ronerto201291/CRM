using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Erp.Application.Features.Auth.Handlers;

public class Setup2FaHandler : IRequestHandler<Setup2FaCommand, Setup2FaResult>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITotpService _totp;

    public Setup2FaHandler(IApplicationDbContext ctx, ITotpService totp) { _ctx = ctx; _totp = totp; }

    public async Task<Setup2FaResult> Handle(Setup2FaCommand request, CancellationToken ct)
    {
        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new InvalidOperationException("User not found");

        var (secret, qrUri) = _totp.GenerateSetup(user.Email);
        var backupCodes = _totp.GenerateBackupCodes();

        // Store temporarily (confirmed on Confirm2Fa)
        user.TotpSecret = secret;
        user.TotpBackupCodes = JsonSerializer.Serialize(backupCodes);
        // Don't enable yet — requires verification first
        await _ctx.SaveChangesAsync(ct);

        return new Setup2FaResult(secret, qrUri, backupCodes);
    }
}

public class Confirm2FaHandler : IRequestHandler<Confirm2FaCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITotpService _totp;

    public Confirm2FaHandler(IApplicationDbContext ctx, ITotpService totp) { _ctx = ctx; _totp = totp; }

    public async Task<bool> Handle(Confirm2FaCommand request, CancellationToken ct)
    {
        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new InvalidOperationException("User not found");

        if (string.IsNullOrEmpty(user.TotpSecret))
            throw new InvalidOperationException("2FA setup not initiated. Call /api/auth/2fa/setup first.");

        if (!_totp.Verify(user.TotpSecret, request.TotpCode))
            return false;

        user.TwoFactorEnabled = true;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class Disable2FaHandler : IRequestHandler<Disable2FaCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITotpService _totp;

    public Disable2FaHandler(IApplicationDbContext ctx, ITotpService totp) { _ctx = ctx; _totp = totp; }

    public async Task<bool> Handle(Disable2FaCommand request, CancellationToken ct)
    {
        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new InvalidOperationException("User not found");

        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
            throw new InvalidOperationException("2FA is not enabled.");

        // Accept TOTP OR backup code
        var validTotp = _totp.Verify(user.TotpSecret, request.Code);
        if (!validTotp)
        {
            var updatedBackupCodes = _totp.ValidateAndConsumeBackupCode(user.TotpBackupCodes, request.Code);
            if (updatedBackupCodes == null) return false;
            user.TotpBackupCodes = updatedBackupCodes;
        }

        user.TwoFactorEnabled = false;
        user.TotpSecret = null;
        user.TotpBackupCodes = null;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class VerifyTotpHandler : IRequestHandler<VerifyTotpCommand, LoginResponseDto?>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITotpService _totp;
    private readonly IJwtProvider _jwt;

    public VerifyTotpHandler(IApplicationDbContext ctx, ITotpService totp, IJwtProvider jwt)
    {
        _ctx = ctx;
        _totp = totp;
        _jwt = jwt;
    }

    public async Task<LoginResponseDto?> Handle(VerifyTotpCommand request, CancellationToken ct)
    {
        // IgnoreQueryFilters: login happens before tenant is resolved (same as LoginCommandHandler)
        var user = await _ctx.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, ct);

        if (user == null || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecret))
            return null;

        bool verified = _totp.Verify(user.TotpSecret, request.Code);

        if (!verified)
        {
            var updatedCodes = _totp.ValidateAndConsumeBackupCode(user.TotpBackupCodes, request.Code);
            if (updatedCodes == null) return null;
            user.TotpBackupCodes = updatedCodes;
            await _ctx.SaveChangesAsync(ct);
        }

        return new LoginResponseDto
        {
            Token = _jwt.Generate(user),
            UserId = user.Id,
            Email = user.Email,
            CompanyId = user.CompanyId.ToString()
        };
    }
}
