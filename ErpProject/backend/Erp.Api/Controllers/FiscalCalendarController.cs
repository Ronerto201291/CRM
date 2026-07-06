using Erp.Application.Features.FiscalCalendar;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>
/// Calendario fiscal español: genera y gestiona obligaciones tributarias.
/// </summary>
[ApiController]
[Route("api/fiscal/calendar")]
[Authorize]
public class FiscalCalendarController : ControllerBase
{
    private readonly IMediator _mediator;

    public FiscalCalendarController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetCalendar([FromQuery] int? year, CancellationToken ct)
    {
        var events = await _mediator.Send(new GetFiscalCalendarQuery(year), ct);
        return Ok(events.Select(e => new
        {
            e.Id,
            e.ModelCode,
            e.ModelName,
            e.Year,
            e.Quarter,
            e.Month,
            deadlineDate = e.DeadlineDate,
            reminderDate = e.ReminderDate,
            e.Status,
            e.SubmittedAt,
            e.SubmissionReference,
            e.Amount,
            e.Notes,
            diasRestantes = e.DiasRestantes,
            isOverdue = e.IsOverdue
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEvent(Guid id, CancellationToken ct)
    {
        var evt = await _mediator.Send(new GetFiscalEventQuery(id), ct);
        if (evt is null)
            return NotFound(new { error = "Evento no encontrado" });

        return Ok(new
        {
            evt.Id,
            evt.ModelCode,
            evt.ModelName,
            evt.Year,
            evt.Quarter,
            evt.Month,
            deadlineDate = evt.DeadlineDate,
            reminderDate = evt.ReminderDate,
            evt.Status,
            evt.SubmittedAt,
            evt.SubmissionReference,
            evt.Amount,
            evt.Notes,
            diasRestantes = evt.DiasRestantes,
            isOverdue = evt.IsOverdue
        });
    }

    [HttpPost("generate/{year:int}")]
    public async Task<IActionResult> GenerateCalendar(int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateFiscalCalendarCommand(year), ct);
        return Ok(new
        {
            message = result.Message,
            totalEvents = result.TotalEvents,
            newEvents = result.NewEvents,
            skipped = result.Skipped
        });
    }

    [HttpPatch("{id:guid}/submit")]
    public async Task<IActionResult> MarkSubmitted(
        Guid id,
        [FromBody] SubmitFiscalEventRequest request,
        CancellationToken ct)
    {
        var evt = await _mediator.Send(new MarkFiscalEventSubmittedCommand(id, request.SubmissionReference), ct);
        if (evt is null)
            return NotFound(new { error = "Evento no encontrado" });

        return Ok(new { message = "Evento marcado como presentado", status = evt.Status });
    }

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
    {
        var overdue = await _mediator.Send(new GetOverdueFiscalEventsQuery(), ct);
        return Ok(overdue.Select(e => new
        {
            e.Id,
            e.ModelCode,
            e.ModelName,
            e.Year,
            e.Quarter,
            e.Month,
            deadlineDate = e.DeadlineDate,
            diasRestantes = e.DiasRestantes,
            e.Status
        }));
    }

    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent(
        [FromBody] CreateFiscalEventRequest request,
        CancellationToken ct)
    {
        var created = await _mediator.Send(new CreateFiscalEventCommand(
            request.ModelCode,
            request.ModelName,
            request.Year,
            request.Quarter,
            request.Month,
            request.DeadlineDate,
            request.ReminderDate,
            request.Amount,
            request.Notes), ct);

        return CreatedAtAction(nameof(GetEvent), new { id = created.Id }, new
        {
            id = created.Id,
            modelName = created.ModelName,
            deadlineDate = created.DeadlineDate
        });
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
    string? Notes);
