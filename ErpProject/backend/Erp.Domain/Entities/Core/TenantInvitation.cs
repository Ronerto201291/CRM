using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

public class TenantInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }

    public Company? Company { get; set; }
}
