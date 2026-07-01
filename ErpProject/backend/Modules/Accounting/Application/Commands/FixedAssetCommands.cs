using MediatR;

namespace Erp.Modules.Accounting.Application.Commands;

/// <summary>Registra un nuevo activo fijo en el módulo de amortizaciones.</summary>
public record CreateFixedAssetCommand(
    string AssetCode,
    string Name,
    string? Description,
    DateTime AcquisitionDate,
    DateTime CommissioningDate,
    decimal AcquisitionCost,
    decimal ResidualValue,
    int UsefulLifeYears,
    string AmortizationMethod,
    string AssetAccountCode,
    string DepreciationAccountCode,
    string AccumDepreciationAccountCode,
    string? Notes
) : IRequest<Guid>;

/// <summary>Actualiza metadatos no fiscales del activo (nombre, vida útil, valor residual).</summary>
public record UpdateFixedAssetCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal ResidualValue,
    int UsefulLifeYears,
    string? Notes
) : IRequest;

/// <summary>Da de baja el activo (enajenación o pérdida). Status → Disposed.</summary>
public record DisposeFixedAssetCommand(
    Guid Id,
    DateTime DisposedAt,
    string? Notes
) : IRequest;

/// <summary>
/// Genera manualmente el asiento de dotación de amortización para un mes concreto.
/// El job automático llama este mismo command internamente.
/// </summary>
public record PostMonthlyAmortizationCommand(
    Guid AssetId,
    int Year,
    int Month
) : IRequest<Guid?>; // Devuelve el JournalEntry.Id creado, o null si no hay dotación
