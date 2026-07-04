using Erp.Application.Common.Attributes;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Application.Features.SupplierUploads.Commands;
using Erp.Modules.Crm.Application.Features.SupplierUploads.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Crm.Api.Controllers;

[ApiController, Route("api/[controller]"), Authorize, RequiredModule("CRM")]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    public SuppliersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Supplier.Read)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetSuppliersQuery { Search = search, Page = page, PageSize = pageSize }, ct);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Supplier.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSupplierByIdQuery { Id = id }, ct);
        if (result == null) return NotFound();

        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.Supplier.Create)]
    public async Task<IActionResult> Create([FromBody] CreateSupplierCommand cmd, CancellationToken ct)
    {
        var result = await _mediator.Send(cmd, ct);
        return Created("", result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Supplier.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierCommand cmd, CancellationToken ct)
    {
        cmd.Id = id;
        try
        {
            var result = await _mediator.Send(cmd, ct);
            return result == null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>POST /api/suppliers/{id}/anonymize — RGPD Art. 17 derecho de supresión.</summary>
    [HttpPost("{id:guid}/anonymize")]
    [RequirePermission(Permissions.Supplier.Anonymize)]
    public async Task<IActionResult> Anonymize(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new AnonymizeSupplierCommand { Id = id }, ct);
        return result ? Ok(new { message = "Datos personales anonimizados correctamente." }) : NotFound();
    }

    /// <summary>GET /api/suppliers/uploads — listado de facturas subidas por proveedores vía enlace público (ADR-0018 #39).</summary>
    [HttpGet("uploads")]
    [RequirePermission(Permissions.Supplier.Read)]
    public async Task<IActionResult> GetUploads(CancellationToken ct)
        => Ok(await _mediator.Send(new GetSupplierInvoiceUploadsQuery(), ct));

    [HttpGet("uploads/{id:guid}/download-url")]
    [RequirePermission(Permissions.Supplier.Read)]
    public async Task<IActionResult> GetUploadDownloadUrl(Guid id, CancellationToken ct)
    {
        try
        {
            var url = await _mediator.Send(new GetSupplierInvoiceUploadDownloadUrlQuery { Id = id }, ct);
            return Ok(new { url });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("uploads/{id:guid}/mark-reviewed")]
    [RequirePermission(Permissions.Supplier.Update)]
    public async Task<IActionResult> MarkUploadReviewed(Guid id, CancellationToken ct)
    {
        var ok = await _mediator.Send(new MarkSupplierInvoiceUploadReviewedCommand { Id = id }, ct);
        return ok ? Ok(new { message = "Factura marcada como revisada." }) : NotFound();
    }
}
