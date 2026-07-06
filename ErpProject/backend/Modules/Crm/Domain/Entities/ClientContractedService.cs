using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

/// <summary>
/// Contrato de un servicio del catálogo de la empresa con un cliente concreto
/// (p. ej. mantenimiento anual de un taller). Snapshotea Name/Price/TaxRate/Periodicity
/// del catálogo en el momento de la contratación (mismo patrón que el snapshot fiscal
/// de Invoice): un cambio posterior en el catálogo no altera contratos ya firmados.
/// ContractedServiceBillingJob revisa NextBillingDate para disparar la facturación
/// recurrente (ver ClientServiceDueForBillingEvent).
/// </summary>
public class ClientContractedService : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid ServiceCatalogItemId { get; set; }
    public ServiceCatalogItem? ServiceCatalogItem { get; set; }

    // Snapshot al contratar
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal TaxRate { get; set; }
    /// <summary>"Monthly" | "Quarterly" | "Yearly"</summary>
    public string Periodicity { get; set; } = "Monthly";

    public DateTime StartDate { get; set; }
    public DateTime NextBillingDate { get; set; }
    public DateTime? EndDate { get; set; }
    /// <summary>"Active" | "Cancelled"</summary>
    public string Status { get; set; } = "Active";
    public Guid? LastInvoiceId { get; set; }
}
