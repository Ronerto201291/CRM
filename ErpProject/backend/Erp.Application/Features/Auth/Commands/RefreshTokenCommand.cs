using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Auth.Commands;

/// <summary>
/// JWT Refresh Token rotation.
/// </summary>
public class RefreshTokenCommand : IRequest<RefreshTokenResponse>
{
    public string Token { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IJwtProvider _jwt;
    public RefreshTokenHandler(IApplicationDbContext ctx, IJwtProvider jwt) { _ctx = ctx; _jwt = jwt; }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand req, CancellationToken ct)
    {
        var existing = await _ctx.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == req.Token && !r.IsRevoked && r.ExpiresAt > DateTime.UtcNow, ct)
            ?? throw new UnauthorizedAccessException("Refresh token inválido o expirado.");

        // Revoke old token
        existing.IsRevoked = true;

        // Generate new tokens
        var newAccessToken = _jwt.Generate(existing.User!);
        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            Token = Guid.NewGuid().ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        existing.ReplacedByToken = newRefreshToken.Token;
        _ctx.RefreshTokens.Add(newRefreshToken);
        await _ctx.SaveChangesAsync(ct);

        return new RefreshTokenResponse { AccessToken = newAccessToken, RefreshToken = newRefreshToken.Token };
    }
}
