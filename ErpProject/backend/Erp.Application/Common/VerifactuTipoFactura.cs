namespace Erp.Application.Common;

/// <summary>Códigos TipoFactura VeriFactu / SII (RD 1007/2023).</summary>
public static class VerifactuTipoFactura
{
    public static string Resolve(string? invoiceType, string? rectifiedInvoiceType = null)
    {
        if (string.Equals(invoiceType, "Rectificativa", StringComparison.OrdinalIgnoreCase))
            return string.Equals(rectifiedInvoiceType, "Simplificada", StringComparison.OrdinalIgnoreCase)
                ? "R5"
                : "R1";

        if (string.Equals(invoiceType, "Simplificada", StringComparison.OrdinalIgnoreCase))
            return "F2";

        return "F1";
    }
}
