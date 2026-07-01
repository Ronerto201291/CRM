using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/notes")]
[Authorize]
public class CrmNotesController : ControllerBase
{
    private readonly ICrmDbContext _db;
    private readonly ITenantContext _tenant;

    public CrmNotesController(ICrmDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/crm/notes?entityType=Client&entityId={id}
    [HttpGet]
    public async Task<IActionResult> GetNotes([FromQuery] string entityType, [FromQuery] Guid entityId)
    {
        var notes = await _db.Notes
            .Where(n => n.EntityType == entityType && n.EntityId == entityId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.EntityType,
                n.EntityId,
                n.Title,
                n.Content,
                n.CreatedAt,
                n.UpdatedAt,
            })
            .ToListAsync();

        return Ok(notes);
    }

    // POST /api/crm/notes
    [HttpPost]
    public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { error = "Content is required" });

        // Validate entity belongs to tenant
        if (!await EntityBelongsToTenant(req.EntityType, req.EntityId))
            return NotFound();

        var note = new CrmNote
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId!.Value,
            EntityType = req.EntityType,
            EntityId = req.EntityId,
            Title = req.Title,
            Content = req.Content,
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(new { note.Id, note.EntityType, note.EntityId, note.Title, note.Content, note.CreatedAt, note.UpdatedAt });
    }

    // PUT /api/crm/notes/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { error = "Content is required" });

        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return NotFound();

        note.Title = req.Title;
        note.Content = req.Content;
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(CancellationToken.None);
        return Ok(new { note.Id, note.Title, note.Content, note.UpdatedAt });
    }

    // DELETE /api/crm/notes/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return NotFound();

        _db.Notes.Remove(note);
        await _db.SaveChangesAsync(CancellationToken.None);
        return NoContent();
    }

    private async Task<bool> EntityBelongsToTenant(string entityType, Guid entityId)
    {
        return entityType switch
        {
            "Client" => await _db.Clients.AnyAsync(c => c.Id == entityId),
            "Lead"   => await _db.Leads.AnyAsync(l => l.Id == entityId),
            _        => false,
        };
    }
}

public record CreateNoteRequest(string EntityType, Guid EntityId, string? Title, string Content);
public record UpdateNoteRequest(string? Title, string Content);
