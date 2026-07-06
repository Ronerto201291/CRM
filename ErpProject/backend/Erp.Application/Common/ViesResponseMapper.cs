namespace Erp.Application.Common;

using Erp.Application.Common.Interfaces;

/// <summary>
/// Texto orientativo compartido entre TaxController y ValidateViesCommand.
/// </summary>
public static class ViesResponseMapper
{
    public static string BuildAdvice(ViesValidationResult result) => result.IsValid
        ? $"NIF UE válido. Puede emitir factura exenta de IVA (art. 25 LIVA) a {result.Name ?? "este operador"}."
        : "NIF UE no validado. No puede aplicar exención intracomunitaria hasta confirmar la validez.";
}
