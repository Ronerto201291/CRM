using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

public class User : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? TotpSecret { get; set; }        // Base32 encoded TOTP secret
    public string? TotpBackupCodes { get; set; }    // JSON array of one-time backup codes
    public Guid? RoleId { get; set; }
    public Role? Role { get; set; }
}
