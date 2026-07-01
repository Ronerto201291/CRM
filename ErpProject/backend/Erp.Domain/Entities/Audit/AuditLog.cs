using Erp.Domain.Common;
using Erp.Domain.Entities.Core;

namespace Erp.Domain.Entities.Audit;

/// <summary>
/// Immutable audit log. Required by Ley 11/2021 Antifraude.
/// Hash field ensures integrity of audit trail.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    
    public string Entity { get; set; } = string.Empty;      // EntityName
    public Guid? EntityId { get; set; }                       // EntityId
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string OldValues { get; set; } = "{}"; // JSONB
    public string NewValues { get; set; } = "{}"; // JSONB
    
    /// <summary>
    /// SHA256 hash of the audit entry for integrity verification.
    /// Hash = SHA256(Entity + EntityId + Action + Timestamp + OldValues + NewValues)
    /// </summary>
    public string? Hash { get; set; }
}
