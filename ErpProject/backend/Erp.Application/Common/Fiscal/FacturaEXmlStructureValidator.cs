using System.Xml.Linq;

namespace Erp.Application.Common.Fiscal;

/// <summary>Validación estructural FacturaE 3.2.2 sin certificado (homologación offline).</summary>
public static class FacturaEXmlStructureValidator
{
    // Namespace real de FacturaE 3.2.2 (facturae.gob.es); antes apuntaba a
    // "Version3.2.2/Facturae32.xsd", que no es el namespace oficial de la
    // versión — un receptor FACe/validador real rechazaría el documento.
    // Única fuente de verdad: FacturaEService.cs referencia esta misma
    // constante para que generador y validador no puedan volver a divergir.
    public const string FacturaENamespace =
        "http://www.facturae.gob.es/formato/Versiones/Facturaev3_2_2.xml";

    public static FacturaEValidationResult Validate(string xml)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            return new FacturaEValidationResult(false, [$"XML mal formado: {ex.Message}"], []);
        }

        var root = doc.Root;
        if (root is null)
        {
            errors.Add("Documento sin elemento raíz.");
            return new FacturaEValidationResult(false, errors, warnings);
        }

        if (root.Name.LocalName != "Facturae")
            errors.Add($"Raíz esperada 'Facturae', encontrada '{root.Name.LocalName}'.");

        if (root.Name.NamespaceName != FacturaENamespace)
            errors.Add($"Namespace raíz debe ser '{FacturaENamespace}', encontrado '{root.Name.NamespaceName}'.");

        var header = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "FileHeader" && e.Name.NamespaceName == FacturaENamespace);
        if (header is null)
            errors.Add("Falta elemento FileHeader.");
        else
        {
            var schemaVersion = header.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "SchemaVersion");
            if (schemaVersion?.Value != "3.2.2")
            {
                var found = schemaVersion?.Value ?? "(vacío)";
                errors.Add($"SchemaVersion debe ser 3.2.2, encontrado: {found}");
            }
        }

        var seller = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "SellerParty");
        var buyer = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "BuyerParty");
        if (seller is null)
            errors.Add("Falta SellerParty en Parties.");
        if (buyer is null)
            errors.Add("Falta BuyerParty en Parties.");

        var invoice = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Invoice" && e.Parent?.Name.LocalName == "Invoices");
        if (invoice is null)
            errors.Add("Falta Invoices/Invoice.");
        else
        {
            var invoiceNumber = invoice.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "InvoiceNumber");
            if (invoiceNumber is null || string.IsNullOrWhiteSpace(invoiceNumber.Value))
                errors.Add("Falta InvoiceNumber.");
        }

        var taxesOutputs = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "TaxesOutputs");
        if (taxesOutputs is null)
            warnings.Add("Sin TaxesOutputs (factura sin IVA desglosado).");

        return new FacturaEValidationResult(errors.Count == 0, errors, warnings);
    }
}

public record FacturaEValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
