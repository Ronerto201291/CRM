using Erp.Application.Common.Attributes;
using Erp.Modules.Crm.Application.Features.Notes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController]
[Route("api/crm/notes")]
[Authorize]
[RequiredModule("CRM")]
public class CrmNotesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CrmNotesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Note.Read)]
    public async Task<IActionResult> GetNotes([FromQuery] string entityType, [FromQuery] Guid entityId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetNotesQuery(entityType, entityId), ct));

    [HttpPost]
    [RequirePermission(Permissions.Note.Create)]
    public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CreateNoteCommand(req.EntityType, req.EntityId, req.Title, req.Content), ct);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Note.Update)]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateNoteRequest req, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new UpdateNoteCommand(id, req.Title, req.Content), ct));
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Note.Delete)]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteNoteCommand(id), ct);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

public record CreateNoteRequest(string EntityType, Guid EntityId, string? Title, string Content);
public record UpdateNoteRequest(string? Title, string Content);
