using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities
{
    public class Currency : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Code { get; set; } = string.Empty; // EUR, USD, GBP, etc.
        public string Name { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m; // vs EUR
        public DateTime RateDate { get; set; }
        public string Source { get; set; } = "ECB"; // European Central Bank
        public bool IsActive { get; set; } = true;
    }

    public class CurrencyExchange : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string FromCurrency { get; set; } = string.Empty;
        public string ToCurrency { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal ExchangedAmount { get; set; }
        public decimal ExchangeRate { get; set; }
        public DateTime ExchangeDate { get; set; }
        public string Type { get; set; } = "Manual"; // Manual, Automatic, Market
        public Guid? LinkedTransactionId { get; set; } // Invoice, Receipt, etc.
    }

    public class ExchangeRateHistory : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal Rate { get; set; }
        public DateTime RateDate { get; set; }
        public string Source { get; set; } = "ECB";
    }
}
