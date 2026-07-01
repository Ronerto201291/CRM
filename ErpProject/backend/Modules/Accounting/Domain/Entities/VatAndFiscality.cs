using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class VatRegime : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Type { get; set; } = "Standard"; // Standard, CashBasis, Prorrata, EquivalenceSurcharge, InversionSubject
        public bool IsActive { get; set; } = true;
        public DateTime EffectiveDate { get; set; }
    }

    public class VatTransaction : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid? InvoiceId { get; set; }
        public Guid? ExpenseId { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Direction { get; set; } = "Outbound"; // Outbound, Inbound
        public string VatRegime { get; set; } = "Standard";
        public decimal VatableBase { get; set; }
        public decimal VatRate { get; set; }
        public decimal VatAmount { get; set; }
        public decimal DeductibleVat { get; set; }
        public bool IsRecognizedByCashBasis { get; set; } = false;
        public string Status { get; set; } = "Pending"; // Pending, Recognized, Deferred
    }

    public class ProrrataCalculation : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int FiscalYear { get; set; }
        public decimal InlandRevenue { get; set; } // Revenues subject to VAT
        public decimal ExemptRevenue { get; set; } // Exempt revenues
        public decimal ProrrataPercentage { get; set; } // % deductible VAT
        public decimal AdjustmentAmount { get; set; }
        public string Type { get; set; } = "General"; // General, Special, Investment
    }

    public class ViesDeclaration : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string CountryCode { get; set; } = string.Empty;
        public string VatNumber { get; set; } = string.Empty;
        public decimal TotalAmountSupplies { get; set; }
        public decimal TotalAmountServices { get; set; }
        public DateTime DeclarationDate { get; set; }
        public bool IsValid { get; set; } = false;
        public string ValidationStatus { get; set; } = "Pending"; // Pending, Valid, Invalid
    }

    public class RecargoDEquivalencia : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid? InvoiceId { get; set; }
        public string SupplierVatNumber { get; set; } = string.Empty;
        public bool SupplierIsRE { get; set; } = false;
        public decimal Base { get; set; }
        public decimal RechargeRate { get; set; }
        public decimal RechargeAmount { get; set; }
        public bool IsEndToEnd { get; set; } = false;
        public string Modelo303Status { get; set; } = "Pending"; // Pending, Reported, Confirmed
    }

    public class InversionDeSujetoActivo : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid? InvoiceId { get; set; }
        public string SupplierCountryCode { get; set; } = string.Empty;
        public decimal VatableBase { get; set; }
        public decimal VatRate { get; set; }
        public decimal VatAmount { get; set; }
        public bool IsReverseCharge { get; set; } = true;
        public DateTime DeclarationDate { get; set; }
    }
}
