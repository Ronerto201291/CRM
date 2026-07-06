using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities;

/// <summary>Auditoría de envíos VERI*FACTU a la AEAT (RD 1007/2023).</summary>
public class VerifactuSubmissionLog : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    /// <summary>Alta | Anulacion</summary>
    public string SubmissionType { get; set; } = "Alta";
    public string EstadoEnvio { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool IsProduction { get; set; }
    public string? RawResponse { get; set; }
    public DateTime SubmittedAt { get; set; }
}
