using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Handlers;

// ── CREATE BUDGET ─────────────────────────────────────────────────────────────
public class CreateBudgetHandler : IRequestHandler<CreateBudgetCommand, Guid>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateBudgetHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateBudgetCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        var budget = new Budget
        {
            Id         = Guid.NewGuid(),
            CompanyId  = companyId,
            Name       = req.Name,
            FiscalYear = req.FiscalYear,
            StartDate  = req.StartDate,
            EndDate    = req.EndDate,
            Status     = "Draft",
            CreatedAt  = DateTime.UtcNow
        };

        _ctx.Budgets.Add(budget);
        await _ctx.SaveChangesAsync(ct);
        return budget.Id;
    }
}

// ── APPROVE ───────────────────────────────────────────────────────────────────
public class ApproveBudgetHandler : IRequestHandler<ApproveBudgetCommand>
{
    private readonly IAccountingDbContext _ctx;

    public ApproveBudgetHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(ApproveBudgetCommand req, CancellationToken ct)
    {
        var budget = await _ctx.Budgets.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Presupuesto {req.Id} no encontrado.");

        if (budget.Status != "Draft")
            throw new InvalidOperationException($"Solo se pueden aprobar presupuestos en estado Draft. Estado actual: {budget.Status}.");

        budget.Status    = "Approved";
        budget.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }
}

// ── CLOSE ─────────────────────────────────────────────────────────────────────
public class CloseBudgetHandler : IRequestHandler<CloseBudgetCommand>
{
    private readonly IAccountingDbContext _ctx;

    public CloseBudgetHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(CloseBudgetCommand req, CancellationToken ct)
    {
        var budget = await _ctx.Budgets.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Presupuesto {req.Id} no encontrado.");

        if (budget.Status == "Closed")
            throw new InvalidOperationException("El presupuesto ya está cerrado.");

        budget.Status    = "Closed";
        budget.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }
}

// ── ADD LINE ──────────────────────────────────────────────────────────────────
public class AddBudgetLineHandler : IRequestHandler<AddBudgetLineCommand, Guid>
{
    private readonly IAccountingDbContext _ctx;

    public AddBudgetLineHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<Guid> Handle(AddBudgetLineCommand req, CancellationToken ct)
    {
        var budget = await _ctx.Budgets.FindAsync([req.BudgetId], ct)
            ?? throw new KeyNotFoundException($"Presupuesto {req.BudgetId} no encontrado.");

        if (budget.Status == "Closed")
            throw new InvalidOperationException("No se pueden añadir líneas a un presupuesto cerrado.");

        var line = new BudgetLine
        {
            Id             = Guid.NewGuid(),
            BudgetId       = req.BudgetId,
            AccountId      = req.AccountId,
            CostCenterId   = req.CostCenterId,
            Type           = req.Type,
            BudgetedAmount = req.BudgetedAmount,
            ActualAmount   = 0,
            CreatedAt      = DateTime.UtcNow
        };

        _ctx.BudgetLines.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return line.Id;
    }
}

// ── UPDATE LINE ───────────────────────────────────────────────────────────────
public class UpdateBudgetLineHandler : IRequestHandler<UpdateBudgetLineCommand>
{
    private readonly IAccountingDbContext _ctx;

    public UpdateBudgetLineHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(UpdateBudgetLineCommand req, CancellationToken ct)
    {
        var line = await _ctx.BudgetLines.FindAsync([req.LineId], ct)
            ?? throw new KeyNotFoundException($"Línea {req.LineId} no encontrada.");

        line.BudgetedAmount = req.BudgetedAmount;
        line.UpdatedAt      = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }
}

// ── DELETE LINE ───────────────────────────────────────────────────────────────
public class DeleteBudgetLineHandler : IRequestHandler<DeleteBudgetLineCommand>
{
    private readonly IAccountingDbContext _ctx;

    public DeleteBudgetLineHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(DeleteBudgetLineCommand req, CancellationToken ct)
    {
        var line = await _ctx.BudgetLines.FindAsync([req.LineId], ct)
            ?? throw new KeyNotFoundException($"Línea {req.LineId} no encontrada.");

        _ctx.BudgetLines.Remove(line);
        await _ctx.SaveChangesAsync(ct);
    }
}

