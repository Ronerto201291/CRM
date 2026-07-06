namespace Erp.Modules.Treasury.Application.Interfaces;

public interface ISepaXmlGenerator
{
    /// <summary>pain.001 — cobro: cliente (deudor) paga a empresa (acreedor).</summary>
    string GenerateCollectionXml(
        string companyName,
        string companyIban,
        string? companyBic,
        string clientName,
        string clientIban,
        string? clientBic,
        decimal amount,
        string endToEndId,
        string remittanceInfo);

    /// <summary>pain.008 — adeudo directo SEPA (cobro domiciliado).</summary>
    string GenerateDirectDebitXml(
        string creditorName,
        string creditorIban,
        string? creditorBic,
        string creditorId,
        string debtorName,
        string debtorIban,
        string? debtorBic,
        string mandateId,
        DateTime mandateSignatureDate,
        decimal amount,
        string endToEndId,
        string remittanceInfo);
}
