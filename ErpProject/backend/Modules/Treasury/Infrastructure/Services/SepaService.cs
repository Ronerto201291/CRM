using System.Globalization;
using System.Xml.Linq;
using Erp.Application.Common.Validation;

namespace Erp.Modules.Treasury.Infrastructure.Services;

/// <summary>
/// Genera XML SEPA pain.001.001.03 (ISO 20022).
/// </summary>
public static class SepaService
{
    /// <summary>
    /// Cobro: el cliente (deudor) transfiere a la empresa (acreedor).
    /// </summary>
    public static string GenerateCollectionXml(
        string companyName,
        string companyIban,
        string? companyBic,
        string clientName,
        string clientIban,
        string? clientBic,
        decimal amount,
        string endToEndId,
        string remittanceInfo)
    {
        if (!IbanValidator.IsValid(companyIban))
            throw new ArgumentException("IBAN de la empresa no válido.");
        if (!IbanValidator.IsValid(clientIban))
            throw new ArgumentException("IBAN del cliente no válido.");

        return BuildPain001(
            initiatingPartyName: companyName,
            debtorName: clientName,
            debtorIban: clientIban,
            debtorBic: clientBic,
            creditorName: companyName,
            creditorIban: companyIban,
            creditorBic: companyBic,
            amount,
            endToEndId,
            remittanceInfo);
    }

    /// <summary>Pago a proveedor: la empresa (deudor) paga al beneficiario (acreedor).</summary>
    public static string GeneratePaymentXml(
        string companyName,
        string companyIban,
        string? companyBic,
        string beneficiaryName,
        string beneficiaryIban,
        string? beneficiaryBic,
        decimal amount,
        string endToEndId,
        string remittanceInfo)
    {
        if (!IbanValidator.IsValid(companyIban))
            throw new ArgumentException("IBAN de la empresa no válido.");
        if (!IbanValidator.IsValid(beneficiaryIban))
            throw new ArgumentException("IBAN del beneficiario no válido.");

        return BuildPain001(
            initiatingPartyName: companyName,
            debtorName: companyName,
            debtorIban: companyIban,
            debtorBic: companyBic,
            creditorName: beneficiaryName,
            creditorIban: beneficiaryIban,
            creditorBic: beneficiaryBic,
            amount,
            endToEndId,
            remittanceInfo);
    }

    [Obsolete("Usar GenerateCollectionXml o GeneratePaymentXml.")]
    public static string GenerateCreditTransferXml(
        string originatorName,
        string originatorIban,
        string? originatorBic,
        string debtorName,
        string debtorIban,
        string? debtorBic,
        decimal amount,
        string endToEndId,
        string remittanceInfo) =>
        GeneratePaymentXml(
            originatorName, originatorIban, originatorBic,
            debtorName, debtorIban, debtorBic,
            amount, endToEndId, remittanceInfo);

