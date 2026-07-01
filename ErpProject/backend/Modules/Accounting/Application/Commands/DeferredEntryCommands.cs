using MediatR;

namespace Erp.Modules.Accounting.Application.Commands;

/// <summary>
/// Registra un nuevo entrada de periodificación (gasto/ingreso diferido).
/// El job mensual generará automáticamente los asientos de reconocimiento.
/// </summary>
public record CreateDeferredEntryCommand(
    string EntryType,             // "PrepaidExpense" | "DeferredRevenue"
    string Description,
    decimal TotalAmount,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string DeferralAccountCode,   // "480" | "485"
    string CounterpartAccountCode, // 6xx para gastos, 7xx para ingresos
    string? SourceType,
    Guid? SourceId
) : IRequest<Guid>;

/// <summary>
/// Reconoce manualmente la cuota mensual de una periodificación.
/// El job automático llama este mismo command internamente.
/// </summary>
public record RecognizeDeferredEntryMonthCommand(
    Guid DeferredEntryId,
    int Year,
    int Month
) : IRequest<Guid?>; // Devuelve el JournalEntry.Id creado, o null si ya reconocido/completado
