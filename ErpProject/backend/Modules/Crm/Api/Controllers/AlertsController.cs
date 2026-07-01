using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly ICrmDbContext _db;
    private readonly ITenantContext _tenant;

    public AlertsController(ICrmDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/crm/alerts — all alerts (newest first)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var alerts = await _db.ScheduledAlerts
            .OrderByDescending(a => a.ScheduledAt)
            .Select(a => MapAlert(a))
            .ToListAsync();
        return Ok(alerts);
    }

    // GET /api/crm/alerts/pending — alerts due now that haven't been acknowledged
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var now = DateTime.UtcNow;
        var pending = await _db.ScheduledAlerts
            .Where(a => !a.IsAcknowledged &&
                        (a.SnoozedUntil == null ? a.ScheduledAt <= now : a.SnoozedUntil <= now))
            .OrderBy(a => a.ScheduledAt)
            .Select(a => MapAlert(a))
            .ToListAsync();
        return Ok(pending);
    }

    // POST /api/crm/alerts — create
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { error = "Title is required" });

        // Resolve client name if ClientId provided
        string? clientName = null;
        if (req.ClientId.HasValue)
            clientName = await _db.Clients
                .Where(c => c.Id == req.ClientId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();

        var alert = new ScheduledAlert
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId!.Value,
            Title = req.Title,
            Description = req.Description,
            ScheduledAt = req.ScheduledAt.ToUniversalTime(),
            ClientId = req.ClientId,
            ClientName = clientName,
        };

        _db.ScheduledAlerts.Add(alert);
        await _db.SaveChangesAsync(CancellationToken.None);
        return Ok(MapAlert(alert));
    }

    // PUT /api/crm/alerts/{id} — update
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateAlertRequest req)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == id);
        if (alert is null) return NotFound();

        if (req.ClientId.HasValue && req.ClientId != alert.ClientId)
            alert.ClientName = await _db.Clients
                .Where(c => c.Id == req.ClientId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();
        else if (!req.ClientId.HasValue)
            alert.ClientName = null;

        alert.Title = req.Title;
        alert.Description = req.Description;
        alert.ScheduledAt = req.ScheduledAt.ToUniversalTime();
        alert.ClientId = req.ClientId;
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);
        return Ok(MapAlert(alert));
    }

    // PATCH /api/crm/alerts/{id}/acknowledge
    [HttpPatch("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == id);
        if (alert is null) return NotFound();

        alert.IsAcknowledged = true;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);
        return NoContent();
    }

    // PATCH /api/crm/alerts/{id}/snooze
    [HttpPatch("{id:guid}/snooze")]
    public async Task<IActionResult> Snooze(Guid id, [FromBody] SnoozeRequest req)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == id);
        if (alert is null) return NotFound();

        alert.SnoozedUntil = req.SnoozedUntil.ToUniversalTime();
        alert.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);
        return NoContent();
    }

    // DELETE /api/crm/alerts/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var alert = await _db.ScheduledAlerts.FirstOrDefaultAsync(a => a.Id == id);
        if (alert is null) return NotFound();

        _db.ScheduledAlerts.Remove(alert);
        await _db.SaveChangesAsync(CancellationToken.None);
        return NoContent();
    }

    private static object MapAlert(ScheduledAlert a) => new
    {
        a.Id,
        a.Title,
        a.Description,
        a.ScheduledAt,
        a.ClientId,
        a.ClientName,
        a.IsAcknowledged,
        a.AcknowledgedAt,
        a.SnoozedUntil,
        a.CreatedAt,
        a.UpdatedAt,
    };
}

public record CreateAlertRequest(string Title, string? Description, DateTime ScheduledAt, Guid? ClientId);
public record SnoozeRequest(DateTime SnoozedUntil);
