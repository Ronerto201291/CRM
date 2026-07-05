using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

public class Company : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Country { get; set; } = "ES";
    public Guid SubscriptionId { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Expense QR upload
    public string PublicUploadToken { get; set; } = Guid.NewGuid().ToString("N");
    public bool QrUploadEnabled { get; set; } = true;

    // Stripe
    public string? StripeCustomerId { get; set; }
    
    // Purchase matching tolerance (per-tenant). Amount in currency units. If 0, no tolerance allowed.
    public decimal MatchingToleranceAmount { get; set; } = 0m;

    /// <summary>Importe a partir del cual pedidos/gastos requieren aprobación manual (0 = desactivado).</summary>
    public decimal ApprovalThresholdAmount { get; set; } = 0m;

    /// <summary>Email de la gestoría externa para export periódico (ADR-0018 #42e).</summary>
    public string? AccountantEmail { get; set; }

    /// <summary>disabled | monthly | quarterly</summary>
    public string AccountantExportFrequency { get; set; } = "disabled";

    public DateTime? AccountantExportLastRunAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
