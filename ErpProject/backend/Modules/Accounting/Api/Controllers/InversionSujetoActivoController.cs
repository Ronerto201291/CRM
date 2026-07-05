using Erp.Application.Common.Attributes;
using Erp.Modules.Accounting.Application.Features.Isp;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

[ApiController]
[Route("api/v1/accounting/isp")]
[Authorize]
[RequiredModule("Accounting")]
public class InversionSujetoActivoController : ControllerBase
{
    private readonly IMediator _mediator;

    public InversionSujetoActivoController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _mediator.Send(new GetIspOperationsQuery(), ct));

    [HttpPost]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateIspRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateIspOperationCommand(
            request.SupplierCountryCode,
            request.VatableBase,
            request.VatRate), ct);

        return Created(string.Empty, new
        {
            id = result.Id,
            supplierCountryCode = result.SupplierCountryCode,
            vatableBase = result.VatableBase,
            vatRate = result.VatRate,
            vatAmount = result.VatAmount,
            isReverseCharge = result.IsReverseCharge,
            status = result.Status,
            message = "Inversión del sujeto pasivo registrada"
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Vat.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIspOperationQuery(id), ct);
        if (result is null) return NotFound();

        return Ok(new
        {
            id = result.Id,
            supplierCountryCode = result.SupplierCountryCode,
            vatableBase = result.VatableBase,
            vatRate = result.VatRate,
            vatAmount = result.VatAmount,
            isReverseCharge = result.IsReverseCharge,
            status = result.Status,
            regulation = "Art. 84.1 RD 1619/2012"
        });
    }

    [HttpPost("{id:guid}/calculate-vat")]
    [RequirePermission(Permissions.Vat.Manage)]
    public async Task<IActionResult> CalculateVAT(Guid id, [FromBody] CalculateIspVatRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CalculateIspVatCommand(id, request.Amount), ct);
            return Ok(new
            {
                id = result.Id,
                invoiceVat = result.InvoiceVat,
                ispApplied = result.IspApplied,
                effectiveVat = result.EffectiveVat,
                message = result.Message,
                status = result.Status
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

public class CreateIspRequest
{
    public string SupplierCountryCode { get; set; } = string.Empty;
    public decimal VatableBase { get; set; }
    public decimal VatRate { get; set; }
}

public class CalculateIspVatRequest
{
    public decimal Amount { get; set; }
}