// ── QUERIES ───────────────────────────────────────────────────────────────────
public class GetBudgetsHandler : IRequestHandler<GetBudgetsQuery, List<BudgetSummaryDto>>
{
    private readonly IAccountingDbContext _ctx;

    public GetBudgetsHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<BudgetSummaryDto>> Handle(GetBudgetsQuery req, CancellationToken ct)
    {
        var q = _ctx.Budgets.Include(b => b.Lines).AsNoTracking().AsQueryable();
        if (req.FiscalYear.HasValue) q = q.Where(b => b.FiscalYear == req.FiscalYear.Value);

        return await q.OrderByDescending(b => b.FiscalYear).ThenBy(b => b.Name)
            .Select(b => new BudgetSummaryDto(
                b.Id, b.Name, b.FiscalYear, b.StartDate, b.EndDate, b.Status,
                b.Lines.Count,
                b.Lines.Sum(l => l.BudgetedAmount),
                b.CreatedAt))
            .ToListAsync(ct);
    }
}

public class GetBudgetHandler : IRequestHandler<GetBudgetQuery, BudgetDetailDto?>
{
    private readonly IAccountingDbContext _ctx;

    public GetBudgetHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<BudgetDetailDto?> Handle(GetBudgetQuery req, CancellationToken ct)
    {
        var b = await _ctx.Budgets.Include(x => x.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);

        if (b is null) return null;

        return new BudgetDetailDto(
            b.Id, b.Name, b.FiscalYear, b.StartDate, b.EndDate, b.Status,
            b.Lines.Select(l => new BudgetLineDto(
                l.Id, l.BudgetId, l.AccountId, null, l.CostCenterId, l.Type, l.BudgetedAmount
            )).ToList()
        );
    }
}

/// <summary>
/// Compara presupuesto vs real consultando los JournalEntryLines del ejercicio fiscal.
/// La imputación real se obtiene sumando débitos/créditos de las cuentas asociadas
/// a cada línea presupuestaria durante el rango de fechas del presupuesto.
/// </summary>
public class GetBudgetAnalysisHandler : IRequestHandler<GetBudgetAnalysisQuery, List<BudgetLineAnalysisDto>>
{
    private readonly IAccountingDbContext _ctx;

    public GetBudgetAnalysisHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<BudgetLineAnalysisDto>> Handle(GetBudgetAnalysisQuery req, CancellationToken ct)
    {
        var budget = await _ctx.Budgets.Include(b => b.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == req.BudgetId, ct)
            ?? throw new KeyNotFoundException($"Presupuesto {req.BudgetId} no encontrado.");

        // Cargar todos los asientos del período del presupuesto (posted = contabilizados)
        var journalLines = await _ctx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted
                     && l.JournalEntry.Date >= budget.StartDate
                     && l.JournalEntry.Date <= budget.EndDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = new List<BudgetLineAnalysisDto>();

        foreach (var line in budget.Lines)
        {
            decimal actual = 0;

            if (line.AccountId.HasValue)
            {
                var matchingLines = journalLines.Where(jl => jl.AccountId == line.AccountId).ToList();

                // Para cuentas de ingresos (7xx): los créditos son ingresos, los débitos correcciones
                // Para cuentas de gastos (6xx): los débitos son gastos, los créditos devoluciones
                actual = line.Type == "Revenue"
                    ? matchingLines.Sum(jl => jl.Credit - jl.Debit)
                    : matchingLines.Sum(jl => jl.Debit - jl.Credit);
            }

            var variance = line.BudgetedAmount - actual;
            var variancePct = line.BudgetedAmount > 0
                ? (int)Math.Round((variance / line.BudgetedAmount) * 100)
                : 0;

            var status = line.Type == "Revenue"
                ? (actual >= line.BudgetedAmount ? "OnTrack" : "UnderBudget")
                : (actual <= line.BudgetedAmount ? "OnTrack" : "OverBudget");

            result.Add(new BudgetLineAnalysisDto(
                line.Id, null, line.Type,
                line.BudgetedAmount, Math.Round(actual, 2),
                Math.Round(variance, 2), variancePct, status));
        }

        return result;
    }
}
