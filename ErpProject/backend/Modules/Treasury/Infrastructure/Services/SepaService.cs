using System.Xml.Linq;
using Erp.Modules.Treasury.Domain.Entities;

namespace Erp.Modules.Treasury.Infrastructure.Services;

/// <summary>
/// Genera XML SEPA Credit Transfer (pain.001.001.03) para efectos comerciales.
/// Descarga: el usuario ejecuta la transferencia en su banco.
/// </summary>
public static class SepaService
{
    /// <summary>
    /// Genera un XML SEPA Credit Transfer (pain.001.001.03) para cobrar un efecto.
    /// Compatible con banca electrónica española (Santander, BBVA, CaixaBank, etc.).
    /// </summary>
    public static string GenerateCreditTransferXml(
        string originatorName,
        string originatorIban,
        string originatorBic,
        string debtorName,
        string debtorIban,
        string debtorBic,
        decimal amount,
        string endToEndId,
        string remittanceInfo)
    {
        var now = DateTime.UtcNow;
        var msgId = $"ERP{now:yyyyMMddHHmmss}";

        var ns = XNamespace.Get("urn:iso:std:iso:20022:tech:xsd:pain.001.001.03");
        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(ns + "CustomerCreditTransferInitiation",
                new XElement(ns + "GroupHeader",
                    new XElement(ns + "MsgId", msgId),
                    new XElement(ns + "CreDtTm", now.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement(ns + "NbOfTxs", "1"),
                    new XElement(ns + "CtrlSum", amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(ns + "InitgPty",
                        new XElement(ns + "Nm", originatorName))),
                new XElement(ns + "PaymentInformation",
                    new XElement(ns + "PmtInfId", $"PMT{msgId}"),
                    new XElement(ns + "PmtMtd", "TRF"),
                    new XElement(ns + "BtchBookg", "false"),
                    new XElement(ns + "NbOfTxs", "1"),
                    new XElement(ns + "CtrlSum", amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(ns + "ReqdExctnDt", now.ToString("yyyy-MM-dd")),
                    new XElement(ns + "Dbtr",
                        new XElement(ns + "Nm", originatorName)),
                    new XElement(ns + "DbtrAcct",
                        new XElement(ns + "Id",
                            new XElement(ns + "IBAN", originatorIban.Replace(" ", "").ToUpperInvariant()))),
                    new XElement(ns + "DbtrAgt",
                        new XElement(ns + "FinInstnId",
                            new XElement(ns + "BIC", originatorBic ?? "XXXXESMM")),
                    new XElement(ns + "ChrgBr", "SLEV"),
                    new XElement(ns + "CdtTrfTxInf",
                        new XElement(ns + "PmtId",
                            new XElement(ns + "EndToEndId", endToEndId)),
                        new XElement(ns + "Amt",
                            new XElement(ns + "InstdAmt",
                                new XAttribute("Ccy", "EUR"),
                                amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))),
                        new XElement(ns + "CdtrAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BIC", debtorBic ?? "XXXXESMM"))),
                        new XElement(ns + "Cdtr",
                            new XElement(ns + "Nm", debtorName)),
                        new XElement(ns + "CdtrAcct",
                            new XElement(ns + "Id",
                                new XElement(ns + "IBAN", debtorIban.Replace(" ", "").ToUpperInvariant()))),
                        new XElement(ns + "RmtInf",
                            new XElement(ns + "Ustrd", remittanceInfo)))))));

        return xml.Declaration + "\n" + xml.ToString();
    }
}
