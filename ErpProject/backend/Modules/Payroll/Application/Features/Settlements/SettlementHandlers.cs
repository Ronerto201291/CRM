using Erp.Application.Common.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Application.Features.Settlements;

public record SettlementListItemDto(
    Guid Id, int Year, int Month, string Status, Guid? JournalEntryId,
    int LineCount, decimal TotalGross, decimal TotalIrpf, decimal TotalEmployerSs);

public record GetSettlementsQuery(int? Year) : IRequest<IReadOnlyList<SettlementListItemDto>>;

public record CreateSettlementCommand(int Year, int Month) : IRequest<CreateSettlementResult>;

public record CreateSettlementResult(Guid Id);

public record AddPayrollLineCommand(
    Guid SettlementId, Guid EmployeeId, decimal GrossSalary,
    decimal CommonContingenciesBase, decimal EmployeeSocialSecurity,
    decimal EmployerSocialSecurity, decimal IrpfBase, decimal IrpfRate,
    decimal IrpfWithheld, decimal NetPay) : IRequest<AddPayrollLineResult>;

public record AddPayrollLineResult(Guid LineId);

public record FinalizeSettlementCommand(Guid SettlementId) : IRequest<FinalizeSettlementResult>;

public record FinalizeSettlementResult(string Message, Guid JournalEntryId, bool AlreadyFinalized);

public record SettlementLineListItemDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string TaxId,
    decimal GrossSalary,
    decimal IrpfWithheld,
    decimal NetPay);

public record GetSettlementLinesQuery(Guid SettlementId) : IRequest<IReadOnlyList<SettlementLineListItemDto>>;

public class GetSettlementsHandler : IRequestHandler<GetSettlementsQuery, IReadOnlyList<SettlementListItemDto>>
{
    private readonly IPayrollDbContext _ctx;

    public GetSettlementsHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<SettlementListItemDto>> Handle(GetSettlementsQuery request, CancellationToken ct)
    {
        var y = request.Year ?? DateTime.UtcNow.Year;
        return await _ctx.PayrollSettlements
            .AsNoTracking()
            .Where(s => s.Year == y)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .Select(s => new SettlementListItemDto(
                s.Id, s.Year, s.Month, s.Status, s.JournalEntryId,
                s.Lines.Count,
                s.Lines.Sum(l => l.GrossSalary),
                s.Lines.Sum(l => l.IrpfWithheld),
                s.Lines.Sum(l => l.EmployerSocialSecurity)))
            .ToListAsync(ct);
    }
}

public class CreateSettlementHandler : IRequestHandler<CreateSettlementCommand, CreateSettlementResult>
{
    private readonly IPayrollDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateSettlementHandler(IPayrollDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CreateSettlementResult> Handle(CreateSettlementCommand request, CancellationToken ct)
    {
        if (request.Month is < 1 or > 12)
            throw new ArgumentException("Month debe ser 1–12.");

        var exists = await _ctx.PayrollSettlements
            .AnyAsync(s => s.Year == request.Year && s.Month == request.Month, ct);
        if (exists)
            throw new InvalidOperationException("Ya existe liquidación para ese mes.");

        var s = new PayrollSettlement
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved"),
            Year = request.Year,
            Month = request.Month,
            Status = "Draft",
        };

        _ctx.PayrollSettlements.Add(s);
        await _ctx.SaveChangesAsync(ct);
        return new CreateSettlementResult(s.Id);
    }
}

public class AddPayrollLineHandler : IRequestHandler<AddPayrollLineCommand, AddPayrollLineResult>
{
    private readonly IPayrollDbContext _ctx;

    public AddPayrollLineHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<AddPayrollLineResult> Handle(AddPayrollLineCommand request, CancellationToken ct)
    {
        var settlement = await _ctx.PayrollSettlements
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == request.SettlementId, ct)
            ?? throw new KeyNotFoundException("Settlement not found");

        if (settlement.Status != "Draft")
            throw new InvalidOperationException("Solo se pueden añadir líneas en borrador.");

