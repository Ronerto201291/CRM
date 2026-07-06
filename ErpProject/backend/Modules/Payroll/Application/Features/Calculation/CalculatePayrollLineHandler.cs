using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Application.Services;
using Erp.Modules.Payroll.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Application.Features.Calculation;

public record CalculatePayrollLineCommand(
    Guid SettlementId, Guid EmployeeId, decimal GrossSalary,
    Guid? TemplateId, decimal? IrpfRatePercentOverride) : IRequest<CalculatePayrollLineResult>;

public record CalculatePayrollLineResult(
    Guid LineId,
    decimal GrossSalary,
    decimal CommonContingenciesBase,
    decimal EmployeeSocialSecurity,
    decimal EmployerSocialSecurity,
    decimal IrpfBase,
    decimal IrpfRate,
    decimal IrpfWithheld,
    decimal NetPay,
    string CalculationNote);

public class CalculatePayrollLineHandler : IRequestHandler<CalculatePayrollLineCommand, CalculatePayrollLineResult>
{
    private readonly IPayrollDbContext _ctx;

    public CalculatePayrollLineHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<CalculatePayrollLineResult> Handle(CalculatePayrollLineCommand request, CancellationToken ct)
    {
        var settlement = await _ctx.PayrollSettlements
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == request.SettlementId, ct)
            ?? throw new KeyNotFoundException("Settlement not found");

        if (settlement.Status != "Draft")
            throw new InvalidOperationException("Solo se pueden calcular líneas en borrador.");

        var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct)
            ?? throw new ArgumentException("Empleado no encontrado.");

        if (settlement.Lines.Any(l => l.EmployeeId == request.EmployeeId))
            throw new InvalidOperationException("El empleado ya tiene línea en esta liquidación.");

        PayrollTemplate? template = null;
        if (request.TemplateId != null)
            template = await _ctx.PayrollTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct);

        if (template == null)
            template = await _ctx.PayrollTemplates.FirstOrDefaultAsync(t => t.IsDefault && t.IsActive, ct);

        var empSsRate = template?.EmployeeSsRatePercent ?? 6.35m;
        var erSsRate = template?.EmployerSsRatePercent ?? 30m;
        var irpfRate = request.IrpfRatePercentOverride ?? template?.DefaultIrpfRatePercent ?? 15m;

        var calc = PayrollCalculationService.Calculate(new PayrollCalculationService.Input(
            request.GrossSalary, empSsRate, erSsRate, irpfRate));

        var line = new PayrollLine
        {
            Id = Guid.NewGuid(),
            PayrollSettlementId = settlement.Id,
            EmployeeId = emp.Id,
            GrossSalary = request.GrossSalary,
            CommonContingenciesBase = calc.CommonContingenciesBase,
            EmployeeSocialSecurity = calc.EmployeeSocialSecurity,
            EmployerSocialSecurity = calc.EmployerSocialSecurity,
            IrpfBase = calc.IrpfBase,
            IrpfRate = calc.IrpfRate,
            IrpfWithheld = calc.IrpfWithheld,
            NetPay = calc.NetPay,
        };

        _ctx.PayrollLines.Add(line);

        _ctx.SocialSecurityContributions.Add(new SocialSecurityContribution
        {
            Id = Guid.NewGuid(),
            CompanyId = settlement.CompanyId,
            PayrollLineId = line.Id,
            ContingencyType = "CC",
            BaseAmount = calc.CommonContingenciesBase,
            EmployeeRatePercent = empSsRate,
            EmployerRatePercent = erSsRate,
            EmployeeAmount = calc.EmployeeSocialSecurity,
            EmployerAmount = calc.EmployerSocialSecurity,
        });

        _ctx.TaxableBases.Add(new TaxableBase
        {
            Id = Guid.NewGuid(),
            CompanyId = settlement.CompanyId,
            PayrollLineId = line.Id,
            BaseType = "General",
            Amount = calc.IrpfBase,
            IrpfRatePercent = calc.IrpfRate,
            IrpfWithheld = calc.IrpfWithheld,
        });

        await _ctx.SaveChangesAsync(ct);

        var note = template != null
            ? $"Calculado con plantilla «{template.Name}» (SS {empSsRate}%/{erSsRate}%, IRPF {irpfRate}%). Orientativo."
            : $"Calculado con tablas por defecto (SS {empSsRate}%/{erSsRate}%, IRPF {irpfRate}%). Orientativo.";

        return new CalculatePayrollLineResult(
            line.Id, request.GrossSalary,
            calc.CommonContingenciesBase, calc.EmployeeSocialSecurity, calc.EmployerSocialSecurity,
            calc.IrpfBase, calc.IrpfRate, calc.IrpfWithheld, calc.NetPay, note);
    }
}
