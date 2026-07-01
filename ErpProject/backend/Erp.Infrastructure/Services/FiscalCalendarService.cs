using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Implementación del calendario fiscal español.
/// Genera automáticamente los eventos según la normativa LGT (art. 19) y específica de cada modelo.
/// </summary>
public class FiscalCalendarService : IFiscalCalendarService
{
    private readonly IApplicationDbContext _ctx;
    private readonly ILogger<FiscalCalendarService> _log;

    public FiscalCalendarService(IApplicationDbContext ctx, ILogger<FiscalCalendarService> log)
    {
        _ctx = ctx;
        _log = log;
    }

    public async Task<List<FiscalEvent>> GenerateYearCalendarAsync(Guid companyId, int year, CancellationToken ct = default)
    {
        var events = new List<FiscalEvent>();

        // ── Modelo 303 — IVA trimestral (art. 71 RD 1624/1992) ─────────────────
        // Plazo: 30 días naturales después del trimestre (20 de abril, julio, octubre;
        // 30 de enero para el 4T, con transmisión telemática obligatoria)
        var modelo303Deadlines = new[]
        {
            (quarter: 1, deadline: new DateTime(year, 4, 20),  reminder: new DateTime(year, 4, 13)),
            (quarter: 2, deadline: new DateTime(year, 7, 20),  reminder: new DateTime(year, 7, 13)),
            (quarter: 3, deadline: new DateTime(year, 10, 20), reminder: new DateTime(year, 10, 13)),
            (quarter: 4, deadline: new DateTime(year + 1, 1, 30), reminder: new DateTime(year, 12, 23)),
        };

        foreach (var (quarter, deadline, reminder) in modelo303Deadlines)
        {
            events.Add(CreateEvent(companyId, year, "303", $"IVA Trimestral T{quarter} {year}",
                quarter, null, deadline, reminder));
        }

        // ── Modelo 111 — Retenciones IRPF trimestrales ─────────────────────────
        // Plazo igual que el 303: día 20 del mes siguiente (30 enero para 4T)
        var modelo111Deadlines = new[]
        {
            (quarter: 1, deadline: new DateTime(year, 4, 20),  reminder: new DateTime(year, 4, 13)),
            (quarter: 2, deadline: new DateTime(year, 7, 20),  reminder: new DateTime(year, 7, 13)),
            (quarter: 3, deadline: new DateTime(year, 10, 20), reminder: new DateTime(year, 10, 13)),
            (quarter: 4, deadline: new DateTime(year + 1, 1, 30), reminder: new DateTime(year, 12, 23)),
        };

        foreach (var (quarter, deadline, reminder) in modelo111Deadlines)
        {
            events.Add(CreateEvent(companyId, year, "111", $"Retenciones IRPF T{quarter} {year}",
                quarter, null, deadline, reminder));
        }

        // ── Modelo 347 — Operaciones con terceros (febrero) ───────────────────────
        // Plazo: febrero (presentación telemática obligatoria desde 2017)
        // Art. 29 RD 1065/2007
        events.Add(CreateEvent(companyId, year, "347", $"Operaciones con Terceros {year}",
            null, null, new DateTime(year + 1, 2, 20), new DateTime(year + 1, 2, 13)));

        // ── Modelo 349 — Intracomunitarias ───────────────────────────────────────
        // Plazo: 30 días naturales después del período (20 del mes siguiente)
        var modelo349Deadlines = new[]
        {
            (quarter: 1, deadline: new DateTime(year, 4, 20),  reminder: new DateTime(year, 4, 13)),
            (quarter: 2, deadline: new DateTime(year, 7, 20),  reminder: new DateTime(year, 7, 13)),
            (quarter: 3, deadline: new DateTime(year, 10, 20), reminder: new DateTime(year, 10, 13)),
            (quarter: 4, deadline: new DateTime(year + 1, 1, 30), reminder: new DateTime(year, 12, 23)),
        };

        foreach (var (quarter, deadline, reminder) in modelo349Deadlines)
        {
            events.Add(CreateEvent(companyId, year, "349", $"Operaciones Intracomunitarias T{quarter} {year}",
                quarter, null, deadline, reminder));
        }

        // ── Modelo 390 — Resumen Anual IVA (dentro del 4T, hasta 30 enero) ──────
        // Plazo: 30 días naturales después del período (30 de enero del año siguiente)
        events.Add(CreateEvent(companyId, year, "390", $"Resumen Anual IVA {year}",
            null, null, new DateTime(year + 1, 1, 30), new DateTime(year + 1, 1, 23)));

        // ── Modelo 190 — Resumen anual retenciones (febrero) ────────────────────
        events.Add(CreateEvent(companyId, year, "190", $"Resumen Anual Retenciones {year}",
            null, null, new DateTime(year + 1, 2, 20), new DateTime(year + 1, 2, 13)));

        // ── Modelo 130 — Pago fraccionado Impuesto Sociedades ───────────────────
        // 3 pagos: abril (20 días hábiles), octubre (20 días), diciembre (20 días)
        // Para sociedades que opten por pagos fraccionados (art. 40.2 LIS)
        events.Add(CreateEvent(companyId, year, "130", $"Pago Fraccionado IS 1er Trimestre {year}",
            1, null, new DateTime(year, 4, 20), new DateTime(year, 4, 13)));
        events.Add(CreateEvent(companyId, year, "130", $"Pago Fraccionado IS 2º Trimestre {year}",
            2, null, new DateTime(year, 10, 20), new DateTime(year, 10, 13)));
        events.Add(CreateEvent(companyId, year, "130", $"Pago Fraccionado IS 3er Trimestre {year}",
            3, null, new DateTime(year + 1, 12, 20), new DateTime(year, 12, 13)));

        // ── Modelo 180 — Arrendamientos (febrero) ────────────────────────────────
        // Retenciones por arrendamiento: presentación anual
        events.Add(CreateEvent(companyId, year, "180", $"Retenciones Arrendamientos {year}",
            null, null, new DateTime(year + 1, 2, 20), new DateTime(year + 1, 2, 13)));

        _log.LogInformation("Generados {Count} eventos fiscales para empresa {CompanyId} año {Year}",
            events.Count, companyId, year);

        return events;
    }

