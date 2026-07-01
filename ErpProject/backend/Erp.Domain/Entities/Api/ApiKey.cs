using Erp.Domain.Common;

namespace Erp.Domain.Entities.Api;

public class ApiKey : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>First 8 chars of the raw key — for display only. Never store or compare the full plaintext key.</summary>
    public string KeyPrefix { get; set; } = string.Empty;
    /// <summary>SHA-256 hex digest of the raw key. Used for DB validation in ApiKeyRateLimitMiddleware.</summary>
    public string KeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int RateLimit { get; set; }
}
