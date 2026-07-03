using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

/// <summary>Membresía usuario↔empresa para multi-empresa (ADR-0018 #42a fase 1).</summary>
public class UserCompany : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? RoleId { get; set; }
    public Role? Role { get; set; }
    public bool IsDefault { get; set; }
}
