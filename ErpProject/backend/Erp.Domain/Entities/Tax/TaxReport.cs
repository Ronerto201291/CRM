using Erp.Domain.Common;

namespace Erp.Domain.Entities.Tax;

public class TaxReport : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal NetTax { get; set; }
}
