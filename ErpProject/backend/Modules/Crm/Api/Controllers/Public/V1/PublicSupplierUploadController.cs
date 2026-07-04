using Asp.Versioning;
using Erp.Modules.Crm.Application.Features.SupplierUploads.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers.Public.V1;

/// <summary>
/// Portal público del proveedor para subir su factura (ADR-0018 #39, mismo patrón que
/// ExpensesController.Upload). No requiere autenticación — acceso por PublicUploadToken
/// único del proveedor (Supplier.PublicUploadToken), activable/desactivable por el staff.
/// Route: /api/v1/public/supplier-uploads/{token}
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/public/supplier-uploads")]
[AllowAnonymous]
public class PublicSupplierUploadController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicSupplierUploadController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{token}"), RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Upload(string token, IFormFile file, [FromForm] string? comment, CancellationToken ct)
    {
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { error = "Solo JPG, PNG, WebP o PDF." });

        if (!IsValidFileSignature(file))
            return BadRequest(new { error = "El archivo no tiene un formato válido o está corrupto." });

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        try
        {
            var result = await _mediator.Send(new UploadSupplierInvoiceByTokenCommand
            {
                Token = token,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileContent = ms.ToArray(),
                Comment = comment,
            }, ct);
            return Ok(new { message = result.Message, uploadId = result.UploadId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

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
