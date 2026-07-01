using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities;

public class InvoiceLine : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    
    public Guid? ProductId { get; set; }
    
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    
    // IVA español: 21%, 10%, 4%, 0% (Exento)
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    
    // Recargo equivalencia: 5.2%, 1.4%, 0.5%
    public decimal SurchargeRate { get; set; }
    public decimal SurchargeAmount { get; set; }

    /// <summary>
    /// Tipo de operación IVA (Modelo 303):
    ///   "Nacional"         → operación interior, IVA devengado normal
    ///   "IntraComunitario" → entrega intracomunitaria exenta (art. 25 LIVA), casilla 59 del 303
    ///   "Exportacion"      → exportación fuera UE exenta (art. 21 LIVA), casilla 60 del 303
    /// </summary>
    public string TipoOperacion { get; set; } = "Nacional";

    public decimal LineTotal { get; set; } // Base imponible (Quantity * UnitPrice)
}
