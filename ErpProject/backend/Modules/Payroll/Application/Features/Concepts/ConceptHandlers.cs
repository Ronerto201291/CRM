using Erp.Modules.Payroll.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Application.Features.Concepts;

public record PayrollDeductionDto(Guid Id, Guid PayrollLineId, string Code, string Description, decimal Amount, string Category);
public record SocialSecurityContributionDto(
    Guid Id, Guid PayrollLineId, string ContingencyType, decimal BaseAmount,
    decimal EmployeeRatePercent, decimal EmployerRatePercent,
    decimal EmployeeAmount, decimal EmployerAmount);
public record TaxableBaseDto(
    Guid Id, Guid PayrollLineId, string BaseType, decimal Amount,
    decimal IrpfRatePercent, decimal IrpfWithheld);

public record GetPayrollLineConceptsQuery(Guid LineId) : IRequest<PayrollLineConceptsResult>;

public record PayrollLineConceptsResult(
    IReadOnlyList<PayrollDeductionDto> Deductions,
    IReadOnlyList<SocialSecurityContributionDto> SocialSecurityContributions,
    IReadOnlyList<TaxableBaseDto> TaxableBases);

public record AddPayrollDeductionCommand(
    Guid LineId, string Code, string Description, decimal Amount, string? Category)
    : IRequest<PayrollDeductionDto>;

public class GetPayrollLineConceptsHandler : IRequestHandler<GetPayrollLineConceptsQuery, PayrollLineConceptsResult>
{
    private readonly IPayrollDbContext _ctx;

    public GetPayrollLineConceptsHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<PayrollLineConceptsResult> Handle(GetPayrollLineConceptsQuery request, CancellationToken ct)
    {
        var line = await _ctx.PayrollLines.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LineId, ct)
            ?? throw new KeyNotFoundException("Line not found");

        var deductions = await _ctx.PayrollDeductions.AsNoTracking()
            .Where(d => d.PayrollLineId == line.Id)
            .Select(d => new PayrollDeductionDto(d.Id, d.PayrollLineId, d.Code, d.Description, d.Amount, d.Category))
            .ToListAsync(ct);

        var ss = await _ctx.SocialSecurityContributions.AsNoTracking()
            .Where(c => c.PayrollLineId == line.Id)
            .Select(c => new SocialSecurityContributionDto(
                c.Id, c.PayrollLineId, c.ContingencyType, c.BaseAmount,
                c.EmployeeRatePercent, c.EmployerRatePercent, c.EmployeeAmount, c.EmployerAmount))
            .ToListAsync(ct);

        var bases = await _ctx.TaxableBases.AsNoTracking()
            .Where(b => b.PayrollLineId == line.Id)
            .Select(b => new TaxableBaseDto(b.Id, b.PayrollLineId, b.BaseType, b.Amount, b.IrpfRatePercent, b.IrpfWithheld))
            .ToListAsync(ct);

        return new PayrollLineConceptsResult(deductions, ss, bases);
    }
}

public class AddPayrollDeductionHandler : IRequestHandler<AddPayrollDeductionCommand, PayrollDeductionDto>
{
    private readonly IPayrollDbContext _ctx;

    public AddPayrollDeductionHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<PayrollDeductionDto> Handle(AddPayrollDeductionCommand request, CancellationToken ct)
    {
        var line = await _ctx.PayrollLines
            .Include(l => l.Settlement)
            .FirstOrDefaultAsync(l => l.Id == request.LineId, ct)
            ?? throw new KeyNotFoundException("Line not found");

        if (line.Settlement?.Status != "Draft")
            throw new InvalidOperationException("Solo en liquidación borrador.");

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Código obligatorio.");

        var d = new Domain.Entities.PayrollDeduction
        {
            Id = Guid.NewGuid(),
            PayrollLineId = line.Id,
            Code = request.Code.Trim(),
            Description = request.Description?.Trim() ?? request.Code.Trim(),
            Amount = request.Amount,
            Category = request.Category?.Trim() ?? "Deduccion",
        };

        _ctx.PayrollDeductions.Add(d);
        await _ctx.SaveChangesAsync(ct);

        return new PayrollDeductionDto(d.Id, d.PayrollLineId, d.Code, d.Description, d.Amount, d.Category);
    }
}
