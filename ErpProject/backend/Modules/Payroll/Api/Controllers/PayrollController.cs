using Erp.Application.Common.Attributes;
using Erp.Application.Common.Fiscal;
using Erp.Modules.Payroll.Application.Features.Calculation;
using Erp.Modules.Payroll.Application.Features.Concepts;
using Erp.Modules.Payroll.Application.Features.Employees;
using Erp.Modules.Payroll.Application.Features.Exports;
using Erp.Modules.Payroll.Application.Features.Pdf;
using Erp.Modules.Payroll.Application.Features.Settlements;
using Erp.Modules.Payroll.Application.Features.Templates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Payroll.Api.Controllers;

/// <summary>Nóminas: trabajadores, liquidaciones mensuales y exportes TGSS (TC1/TC2) en CSV estructurado.</summary>
[ApiController]
[Route("api/payroll")]
[Authorize]
[RequiredModule("Payroll")]
public class PayrollController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollController(IMediator mediator) => _mediator = mediator;

    // ── Employees ───────────────────────────────────────────────────────────

    [HttpGet("employees")]
    [RequirePermission(Permissions.Employee.Read)]
    public async Task<IActionResult> ListEmployees(CancellationToken ct)
        => Ok(await _mediator.Send(new GetEmployeesQuery(), ct));

    [HttpPost("employees")]
    [RequirePermission(Permissions.Employee.Create)]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeBody body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CreateEmployeeCommand(
                body.TaxId, body.FullName, body.SocialSecurityNumber,
                body.HireDate, body.ContractType, body.WeeklyHours), ct);
            return Created($"/api/payroll/employees/{result.Id}", new { result.Id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Settlements ───────────────────────────────────────────────────────────

    [HttpGet("settlements")]
    [RequirePermission(Permissions.Settlement.Read)]
    public async Task<IActionResult> ListSettlements([FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSettlementsQuery(year), ct));

    [HttpPost("settlements")]
    [RequirePermission(Permissions.Settlement.Create)]
    public async Task<IActionResult> CreateSettlement([FromBody] CreateSettlementBody body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CreateSettlementCommand(body.Year, body.Month), ct);
            return Created($"/api/payroll/settlements/{result.Id}", new { result.Id });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("settlements/{id:guid}/lines")]
    [RequirePermission(Permissions.Settlement.Manage)]
    public async Task<IActionResult> AddLine(Guid id, [FromBody] AddPayrollLineBody body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new AddPayrollLineCommand(
                id, body.EmployeeId, body.GrossSalary, body.CommonContingenciesBase,
                body.EmployeeSocialSecurity, body.EmployerSocialSecurity,
                body.IrpfBase, body.IrpfRate, body.IrpfWithheld, body.NetPay), ct);
            return Ok(new { id = result.LineId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("settlements/{id:guid}/lines")]
    [RequirePermission(Permissions.Settlement.Read)]
    public async Task<IActionResult> ListSettlementLines(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSettlementLinesQuery(id), ct));

    [HttpGet("lines/{lineId:guid}/pdf")]
    [RequirePermission(Permissions.Settlement.Read)]
    public async Task<IActionResult> DownloadLinePdf(Guid lineId, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetPayrollLinePdfQuery(lineId), ct);
            return File(result.PdfBytes, "application/pdf", result.FileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("settlements/{id:guid}/finalize")]
    [RequirePermission(Permissions.Settlement.Manage)]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new FinalizeSettlementCommand(id), ct);
            return Ok(new { message = result.Message, journalEntryId = result.JournalEntryId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── Fase 1: plantillas, cálculo automático, conceptos ───────────────────

    [HttpGet("templates")]
    [RequirePermission(Permissions.Settlement.Read)]
    public async Task<IActionResult> ListTemplates(CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollTemplatesQuery(), ct));

    [HttpPost("templates")]
    [RequirePermission(Permissions.Settlement.Manage)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateBody body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CreatePayrollTemplateCommand(
                body.Name, body.Description, body.DefaultContractType,
                body.DefaultWeeklyHours, body.EmployeeSsRatePercent,
                body.EmployerSsRatePercent, body.DefaultIrpfRatePercent, body.IsDefault), ct);
            return Created($"/api/payroll/templates/{result.Id}", new { result.Id });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("settlements/{id:guid}/calculate-line")]
    [RequirePermission(Permissions.Settlement.Manage)]
    public async Task<IActionResult> CalculateLine(Guid id, [FromBody] CalculateLineBody body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new CalculatePayrollLineCommand(
                id, body.EmployeeId, body.GrossSalary, body.TemplateId, body.IrpfRatePercentOverride), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("lines/{lineId:guid}/concepts")]
    [RequirePermission(Permissions.Settlement.Read)]
    public async Task<IActionResult> GetLineConcepts(Guid lineId, CancellationToken ct)
    {
        try
        {
            return Ok(await _mediator.Send(new GetPayrollLineConceptsQuery(lineId), ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("lines/{lineId:guid}/deductions")]
    [RequirePermission(Permissions.Settlement.Manage)]
    public async Task<IActionResult> AddDeduction(Guid lineId, [FromBody] AddDeductionBody body, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new AddPayrollDeductionCommand(
                lineId, body.Code, body.Description, body.Amount, body.Category), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("export/tc1")]
    [RequirePermission(Permissions.Settlement.Export)]
    public async Task<IActionResult> ExportTc1([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => await SendPayrollExport(new ExportTc1Query(year, month), ct);

    [HttpGet("export/tc2")]
    [RequirePermission(Permissions.Settlement.Export)]
    public async Task<IActionResult> ExportTc2([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => await SendPayrollExport(new ExportTc2Query(year, month), ct);

    [HttpGet("export/tc-red-orientativo")]
    [RequirePermission(Permissions.Settlement.Export)]
    public async Task<IActionResult> ExportTcRedOrientativo([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => await SendPayrollExport(new ExportTcRedOrientativoQuery(year, month), ct);

    [HttpGet("export/red")]
    [RequirePermission(Permissions.Settlement.Export)]
    public async Task<IActionResult> ExportRed([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => await SendPayrollExport(new ExportRedQuery(year, month), ct);

    [HttpGet("export/model-111")]
    [RequirePermission(Permissions.Settlement.Export)]
    public async Task<IActionResult> ExportModel111([FromQuery] int year, [FromQuery] int quarter, CancellationToken ct)
        => await SendPayrollExport(new ExportModel111Query(year, quarter), ct);

    [HttpGet("export/model-190")]
    [RequirePermission(Permissions.Settlement.Export)]
    public async Task<IActionResult> ExportModel190([FromQuery] int year, CancellationToken ct)
        => await SendPayrollExport(new ExportModel190Query(year), ct);

    private async Task<IActionResult> SendPayrollExport<TQuery>(TQuery query, CancellationToken ct)
        where TQuery : IRequest<PayrollFiscalExportResult>
    {
        try
        {
            var result = await _mediator.Send(query, ct);
            FiscalExportHeaders.MarkAsNonOfficial(Response.Headers, result.Disclaimer);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }

    public sealed class CreateEmployeeBody
    {
        public string TaxId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? SocialSecurityNumber { get; set; }
        public DateTime? HireDate { get; set; }
        public string? ContractType { get; set; }
        public decimal? WeeklyHours { get; set; }
    }

    public sealed class CreateSettlementBody
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public sealed class AddPayrollLineBody
    {
        public Guid EmployeeId { get; set; }
        public decimal GrossSalary { get; set; }
        public decimal CommonContingenciesBase { get; set; }
        public decimal EmployeeSocialSecurity { get; set; }
        public decimal EmployerSocialSecurity { get; set; }
        public decimal IrpfBase { get; set; }
        public decimal IrpfRate { get; set; }
        public decimal IrpfWithheld { get; set; }
        public decimal NetPay { get; set; }
    }

    public sealed class CreateTemplateBody
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? DefaultContractType { get; set; }
        public decimal? DefaultWeeklyHours { get; set; }
        public decimal? EmployeeSsRatePercent { get; set; }
        public decimal? EmployerSsRatePercent { get; set; }
        public decimal? DefaultIrpfRatePercent { get; set; }
        public bool IsDefault { get; set; }
    }

    public sealed class CalculateLineBody
    {
        public Guid EmployeeId { get; set; }
        public decimal GrossSalary { get; set; }
        public Guid? TemplateId { get; set; }
        public decimal? IrpfRatePercentOverride { get; set; }
    }

    public sealed class AddDeductionBody
    {
        public string Code { get; set; } = "";
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? Category { get; set; }
    }
}