        var emp = await _ctx.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, ct);
        if (emp == null)
            throw new ArgumentException("Empleado no encontrado.");

        if (settlement.Lines.Any(l => l.EmployeeId == request.EmployeeId))
            throw new InvalidOperationException("El empleado ya tiene línea en esta liquidación.");

        var line = new PayrollLine
        {
            Id = Guid.NewGuid(),
            PayrollSettlementId = settlement.Id,
            EmployeeId = request.EmployeeId,
            GrossSalary = request.GrossSalary,
            CommonContingenciesBase = request.CommonContingenciesBase,
            EmployeeSocialSecurity = request.EmployeeSocialSecurity,
            EmployerSocialSecurity = request.EmployerSocialSecurity,
            IrpfBase = request.IrpfBase,
            IrpfRate = request.IrpfRate,
            IrpfWithheld = request.IrpfWithheld,
            NetPay = request.NetPay,
        };

        _ctx.PayrollLines.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return new AddPayrollLineResult(line.Id);
    }
}

public class FinalizeSettlementHandler : IRequestHandler<FinalizeSettlementCommand, FinalizeSettlementResult>
{
    private readonly IPayrollDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IPayrollJournalEntryGenerator _journal;

    public FinalizeSettlementHandler(
        IPayrollDbContext ctx, ITenantContext tenant, IPayrollJournalEntryGenerator journal)
    {
        _ctx = ctx;
        _tenant = tenant;
        _journal = journal;
    }

    public async Task<FinalizeSettlementResult> Handle(FinalizeSettlementCommand request, CancellationToken ct)
    {
        var settlement = await _ctx.PayrollSettlements
            .Include(s => s.Lines)
            .FirstOrDefaultAsync(s => s.Id == request.SettlementId, ct)
            ?? throw new KeyNotFoundException("Settlement not found");

        if (settlement.Status == "Final" && settlement.JournalEntryId != null)
            return new FinalizeSettlementResult(
                "Liquidación ya cerrada.", settlement.JournalEntryId.Value, AlreadyFinalized: true);

        if (!settlement.Lines.Any())
            throw new InvalidOperationException("Añada al menos una línea antes de finalizar.");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var gross = settlement.Lines.Sum(l => l.GrossSalary);
        var emprSs = settlement.Lines.Sum(l => l.EmployerSocialSecurity);
        var empSs = settlement.Lines.Sum(l => l.EmployeeSocialSecurity);
        var irpf = settlement.Lines.Sum(l => l.IrpfWithheld);
        var net = settlement.Lines.Sum(l => l.NetPay);
        var accrual = new DateTime(settlement.Year, settlement.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var entryId = await _journal.GenerateFromPayrollSettlementAsync(
            tenantId, settlement.Id, settlement.Year, settlement.Month,
            gross, emprSs, empSs, irpf, net, accrual, ct);

        if (settlement.Status == "Draft")
            settlement.Status = "Final";
        settlement.JournalEntryId = entryId;
        await _ctx.SaveChangesAsync(ct);

        return new FinalizeSettlementResult(
            "Liquidación cerrada y asiento contable generado.", entryId, AlreadyFinalized: false);
    }
}

public class GetSettlementLinesHandler : IRequestHandler<GetSettlementLinesQuery, IReadOnlyList<SettlementLineListItemDto>>
{
    private readonly IPayrollDbContext _ctx;

    public GetSettlementLinesHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<SettlementLineListItemDto>> Handle(GetSettlementLinesQuery request, CancellationToken ct)
    {
        return await _ctx.PayrollLines
            .Include(l => l.Employee)
            .Where(l => l.PayrollSettlementId == request.SettlementId)
            .AsNoTracking()
            .OrderBy(l => l.Employee!.FullName)
            .Select(l => new SettlementLineListItemDto(
                l.Id,
                l.EmployeeId,
                l.Employee!.FullName,
                l.Employee.TaxId,
                l.GrossSalary,
                l.IrpfWithheld,
                l.NetPay))
            .ToListAsync(ct);
    }
}
