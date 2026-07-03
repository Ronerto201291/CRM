using Erp.Infrastructure.Fiscal;
using Erp.Modules.Payroll.Application.Features.Employees;
using Erp.Modules.Payroll.Application.Features.Exports;
using Erp.Modules.Payroll.Application.Features.Settlements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Payroll.Api.Controllers;

/// <summary>Nóminas: trabajadores, liquidaciones mensuales y exportes TGSS (TC1/TC2) en CSV estructurado.</summary>
[ApiController]
[Route("api/payroll")]
[Authorize]
public class PayrollController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollController(IMediator mediator) => _mediator = mediator;

    // ── Employees ───────────────────────────────────────────────────────────

    [HttpGet("employees")]
    public async Task<IActionResult> ListEmployees(CancellationToken ct)
        => Ok(await _mediator.Send(new GetEmployeesQuery(), ct));

    [HttpPost("employees")]
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
    public async Task<IActionResult> ListSettlements([FromQuery] int? year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSettlementsQuery(year), ct));

    [HttpPost("settlements")]
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

    [HttpPost("settlements/{id:guid}/finalize")]
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

    [HttpGet("export/tc1")]
    public async Task<IActionResult> ExportTc1([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => await SendPayrollExport(new ExportTc1Query(year, month), ct);

    [HttpGet("export/tc2")]
    public async Task<IActionResult> ExportTc2([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => await SendPayrollExport(new ExportTc2Query(year, month), ct);

    [HttpGet("export/tc-red-orientativo")]
    public async Task<IActionResult> ExportTcRedOrientativo([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => await SendPayrollExport(new ExportTcRedOrientativoQuery(year, month), ct);

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
}
