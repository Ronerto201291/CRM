using Erp.Modules.Treasury.Application.Interfaces;

namespace Erp.Modules.Treasury.Infrastructure.Services;

public sealed class SepaXmlGenerator : ISepaXmlGenerator
{
    public string GenerateCollectionXml(
        string companyName,
        string companyIban,
        string? companyBic,
        string clientName,
        string clientIban,
        string? clientBic,
        decimal amount,
        string endToEndId,
        string remittanceInfo) =>
        SepaService.GenerateCollectionXml(
            companyName, companyIban, companyBic,
            clientName, clientIban, clientBic,
            amount, endToEndId, remittanceInfo);

    public string GenerateDirectDebitXml(
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
        string remittanceInfo) =>
        SepaService.GenerateDirectDebitXml(
            creditorName, creditorIban, creditorBic, creditorId,
            debtorName, debtorIban, debtorBic,
            mandateId, mandateSignatureDate,
            amount, endToEndId, remittanceInfo);
}
