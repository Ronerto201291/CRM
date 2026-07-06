using System.Security.Claims;
using Erp.Application.Features.Documents.Commands;
using Erp.Application.Features.Documents.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>Biblioteca interna de documentos (ADR-0018 #42d).</summary>
[ApiController, Route("api/documents"), Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public DocumentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? entityType, [FromQuery] Guid? entityId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDocumentsQuery
        {
            EntityType = entityType, EntityId = entityId, Page = page, PageSize = pageSize
        }, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result.Items);
    }

    [HttpPost, RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(
        IFormFile file, [FromForm] string? entityType, [FromForm] Guid? entityId,
        [FromForm] string? description, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        try
        {
            var dto = await _mediator.Send(new UploadDocumentCommand
            {
                UploadedByUserId = CurrentUserId(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                Content = ms.ToArray(),
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
            }, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/download-url")]
    public async Task<IActionResult> GetDownloadUrl(Guid id, CancellationToken ct)
    {
        try
        {
            var url = await _mediator.Send(new GetDocumentDownloadUrlQuery { Id = id }, ct);
            return Ok(new { url });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new DeleteDocumentCommand { Id = id }, ct);
        return ok ? Ok(new { message = "Documento eliminado." }) : NotFound();
    }

    private Guid CurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
