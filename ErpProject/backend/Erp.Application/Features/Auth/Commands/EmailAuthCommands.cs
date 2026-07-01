using Erp.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Erp.Application.Features.Auth.Commands;

// ─── Options ────────────────────────────────────────────────────────────────

public class EmailAuthOptions
{
    public const string SectionName = "Email";
    public string AppBaseUrl { get; set; } = "https://app.example.com";
}

// ═══════════════════════════════════════════════════════════════════════════
//  FORGOT PASSWORD
// ═══════════════════════════════════════════════════════════════════════════

public record ForgotPasswordCommand(string Email) : IRequest<Unit>;

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IEmailService _email;
    private readonly IDistributedCache _cache;
    private readonly IOptions<EmailAuthOptions> _opt;

    public ForgotPasswordHandler(
        IApplicationDbContext ctx,
        IEmailService email,
        IDistributedCache cache,
        IOptions<EmailAuthOptions> opt)
    {
        _ctx = ctx;
        _email = email;
        _cache = cache;
        _opt = opt;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand req, CancellationToken ct)
    {
        // Always return Ok to prevent email enumeration attacks.
        var user = await _ctx.Users
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower() && u.IsActive, ct);

        if (user is null) return Unit.Value;

        // Generate a cryptographically secure token, store in Redis (1 hour TTL).
        var token = GenerateToken();
        var cacheKey = $"pwd-reset:{token}";
        await _cache.SetStringAsync(cacheKey, user.Id.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
            ct);

        var resetUrl = $"{_opt.Value.AppBaseUrl}/auth/reset-password?token={Uri.EscapeDataString(token)}";
        await _email.SendPasswordResetAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), resetUrl, ct);

        return Unit.Value;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  RESET PASSWORD
// ═══════════════════════════════════════════════════════════════════════════

public record ResetPasswordCommand(string Token, string NewPassword) : IRequest<bool>;

public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IDistributedCache _cache;

    public ResetPasswordHandler(IApplicationDbContext ctx, IDistributedCache cache)
    {
        _ctx = ctx;
        _cache = cache;
    }

    public async Task<bool> Handle(ResetPasswordCommand req, CancellationToken ct)
    {
        var cacheKey = $"pwd-reset:{req.Token}";
        var userIdStr = await _cache.GetStringAsync(cacheKey, ct);
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            return false; // token expired or invalid

        var user = await _ctx.Users.FindAsync([userId], ct);
        if (user is null || !user.IsActive) return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);

        await _ctx.SaveChangesAsync(ct);

        // Invalidate token immediately (single-use)
        await _cache.RemoveAsync(cacheKey, ct);
        return true;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  SEND EMAIL CONFIRMATION
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Generates and sends an email confirmation token for the given user.
/// Called after registration, or when the user requests a new confirmation link.
/// </summary>
public record SendEmailConfirmationCommand(Guid UserId) : IRequest<Unit>;

public class SendEmailConfirmationHandler : IRequestHandler<SendEmailConfirmationCommand, Unit>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IEmailService _email;
    private readonly IDistributedCache _cache;
    private readonly IOptions<EmailAuthOptions> _opt;

    public SendEmailConfirmationHandler(
        IApplicationDbContext ctx,
        IEmailService email,
        IDistributedCache cache,
        IOptions<EmailAuthOptions> opt)
    {
        _ctx = ctx;
        _email = email;
        _cache = cache;
        _opt = opt;
    }

    public async Task<Unit> Handle(SendEmailConfirmationCommand req, CancellationToken ct)
    {
        var user = await _ctx.Users.FindAsync([req.UserId], ct)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        if (user.EmailConfirmed) return Unit.Value; // already confirmed

        var token = GenerateToken();
        var cacheKey = $"email-confirm:{token}";
        await _cache.SetStringAsync(cacheKey, user.Id.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
            ct);

        var confirmUrl = $"{_opt.Value.AppBaseUrl}/auth/confirm-email?token={Uri.EscapeDataString(token)}";
        await _email.SendEmailConfirmationAsync(user.Email, $"{user.FirstName} {user.LastName}".Trim(), confirmUrl, ct);

        return Unit.Value;
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  CONFIRM EMAIL
// ═══════════════════════════════════════════════════════════════════════════

public record ConfirmEmailCommand(string Token) : IRequest<bool>;

public class ConfirmEmailHandler : IRequestHandler<ConfirmEmailCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IDistributedCache _cache;

    public ConfirmEmailHandler(IApplicationDbContext ctx, IDistributedCache cache)
    {
        _ctx = ctx;
        _cache = cache;
    }

    public async Task<bool> Handle(ConfirmEmailCommand req, CancellationToken ct)
    {
        var cacheKey = $"email-confirm:{req.Token}";
        var userIdStr = await _cache.GetStringAsync(cacheKey, ct);
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            return false;

        var user = await _ctx.Users.FindAsync([userId], ct);
        if (user is null) return false;

        user.EmailConfirmed = true;
        await _ctx.SaveChangesAsync(ct);

        await _cache.RemoveAsync(cacheKey, ct);
        return true;
    }
}
