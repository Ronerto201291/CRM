using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>Registro de cobro TPV contra factura (método card).</summary>
public class PosPayment : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid PosTerminalId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public string? ExternalReference { get; set; }
    public DateTime PaidAt { get; set; }
}
