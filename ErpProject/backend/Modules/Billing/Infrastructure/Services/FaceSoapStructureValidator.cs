using System.Xml.Linq;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>Validación estructural del sobre SOAP FACe sin envío HTTP.</summary>
public static class FaceSoapStructureValidator
{
    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string FaceNs = "https://face.gob.es/schema";

    public static FaceSoapValidationResult Validate(string soap)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(soap);
        }
        catch (Exception ex)
        {
            return new FaceSoapValidationResult(false, [$"SOAP mal formado: {ex.Message}"], []);
        }

        var envelope = doc.Root;
        if (envelope is null || envelope.Name.LocalName != "Envelope")
        {
            errors.Add("Falta elemento raíz soapenv:Envelope.");
            return new FaceSoapValidationResult(false, errors, warnings);
        }

        if (envelope.Name.NamespaceName != SoapNs)
            warnings.Add($"Namespace SOAP esperado '{SoapNs}'.");

        var submit = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "SubmitInvoice");
        if (submit is null)
        {
            errors.Add("Falta face:SubmitInvoice en el cuerpo SOAP.");
            return new FaceSoapValidationResult(false, errors, warnings);
        }

        if (submit.Name.NamespaceName != FaceNs)
            warnings.Add($"SubmitInvoice debería estar en namespace '{FaceNs}'.");

        var fileName = submit.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "FileName")?.Value;
        if (string.IsNullOrWhiteSpace(fileName))
            errors.Add("Falta o está vacío face:FileName.");

        var content = submit.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Content")?.Value;
        if (string.IsNullOrWhiteSpace(content))
            errors.Add("Falta o está vacío face:Content (Base64).");
        else
        {
            try { Convert.FromBase64String(content); }
            catch (FormatException) { errors.Add("face:Content no es Base64 válido."); }
        }

        return new FaceSoapValidationResult(errors.Count == 0, errors, warnings);
    }
}

public record FaceSoapValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
