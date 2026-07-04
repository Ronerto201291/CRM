namespace Erp.Modules.Billing.Application.Features.Billing;

/// <summary>
/// Métodos de cobro soportados (ADR-0018 #42b). "transfer" se mantiene como
/// sinónimo de "bank" por compatibilidad con clientes existentes del API.
/// </summary>
public static class PaymentMethods
{
    public const string Cash = "cash";
    public const string Bank = "bank";
    public const string Card = "card";
    public const string Bizum = "bizum";
    public const string Transfer = "transfer";

    private static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
    {
        Cash, Bank, Card, Bizum, Transfer
    };

    public static bool IsValid(string paymentMethod) => Valid.Contains(paymentMethod);

    /// <summary>
    /// Código de cuenta contable de tesorería donde se registra el cobro.
    /// Card/Bizum van a una cuenta de "pendiente de liquidar" distinta de un
    /// banco normal porque el importe llega neto de comisión y con desfase de
    /// días — así BankReconciliationService (Treasury) puede conciliarlos por
    /// separado de una transferencia corriente.
    /// </summary>
    public static string AccountingCode(string paymentMethod) => paymentMethod.ToLowerInvariant() switch
    {
        Cash => "570",
        Card => "5721",
        Bizum => "5722",
        _ => "572", // bank | transfer
    };
}
