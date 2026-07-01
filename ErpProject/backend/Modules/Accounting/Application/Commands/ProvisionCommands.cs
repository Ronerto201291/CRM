using MediatR;

namespace Erp.Modules.Accounting.Application.Commands;

/// <summary>Registra una nueva provisión contable (490, 499, 147…).</summary>
public record CreateProvisionCommand(
    string Code,
    string Description,
    decimal Amount,
    DateTime DueDate,
    string? Notes
) : IRequest<Guid>;

/// <summary>Actualiza importe y vencimiento de una provisión activa.</summary>
public record UpdateProvisionCommand(
    Guid Id,
    string Description,
    decimal Amount,
    DateTime DueDate
) : IRequest;

/// <summary>
/// Libera una provisión (Status → Released) y genera el asiento contable inverso.
/// Cuenta debe → código de provisión (490/499/147…)
/// Cuenta haber → 795 (Exceso provisiones)
/// </summary>
public record ReleaseProvisionCommand(
    Guid Id
) : IRequest;

/// <summary>Elimina una provisión en estado Draft (no ha generado asiento).</summary>
public record DeleteProvisionCommand(Guid Id) : IRequest;