    public async Task<List<FiscalEvent>> GetPendingEventsAsync(Guid companyId, int year, CancellationToken ct = default)
    {
        return await _ctx.FiscalEvents
            .Where(e => e.CompanyId == companyId && e.Year == year && e.Status == "Pending")
            .OrderBy(e => e.DeadlineDate)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<List<FiscalEvent>> GetOverdueEventsAsync(Guid companyId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _ctx.FiscalEvents
            .Where(e => e.CompanyId == companyId
                     && (e.Status == "Pending" || e.Status == "Reminded")
                     && e.DeadlineDate < now)
            .OrderBy(e => e.DeadlineDate)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<FiscalEvent> MarkAsSubmittedAsync(Guid eventId, string? submissionReference, CancellationToken ct = default)
    {
        var evt = await _ctx.FiscalEvents.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw new InvalidOperationException($"FiscalEvent {eventId} no encontrado");

        evt.Status = "Submitted";
        evt.SubmittedAt = DateTime.UtcNow;
        evt.SubmissionReference = submissionReference;

        await _ctx.SaveChangesAsync(ct);
        _log.LogInformation("Evento fiscal {EventId} ({ModelCode}) marcado como Submitted. Ref: {Ref}",
            eventId, evt.ModelCode, submissionReference);

        return evt;
    }

    private static FiscalEvent CreateEvent(
        Guid companyId, int year, string modelCode, string modelName,
        int? quarter, int? month, DateTime deadline, DateTime reminder)
    {
        return new FiscalEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ModelCode = modelCode,
            ModelName = modelName,
            Year = year,
            Quarter = quarter,
            Month = month,
            DeadlineDate = deadline,
            ReminderDate = reminder,
            Status = "Pending",
            IsAutoGenerated = true,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
