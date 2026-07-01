using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Erp.Infrastructure.Security;

public class JwtProvider : IJwtProvider
{
    private readonly IConfiguration _configuration;

    public JwtProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string Generate(User user)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("CompanyId", user.CompanyId.ToString()),
            new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        };

        var encKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(encKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            null,
            DateTime.UtcNow.AddHours(8),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
