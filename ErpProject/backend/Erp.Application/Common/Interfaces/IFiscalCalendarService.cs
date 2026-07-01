using Erp.Domain.Entities.Core;

namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Genera y gestiona eventos del calendario fiscal español para una empresa.
/// </summary>
public interface IFiscalCalendarService
{
    /// <summary>
    /// Genera todos los eventos fiscales del año para la empresa.
    /// Incluye: 303 (trimestral), 111 (trimestral), 347 (anual),
    /// 349 (trimestral), 390 (anual), 130 (pago fraccionado IS).
    /// </summary>
    Task<List<FiscalEvent>> GenerateYearCalendarAsync(Guid companyId, int year, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los eventos fiscales pendientes para el año.
    /// </summary>
    Task<List<FiscalEvent>> GetPendingEventsAsync(Guid companyId, int year, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los eventos cuya fecha de recordatorio ya pasó pero siguen Pending.
    /// </summary>
    Task<List<FiscalEvent>> GetOverdueEventsAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Marca un evento como Submitted.
    /// </summary>
    Task<FiscalEvent> MarkAsSubmittedAsync(Guid eventId, string? submissionReference, CancellationToken ct = default);
}
