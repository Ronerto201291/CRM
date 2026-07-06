namespace Erp.Application.DTOs;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public List<CompanyMembershipDto> Companies { get; set; } = [];
    /// <summary>True when the user has 2FA enabled. Token will be empty — client must call /api/auth/2fa/verify.</summary>
    public bool RequiresTwoFactor { get; set; }
}
