using System.Xml.Linq;

namespace Erp.Application.Common.Validation;

/// <summary>Validación estructural offline de XML SEPA ISO 20022 (ADR-0018 #0d).</summary>
public static class SepaXmlStructureValidator
{
    private const string Pain001Ns = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.03";
    private const string Pain008Ns = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.02";

    public static void ValidatePain001(string xml)
    {
        Validate(xml, Pain001Ns, "CstmrCdtTrfInitn", "pain.001");
    }

    public static void ValidatePain008(string xml)
    {
        Validate(xml, Pain008Ns, "CstmrDrctDbtInitn", "pain.008");
    }

    private static void Validate(string xml, string expectedNs, string rootChild, string label)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new InvalidOperationException($"XML SEPA {label} vacío.");

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"XML SEPA {label} mal formado: {ex.Message}");
        }

        var root = doc.Root ?? throw new InvalidOperationException($"XML SEPA {label} sin elemento raíz.");
        if (root.Name.NamespaceName != expectedNs)
            throw new InvalidOperationException(
                $"XML SEPA {label}: namespace raíz esperado {expectedNs}, recibido {root.Name.NamespaceName}.");

        if (root.Element(root.Name.Namespace + rootChild) is null)
            throw new InvalidOperationException($"XML SEPA {label}: falta elemento {rootChild}.");

        var initn = root.Element(root.Name.Namespace + rootChild)!;
        var grpHdr = initn.Element(root.Name.Namespace + "GrpHdr");
        if (grpHdr is null)
            throw new InvalidOperationException($"XML SEPA {label}: falta GrpHdr.");

        if (grpHdr.Element(root.Name.Namespace + "MsgId") is null)
            throw new InvalidOperationException($"XML SEPA {label}: GrpHdr sin MsgId.");

        if (grpHdr.Element(root.Name.Namespace + "NbOfTxs") is null)
            throw new InvalidOperationException($"XML SEPA {label}: GrpHdr sin NbOfTxs.");

        var pmtInf = initn.Element(root.Name.Namespace + "PmtInf");
        if (pmtInf is null)
            throw new InvalidOperationException($"XML SEPA {label}: falta PmtInf.");

        if (label == "pain.001" && pmtInf.Element(root.Name.Namespace + "CdtTrfTxInf") is null)
            throw new InvalidOperationException($"XML SEPA {label}: PmtInf sin CdtTrfTxInf.");

        if (label == "pain.008" && pmtInf.Element(root.Name.Namespace + "DrctDbtTxInf") is null)
            throw new InvalidOperationException($"XML SEPA {label}: PmtInf sin DrctDbtTxInf.");
    }
}
