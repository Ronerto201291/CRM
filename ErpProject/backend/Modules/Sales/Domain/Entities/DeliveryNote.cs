using Erp.Domain.Common;

namespace Erp.Modules.Sales.Domain.Entities
{
    public class DeliveryNote : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid SalesOrderId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime DeliveryDate { get; set; }
        public ICollection<DeliveryNoteLine> Lines { get; set; } = new List<DeliveryNoteLine>();
    }

    public class DeliveryNoteLine : AuditableEntity
    {
        public Guid DeliveryNoteId { get; set; }
        public Guid SalesOrderLineId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal ShippedQuantity { get; set; }
    }
}
