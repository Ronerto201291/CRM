using Erp.Domain.Common;

namespace Erp.Modules.Inventory.Domain.Entities
{
    public class Lot : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid ProductId { get; set; }
        public string LotNumber { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SerialNumber : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? LotId { get; set; }
        public string Serial { get; set; } = string.Empty;
        public string Status { get; set; } = "Available"; // Available, Sold, Returned
        public DateTime? SoldDate { get; set; }
    }
}
