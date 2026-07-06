using System.Xml.Linq;
using Erp.Application.Features.Sii.Models;

namespace Erp.Infrastructure.Services.Sii;

/// <summary>Validación estructural del XML SII sin certificado (homologación offline).</summary>
public static class SiiXmlStructureValidator
{
    public static SiiValidationResult Validate(string xml, SiiInvoiceType type)
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
            return new SiiValidationResult(false, [$"XML mal formado: {ex.Message}"], []);
        }

        var root = doc.Root;
        if (root is null)
        {
            errors.Add("Documento sin elemento raíz.");
            return new SiiValidationResult(false, errors, warnings);
        }

        var expectedRoot = type == SiiInvoiceType.Emitidas
            ? "SuministroLRFacturasEmitidas"
            : "SuministroLRFacturasRecibidas";

        if (root.Name.LocalName != expectedRoot)
            errors.Add($"Raíz esperada '{expectedRoot}', encontrada '{root.Name.LocalName}'.");

        if (root.Name.NamespaceName != SiiNamespaces.LR)
            errors.Add($"Namespace raíz debe ser '{SiiNamespaces.LR}', encontrado '{root.Name.NamespaceName}'.");

        var cabecera = root.Elements().FirstOrDefault(e => e.Name.LocalName == "Cabecera");
        if (cabecera is null)
            errors.Add("Falta elemento Cabecera.");
        else
        {
            if (cabecera.Name.NamespaceName != SiiNamespaces.Info)
                errors.Add($"Cabecera debe estar en namespace Info ({SiiNamespaces.Info}).");

            var titular = cabecera.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Titular");
            if (titular is null)
                errors.Add("Cabecera sin Titular.");
            else if (titular.Descendants().All(e => e.Name.LocalName != "NIF"))
                errors.Add("Titular sin NIF.");

            var periodo = cabecera.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "PeriodoLiquidacion");
            if (periodo is null)
                warnings.Add("Cabecera sin PeriodoLiquidacion (puede ser alta inicial).");
        }

        var registros = root.Elements()
            .Where(e => e.Name.LocalName.StartsWith("RegistroLR", StringComparison.Ordinal))
            .ToList();

        if (registros.Count == 0)
            warnings.Add("Sin registros de factura en el periodo (XML vacío válido).");

        foreach (var reg in registros)
        {
            if (reg.Name.NamespaceName != SiiNamespaces.LR)
                errors.Add($"Registro '{reg.Name.LocalName}' con namespace incorrecto.");
        }

        if (type == SiiInvoiceType.Emitidas)
        {
            foreach (var reg in registros)
            {
                var contraparte = reg.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Contraparte");
                if (contraparte is null)
                    errors.Add("Factura emitida sin Contraparte.");
            }
        }
        else
        {
            foreach (var reg in registros)
            {
                var cuotaSoportada = reg.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "CuotaSoportada");
                if (cuotaSoportada is null)
                    warnings.Add("Factura recibida sin CuotaSoportada explícita.");
            }
        }

        return new SiiValidationResult(errors.Count == 0, errors, warnings);
    }
}

public record SiiValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
