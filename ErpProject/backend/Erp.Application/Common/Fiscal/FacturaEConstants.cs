namespace Erp.Application.Common.Fiscal;

/// <summary>Constantes oficiales FacturaE 3.2.2 (BOE 2017, facturae.gob.es).</summary>
public static class FacturaEConstants
{
    /// <summary>Namespace XSD oficial v3.2.2.</summary>
    public const string Namespace =
        "http://www.facturae.gob.es/formato/Versiones/Facturaev3_2_2.xml";

    /// <summary>Namespace obsoleto/erróneo usado en implementaciones antiguas.</summary>
    public const string LegacyWrongNamespace =
        "http://www.facturae.gob.es/formato/Version3.2.2/Facturae32.xsd";

    public const string SchemaVersion = "3.2.2";

    public const string SignaturePolicyUrl =
        "http://www.facturae.es/politica_de_firma_formato_facturae/politica_de_firma_formato_facturae_v3_1.pdf";

    public const string SignaturePolicyHashSha1 = "Ohixl6upD6av8N7pEvDABhEL6hM=";

    public const string SignaturePolicyDescription =
        "Política de firma electrónica para facturación electrónica con formato Facturae";

    /// <summary>Namespace ajeno para extensión Verifactu (representación gráfica).</summary>
    public const string VerifactuExtensionNamespace =
        "urn:es:verifactu:representaciongrafica:1.0";
}
