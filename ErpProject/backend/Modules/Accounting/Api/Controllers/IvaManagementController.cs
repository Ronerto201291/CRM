using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Features.IvaManagement;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/iva")]
[Authorize]
[RequiredModule("Accounting")]
public class IvaManagementController : ControllerBase
{
    private readonly IMediator _mediator;

    public IvaManagementController(IMediator mediator) => _mediator = mediator;

    [HttpGet("registro")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetIvaRegister(CancellationToken ct)
    {
        var summary = await _mediator.Send(new GetIvaRegisterSummaryQuery(), ct);
        return Ok(new
        {
            message = "Libros Registro IVA",
            totalRecords = summary.TotalRecords,
            purchaseVat = summary.PurchaseVat,
            salesVat = summary.SalesVat,
            purchaseRecords = summary.PurchaseRecords,
            salesRecords = summary.SalesRecords,
            intraEU = summary.IntraEuCount
        });
    }

    [HttpGet("registro/purchase")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetPurchaseRegister(CancellationToken ct)
    {
        var detail = await _mediator.Send(new GetPurchaseIvaRegisterQuery(), ct);
        return Ok(new
        {
            type = detail.Type,
            records = detail.Records,
            totalVat = detail.TotalVat,
            lastExport = detail.LastExport
        });
    }

    [HttpGet("registro/sales")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetSalesRegister(CancellationToken ct)
    {
        var detail = await _mediator.Send(new GetSalesIvaRegisterQuery(), ct);
        return Ok(new
        {
            type = detail.Type,
            records = detail.Records,
            totalVat = detail.TotalVat,
            intraEU = detail.IntraEu
        });
    }

    [HttpGet("registro/purchase/lines")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetPurchaseLines([FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetPurchaseIvaLinesQuery(limit), ct));

    [HttpGet("registro/sales/lines")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetSalesLines([FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetSalesIvaLinesQuery(limit), ct));

    [HttpPost("registro/export-riva")]
    [RequirePermission(Permissions.Accounting.Export)]
    public async Task<IActionResult> ExportRiva([FromBody] ExportRivaRequest? dto, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportRivaCommand(dto?.Year, dto?.Month), ct);
        return Ok(new
        {
            fileName = result.FileName,
            format = result.Format,
            totalRecords = result.TotalRecords,
            totalVat = result.TotalVat,
            message = result.Message,
            status = result.Status,
            id = result.Id
        });
    }

    [HttpPost("sii")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> CreateSiiDeclaration([FromBody] CreateSiiRequest dto, CancellationToken ct)
    {
        var year = dto.Year > 0 ? dto.Year : DateTime.UtcNow.Year;
        var month = dto.Month is >= 1 and <= 12 ? dto.Month : DateTime.UtcNow.Month;
        var result = await _mediator.Send(new CreateSiiDeclarationCommand(year, month), ct);
        return Created(string.Empty, new
        {
            id = result.Id,
            status = result.Status,
            totalVatOutput = result.TotalVatOutput,
            totalVatInput = result.TotalVatInput,
            netVat = result.NetVat,
            message = result.Message
        });
    }

    [HttpPost("sii/{id:guid}/submit")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> SubmitSii(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitSiiDeclarationCommand(id), ct);
        return Ok(new
        {
            id = result.Id,
            status = result.Status,
            siiReference = $"SII{result.Id:N}".Substring(0, 16),
            submissionDate = DateTime.UtcNow,
            message = result.Message
        });
    }

    [HttpGet("intra-eu")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetIntraEUOperations(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIntraEuOperationsQuery(), ct);
        return Ok(new
        {
            totalOperations = result.TotalOperations,
            totalAmount = result.TotalAmount,
            countries = result.Countries,
            triangularOperations = result.TriangularOperations,
            reverseChargeApplied = result.ReverseChargeApplied
        });
    }
}

public class ExportRivaRequest
{
    public int? Year { get; set; }
    public int? Month { get; set; }
}

public class CreateSiiRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
}
