using Erp.Application.Common.Attributes;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Features.Expenses.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Expenses.Api.Controllers;

// [RequiredModule] is applied per-action (not at class level) because Upload is
// AllowAnonymous (public token-based upload link) and has no resolved tenant/user
// to check a module license against.
[ApiController, Route("api/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExpensesController(IMediator mediator) => _mediator = mediator;

    [HttpPost("upload/{token}"), AllowAnonymous, RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(string token, IFormFile file, [FromForm] string? comment, CancellationToken ct)
    {
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { error = "Solo JPG, PNG, WebP o PDF." });

        if (!IsValidFileSignature(file))
            return BadRequest(new { error = "El archivo no tiene un formato valido o esta corrupto." });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        try
        {
            var result = await _mediator.Send(new UploadExpenseByTokenCommand
            {
                Token = token,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileContent = ms.ToArray(),
                Comment = comment
            }, ct);
            return Ok(new { message = result.Message, uploadId = result.UploadId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("uploads"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Read)]
    public async Task<IActionResult> GetUploads(CancellationToken ct)
        => Ok(await _mediator.Send(new GetExpenseUploadsQuery(), ct));

    [HttpPost, Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Create)]
    public async Task<IActionResult> CreateManual([FromBody] CreateExpenseDocumentCommand cmd, CancellationToken ct)
    {
        try
        {
            var id = await _mediator.Send(cmd, ct);
            return Ok(new { id, message = "Gasto registrado manualmente." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet, Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Read)]
    public async Task<IActionResult> GetDocuments(CancellationToken ct)
        => Ok(await _mediator.Send(new GetExpenseDocumentsQuery(), ct));

    [HttpGet("by-supplier/{supplierId:guid}"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Read)]
    public async Task<IActionResult> GetBySupplier(Guid supplierId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSupplierExpensesQuery { SupplierId = supplierId }, ct));

    [HttpGet("{id}"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetExpenseByIdQuery { Id = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut("{id}"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Update)]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateExpenseDraftCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        try
        {
            var ok = await _mediator.Send(cmd, ct);
            return ok ? Ok(new { message = "Documento actualizado." }) : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/lines"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Manage)]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] AddExpenseLineCommand cmd, CancellationToken ct)
    {
        cmd.ExpenseDocumentId = id;
        try
        {
            var lineId = await _mediator.Send(cmd, ct);
            return Ok(new { lineId });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id}/lines/{lineId}"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Manage)]
    public async Task<IActionResult> UpdateLine(Guid id, Guid lineId, [FromBody] UpdateExpenseLineCommand cmd, CancellationToken ct)
    {
        cmd.ExpenseDocumentId = id; cmd.LineId = lineId;
        try
        {
            var ok = await _mediator.Send(cmd, ct);
            return ok ? Ok(new { message = "Linea actualizada." }) : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id}/lines/{lineId}"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Manage)]
    public async Task<IActionResult> DeleteLine(Guid id, Guid lineId, CancellationToken ct)
    {
        try
        {
            var ok = await _mediator.Send(new DeleteExpenseLineCommand { ExpenseDocumentId = id, LineId = lineId }, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/approve"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Approve)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ApproveExpenseCommand { Id = id }, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("stats"), Authorize, RequiredModule("Expenses"), RequirePermission(Permissions.Expense.Read)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
        => Ok(await _mediator.Send(new GetExpenseStatsQuery(), ct));

    private static bool IsValidFileSignature(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new BinaryReader(stream);
        var signatures = new Dictionary<string, byte[][]>
        {
            { "image/jpeg",      new[] { new byte[] { 0xFF, 0xD8, 0xFF } } },
            { "image/png",       new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
            { "application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } } },
            { "image/webp",      new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } }
        };
        if (!signatures.TryGetValue(file.ContentType, out var expectedSigs)) return false;
        var headerBytes = reader.ReadBytes(8);
        foreach (var sig in expectedSigs)
            if (headerBytes.Take(sig.Length).SequenceEqual(sig)) return true;
        return false;
    }
}
