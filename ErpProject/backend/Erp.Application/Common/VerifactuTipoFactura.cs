namespace Erp.Application.Common;

/// <summary>Códigos TipoFactura VeriFactu / SII (RD 1007/2023).</summary>
public static class VerifactuTipoFactura
{
    public static string Resolve(string? invoiceType) => invoiceType switch
    {
        "Rectificativa" => "R1",
        "Simplificada"  => "F2",
        _               => "F1"
    };
}
