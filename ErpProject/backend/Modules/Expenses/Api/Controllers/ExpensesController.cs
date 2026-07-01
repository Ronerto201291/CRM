using Erp.Application.Common.Interfaces;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Features.Expenses.Queries;
using Erp.Modules.Expenses.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Erp.Modules.Expenses.Api.Controllers;

[ApiController, Route("api/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExpensesDbContext _expenses;
    private readonly IApplicationDbContext _app;
    private readonly ICrmDbContext _crmCtx;
    private readonly IFileStorageService _storage;
    private readonly string _bucket;

    public ExpensesController(
        IMediator mediator,
        IExpensesDbContext expenses,
        IApplicationDbContext app,
        ICrmDbContext crmCtx,
        IFileStorageService storage,
        IConfiguration config)
    {
        _mediator = mediator;
        _expenses = expenses;
        _app = app;
        _crmCtx = crmCtx;
        _storage = storage;
        _bucket = config["Storage:BucketName"] ?? "erp-expenses";
    }

    // Upload — almacena en MinIO (RL-4: cifrado en reposo, sin disco local)
    [HttpPost("upload/{token}"), AllowAnonymous, RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(string token, IFormFile file, [FromForm] string? comment, CancellationToken ct)
    {
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { error = "Solo JPG, PNG, WebP o PDF." });

        if (!IsValidFileSignature(file))
            return BadRequest(new { error = "El archivo no tiene un formato valido o esta corrupto." });

        var company = await _app.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.PublicUploadToken == token && c.QrUploadEnabled, ct);
        if (company == null) return NotFound(new { error = "Token invalido o desactivado." });

        // Clave de objeto: {companyId}/{guid}{extension}
        var objectKey = $"{company.Id}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        await using var stream = file.OpenReadStream();
        await _storage.UploadAsync(_bucket, objectKey, stream, file.ContentType, ct);

        var upload = new ExpenseUpload
        {
            Id = Guid.NewGuid(), CompanyId = company.Id,
            PublicTokenUsed = token, FileName = file.FileName,
            FilePath = objectKey, // ahora es object key de MinIO, no ruta local
            ContentType = file.ContentType,
            Comment = comment, Status = "Pending"
        };
        _expenses.ExpenseUploads.Add(upload);
        _crmCtx.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = company.Id,
            EntityType = "ExpenseUpload", EntityId = upload.Id,
            Action = "Uploaded", Description = $"Documento subido via QR: {file.FileName}"
        });
        await _expenses.SaveChangesAsync(ct);
        return Ok(new { message = "Documento recibido. Sera procesado automaticamente.", uploadId = upload.Id });
    }

    [HttpGet("uploads"), Authorize]
    public async Task<IActionResult> GetUploads(CancellationToken ct)
        => Ok(await _mediator.Send(new GetExpenseUploadsQuery(), ct));

    [HttpPost, Authorize]
    public async Task<IActionResult> CreateManual([FromBody] CreateExpenseDocumentCommand cmd, CancellationToken ct)
    {
        try
        {
            var id = await _mediator.Send(cmd, ct);
            return Ok(new { id, message = "Gasto registrado manualmente." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet, Authorize]
    public async Task<IActionResult> GetDocuments(CancellationToken ct)
        => Ok(await _mediator.Send(new GetExpenseDocumentsQuery(), ct));

    [HttpGet("{id}"), Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetExpenseByIdQuery { Id = id }, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut("{id}"), Authorize]
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

    [HttpPost("{id}/lines"), Authorize]
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

    [HttpPut("{id}/lines/{lineId}"), Authorize]
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

    [HttpDelete("{id}/lines/{lineId}"), Authorize]
    public async Task<IActionResult> DeleteLine(Guid id, Guid lineId, CancellationToken ct)
    {
        try
        {
            var ok = await _mediator.Send(new DeleteExpenseLineCommand { ExpenseDocumentId = id, LineId = lineId }, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id}/approve"), Authorize]
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

    [HttpGet("stats"), Authorize]
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
