using System.Xml.Linq;

namespace Erp.Application.Common.Fiscal;

/// <summary>Validación estructural FacturaE 3.2.2 sin certificado (homologación offline).</summary>
public static class FacturaEXmlStructureValidator
{
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

        var ns = root.Name.NamespaceName;
        if (ns == FacturaEConstants.LegacyWrongNamespace)
        {
            errors.Add(
                $"Namespace obsoleto detectado ('{FacturaEConstants.LegacyWrongNamespace}'). " +
                $"Use '{FacturaEConstants.Namespace}'.");
        }
        else if (ns != FacturaEConstants.Namespace)
        {
            errors.Add(
                $"Namespace raíz debe ser '{FacturaEConstants.Namespace}', encontrado '{ns}'.");
        }

        var header = root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "FileHeader");
        if (header is null)
            errors.Add("Falta elemento FileHeader.");
        else
        {
            var schemaVersion = header.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "SchemaVersion");
            if (schemaVersion?.Value != FacturaEConstants.SchemaVersion)
            {
                var found = schemaVersion?.Value ?? "(vacío)";
                errors.Add($"SchemaVersion debe ser {FacturaEConstants.SchemaVersion}, encontrado: {found}");
            }

            ValidateExtensions(header, errors, warnings);
        }

        if (root.Descendants().FirstOrDefault(e => e.Name.LocalName == "SellerParty") is null)
            errors.Add("Falta SellerParty en Parties.");
        if (root.Descendants().FirstOrDefault(e => e.Name.LocalName == "BuyerParty") is null)
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

        if (root.Descendants().FirstOrDefault(e => e.Name.LocalName == "TaxesOutputs") is null)
            warnings.Add("Sin TaxesOutputs (factura sin IVA desglosado).");

        return new FacturaEValidationResult(errors.Count == 0, errors, warnings);
    }

    private static void ValidateExtensions(XElement header, List<string> errors, List<string> warnings)
    {
        var extensions = header.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Extensions");
        if (extensions is null) return;

        foreach (var extension in extensions.Elements().Where(e => e.Name.LocalName == "Extension"))
        {
            if (extension.Elements().Any(e => e.Name.LocalName is "ExtensionCode" or "ExtensionName"))
            {
                errors.Add(
                    "Extensions/Extension no admite ExtensionCode/ExtensionName en FacturaE 3.2.2; " +
                    "use ExtensionContent con namespace ajeno.");
            }

            var content = extension.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ExtensionContent");
            if (content is null)
                warnings.Add("Extension sin ExtensionContent.");
        }
    }
}

public record FacturaEValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
