using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Controllers;

/// <summary>
/// Calendario fiscal español: genera y gestiona obligaciones tributarias.
/// </summary>
[ApiController]
[Route("api/fiscal/calendar")]
[Authorize]
public class FiscalCalendarController : ControllerBase
{
    private readonly IApplicationDbContext _ctx;
    private readonly IFiscalCalendarService _calendarService;
    private readonly ITenantContext _tenantContext;

    public FiscalCalendarController(
        IApplicationDbContext ctx,
        IFiscalCalendarService calendarService,
        ITenantContext tenantContext)
    {
        _ctx = ctx;
        _calendarService = calendarService;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// GET /api/fiscal/calendar?year=2026
    /// Lista todos los eventos fiscales del año (generados o creados manualmente).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCalendar([FromQuery] int? year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var query = _ctx.FiscalEvents.AsQueryable();

        if (year.HasValue)
            query = query.Where(e => e.Year == year.Value);
        else
            query = query.Where(e => e.Year == DateTime.UtcNow.Year);

        var events = await query
            .OrderBy(e => e.DeadlineDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(events.Select(e => new
        {
            e.Id,
            e.ModelCode,
            e.ModelName,
            e.Year,
            e.Quarter,
            e.Month,
            DeadlineDate = e.DeadlineDate.ToString("yyyy-MM-dd"),
            ReminderDate = e.ReminderDate.ToString("yyyy-MM-dd"),
            e.Status,
            e.SubmittedAt,
            e.SubmissionReference,
            e.Amount,
            e.Notes,
            DiasRestantes = (e.DeadlineDate.Date - DateTime.UtcNow.Date).Days,
            IsOverdue = e.DeadlineDate.Date < DateTime.UtcNow.Date && e.Status != "Submitted"
        }));
    }

    /// <summary>
    /// GET /api/fiscal/calendar/{id}
    /// Detalle de un evento fiscal.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEvent(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var evt = await _ctx.FiscalEvents
            .Where(e => e.Id == id && e.CompanyId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (evt == null)
            return NotFound(new { error = "Evento no encontrado" });

        return Ok(new
        {
            evt.Id,
            evt.ModelCode,
            evt.ModelName,
            evt.Year,
            evt.Quarter,
            evt.Month,
            DeadlineDate = evt.DeadlineDate.ToString("yyyy-MM-dd"),
            ReminderDate = evt.ReminderDate.ToString("yyyy-MM-dd"),
            evt.Status,
            evt.SubmittedAt,
            evt.SubmissionReference,
            evt.Amount,
            evt.Notes,
            DiasRestantes = (evt.DeadlineDate.Date - DateTime.UtcNow.Date).Days,
            IsOverdue = evt.DeadlineDate.Date < DateTime.UtcNow.Date && evt.Status != "Submitted"
        });
    }

    /// <summary>
    /// POST /api/fiscal/calendar/generate/{year}
    /// Genera automáticamente todos los eventos fiscales del año para la empresa.
    /// Solo crea eventos que no existan ya (idempotente por modelo + período).
    /// </summary>
    [HttpPost("generate/{year:int}")]
    public async Task<IActionResult> GenerateCalendar(int year, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        // Verificar si ya existen eventos para ese año
        var existing = await _ctx.FiscalEvents
            .Where(e => e.CompanyId == tenantId && e.Year == year)
            .AsNoTracking()
            .Select(e => new { e.ModelCode, e.Quarter, e.Month })
            .ToListAsync(ct);

        var newEvents = await _calendarService.GenerateYearCalendarAsync(tenantId, year, ct);

        // Filtrar solo los que no existan ya
        foreach (var evt in newEvents)
        {
            var alreadyExists = existing.Any(e =>
                e.ModelCode == evt.ModelCode
                && e.Quarter == evt.Quarter
                && e.Month == evt.Month);

            if (!alreadyExists)
                _ctx.FiscalEvents.Add(evt);
        }

        await _ctx.SaveChangesAsync(ct);

        return Ok(new
        {
            message = $"Calendario fiscal {year} generado correctamente",
            totalEvents = newEvents.Count,
            newEvents = newEvents.Count(e => !existing.Any(ex =>
                ex.ModelCode == e.ModelCode && ex.Quarter == e.Quarter && ex.Month == e.Month)),
            skipped = existing.Count
        });
    }

    /// <summary>
    /// PATCH /api/fiscal/calendar/{id}/submit
    /// Marca un evento como Submitted (presentado).
    /// </summary>
    [HttpPatch("{id:guid}/submit")]
    public async Task<IActionResult> MarkSubmitted(
        Guid id,
        [FromBody] SubmitFiscalEventRequest request,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var evt = await _ctx.FiscalEvents
            .Where(e => e.Id == id && e.CompanyId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (evt == null)
            return NotFound(new { error = "Evento no encontrado" });

        evt.Status = "Submitted";
        evt.SubmittedAt = DateTime.UtcNow;
        evt.SubmissionReference = request.SubmissionReference;

        await _ctx.SaveChangesAsync(ct);

        return Ok(new { message = "Evento marcado como presentado", evt.Status });
    }

    /// <summary>
    /// GET /api/fiscal/calendar/overdue
    /// Lista los eventos fiscales vencidos (pendientes y con deadline pasado).
    /// </summary>
    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var now = DateTime.UtcNow;
        var overdue = await _ctx.FiscalEvents
            .Where(e => e.CompanyId == tenantId
                     && e.Status != "Submitted"
                     && e.DeadlineDate < now.Date)
            .OrderBy(e => e.DeadlineDate)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(overdue.Select(e => new
        {
            e.Id,
            e.ModelCode,
            e.ModelName,
            e.Year,
            e.Quarter,
            e.Month,
            DeadlineDate = e.DeadlineDate.ToString("yyyy-MM-dd"),
            DiasRestantes = (e.DeadlineDate.Date - now.Date).Days,
            e.Status
        }));
    }

    /// <summary>
    /// POST /api/fiscal/calendar/events
    /// Crea un evento manual (para obligaciones no standard).
    /// </summary>
    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent(
        [FromBody] CreateFiscalEventRequest request,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var evt = new FiscalEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            ModelCode = request.ModelCode,
            ModelName = request.ModelName,
            Year = request.Year,
            Quarter = request.Quarter,
            Month = request.Month,
            DeadlineDate = request.DeadlineDate,
            ReminderDate = request.ReminderDate,
            Status = "Pending",
            Amount = request.Amount,
            Notes = request.Notes,
            IsAutoGenerated = false,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.FiscalEvents.Add(evt);
        await _ctx.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetEvent), new { id = evt.Id }, new { id = evt.Id, evt.ModelName, evt.DeadlineDate });
    }
}

public record SubmitFiscalEventRequest(string? SubmissionReference);
public record CreateFiscalEventRequest(
    string ModelCode,
    string ModelName,
    int Year,
    int? Quarter,
    int? Month,
    DateTime DeadlineDate,
    DateTime ReminderDate,
    decimal? Amount,
    string? Notes
);
