using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities
{
    public class Guarantee : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Type { get; set; } = "Warranty"; // Warranty, Collateral, Guarantee, Pledge
        public string ReferenceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; } = "EUR";
        public string RelatedEntity { get; set; } = string.Empty; // Supplier, Client, Bank
        public string Description { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = "Active"; // Active, Expired, Released, Claimed
        public decimal ClaimedAmount { get; set; } = 0;
        public DateTime? ClaimDate { get; set; }
    }

    public class Collateral : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Type { get; set; } = "FixedAsset"; // FixedAsset, Inventory, Securities, Cash
        public string Description { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public DateTime ValuationDate { get; set; }
        public string LinkedAccount { get; set; } = string.Empty; // Loan, Facility ID
        public string Status { get; set; } = "Pledged";
        public decimal LTVRatio { get; set; } // Loan-to-Value
    }

    public class BankGuarantee : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string GuaranteeNumber { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Bid"; // Bid, Performance, Payment, Customs
        public DateTime IssuedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public Guid? BeneficiaryId { get; set; }
        public string BeneficiaryName { get; set; } = string.Empty;
        public decimal Fee { get; set; } // % anual
        public string Status { get; set; } = "Active";
    }
}