    private static string BuildPain001(
        string initiatingPartyName,
        string debtorName,
        string debtorIban,
        string? debtorBic,
        string creditorName,
        string creditorIban,
        string? creditorBic,
        decimal amount,
        string endToEndId,
        string remittanceInfo)
    {
        var now = DateTime.UtcNow;
        var msgId = $"ERP{now:yyyyMMddHHmmss}";
        var fmt = CultureInfo.InvariantCulture;
        var dIban = debtorIban.Replace(" ", "").ToUpperInvariant();
        var cIban = creditorIban.Replace(" ", "").ToUpperInvariant();

        var ns = XNamespace.Get("urn:iso:std:iso:20022:tech:xsd:pain.001.001.03");
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "CstmrCdtTrfInitn",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", msgId),
                        new XElement(ns + "CreDtTm", now.ToString("yyyy-MM-ddTHH:mm:ss", fmt)),
                        new XElement(ns + "NbOfTxs", "1"),
                        new XElement(ns + "CtrlSum", amount.ToString("F2", fmt)),
                        new XElement(ns + "InitgPty",
                            new XElement(ns + "Nm", initiatingPartyName))),
                    new XElement(ns + "PmtInf",
                        new XElement(ns + "PmtInfId", $"PMT{msgId}"),
                        new XElement(ns + "PmtMtd", "TRF"),
                        new XElement(ns + "BtchBookg", "false"),
                        new XElement(ns + "NbOfTxs", "1"),
                        new XElement(ns + "CtrlSum", amount.ToString("F2", fmt)),
                        new XElement(ns + "PmtTpInf",
                            new XElement(ns + "SvcLvl",
                                new XElement(ns + "Cd", "SEPA"))),
                        new XElement(ns + "ReqdExctnDt", now.ToString("yyyy-MM-dd", fmt)),
                        new XElement(ns + "Dbtr",
                            new XElement(ns + "Nm", debtorName)),
                        new XElement(ns + "DbtrAcct",
                            new XElement(ns + "Id",
                                new XElement(ns + "IBAN", dIban))),
                        debtorBic is { Length: > 0 }
                            ? new XElement(ns + "DbtrAgt",
                                new XElement(ns + "FinInstnId",
                                    new XElement(ns + "BIC", debtorBic)))
                            : null,
                        new XElement(ns + "ChrgBr", "SLEV"),
                        new XElement(ns + "CdtTrfTxInf",
                            new XElement(ns + "PmtId",
                                new XElement(ns + "EndToEndId", endToEndId)),
                            new XElement(ns + "Amt",
                                new XElement(ns + "InstdAmt",
                                    new XAttribute("Ccy", "EUR"),
                                    amount.ToString("F2", fmt))),
                            creditorBic is { Length: > 0 }
                                ? new XElement(ns + "CdtrAgt",
                                    new XElement(ns + "FinInstnId",
                                        new XElement(ns + "BIC", creditorBic)))
                                : null,
                            new XElement(ns + "Cdtr",
                                new XElement(ns + "Nm", creditorName)),
                            new XElement(ns + "CdtrAcct",
                                new XElement(ns + "Id",
                                    new XElement(ns + "IBAN", cIban))),
                            new XElement(ns + "RmtInf",
                                new XElement(ns + "Ustrd", remittanceInfo)))))));

        return doc.Declaration + "\n" + doc.ToString();
    }

    /// <summary>Adeudo directo SEPA pain.008.001.02 — empresa cobra al cliente.</summary>
    public static string GenerateDirectDebitXml(
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
        string remittanceInfo)
    {
        if (!IbanValidator.IsValid(creditorIban))
            throw new ArgumentException("IBAN del acreedor no válido.");
        if (!IbanValidator.IsValid(debtorIban))
            throw new ArgumentException("IBAN del deudor no válido.");

        var now = DateTime.UtcNow;
        var msgId = $"SDD{now:yyyyMMddHHmmss}";
        var fmt = CultureInfo.InvariantCulture;
        var cIban = creditorIban.Replace(" ", "").ToUpperInvariant();
        var dIban = debtorIban.Replace(" ", "").ToUpperInvariant();

        var ns = XNamespace.Get("urn:iso:std:iso:20022:tech:xsd:pain.008.001.02");
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "Document",
                new XElement(ns + "CstmrDrctDbtInitn",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", msgId),
                        new XElement(ns + "CreDtTm", now.ToString("yyyy-MM-ddTHH:mm:ss", fmt)),
                        new XElement(ns + "NbOfTxs", "1"),
                        new XElement(ns + "CtrlSum", amount.ToString("F2", fmt)),
                        new XElement(ns + "InitgPty",
                            new XElement(ns + "Nm", creditorName))),
                    new XElement(ns + "PmtInf",
                        new XElement(ns + "PmtInfId", $"SDD{msgId}"),
                        new XElement(ns + "PmtMtd", "DD"),
                        new XElement(ns + "NbOfTxs", "1"),
                        new XElement(ns + "CtrlSum", amount.ToString("F2", fmt)),
                        new XElement(ns + "PmtTpInf",
                            new XElement(ns + "SvcLvl",
                                new XElement(ns + "Cd", "SEPA")),
                            new XElement(ns + "LclInstrm",
                                new XElement(ns + "Cd", "CORE")),
                            new XElement(ns + "SeqTp", "RCUR")),
                        new XElement(ns + "ReqdColltnDt", now.ToString("yyyy-MM-dd", fmt)),
                        new XElement(ns + "Cdtr",
                            new XElement(ns + "Nm", creditorName)),
                        new XElement(ns + "CdtrAcct",
                            new XElement(ns + "Id",
                                new XElement(ns + "IBAN", cIban))),
                        creditorBic is { Length: > 0 }
                            ? new XElement(ns + "CdtrAgt",
                                new XElement(ns + "FinInstnId",
                                    new XElement(ns + "BIC", creditorBic)))
                            : null,
                        new XElement(ns + "CdtrSchmeId",
                            new XElement(ns + "Id",
                                new XElement(ns + "PrvtId",
                                    new XElement(ns + "Othr",
                                        new XElement(ns + "Id", creditorId),
                                        new XElement(ns + "SchmeNm",
                                            new XElement(ns + "Prtry", "SEPA")))))),
                        new XElement(ns + "DrctDbtTxInf",
                            new XElement(ns + "PmtId",
                                new XElement(ns + "EndToEndId", endToEndId)),
                            new XElement(ns + "InstdAmt",
                                new XAttribute("Ccy", "EUR"),
                                amount.ToString("F2", fmt)),
                            new XElement(ns + "DrctDbtTx",
                                new XElement(ns + "MndtRltdInf",
                                    new XElement(ns + "MndtId", mandateId),
                                    new XElement(ns + "DtOfSgntr",
                                        mandateSignatureDate.ToString("yyyy-MM-dd", fmt)))),
                            debtorBic is { Length: > 0 }
                                ? new XElement(ns + "DbtrAgt",
                                    new XElement(ns + "FinInstnId",
                                        new XElement(ns + "BIC", debtorBic)))
                                : null,
                            new XElement(ns + "Dbtr",
                                new XElement(ns + "Nm", debtorName)),
                            new XElement(ns + "DbtrAcct",
                                new XElement(ns + "Id",
                                    new XElement(ns + "IBAN", dIban))),
                            new XElement(ns + "RmtInf",
                                new XElement(ns + "Ustrd", remittanceInfo)))))));

        return doc.Declaration + "\n" + doc.ToString();
    }
}
