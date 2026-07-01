using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Handlers;

// ── CREATE ────────────────────────────────────────────────────────────────────
public class CreateProvisionHandler : IRequestHandler<CreateProvisionCommand, Guid>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateProvisionHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateProvisionCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        var provision = new Provision
        {
            Id          = Guid.NewGuid(),
            CompanyId   = companyId,
            Code        = req.Code,
            Description = req.Description,
            Amount      = req.Amount,
            DueDate     = req.DueDate,
            Status      = "Active",
            CreatedAt   = DateTime.UtcNow
        };

        // Asiento de dotación: Debe 6xx → Haber 4xx/14x (cuentas de provisión PGC)
        // La cuenta de gasto depende del código de provisión:
        //   490 → 694 (Pérdidas por deterioro de créditos)
        //   499 → 699 (Pérdidas por deterioro de otros créditos)
        //   147 → 6391 (Dot. provisión para impuestos)
        //   default → 694
        var expenseCode = req.Code switch
        {
            "490" => "694",
            "499" => "699",
            "147" => "6391",
            _     => "694"
        };

        var acctExpense  = await FindOrNullAsync(_ctx, companyId, expenseCode, ct);
        var acctProvision = await FindOrNullAsync(_ctx, companyId, req.Code, ct);

        if (acctExpense is not null && acctProvision is not null)
        {
            var entry = BuildJournalEntry(
                companyId, provision.Id,
                $"Dotación provisión {req.Code} — {req.Description}",
                debitAcct: acctExpense, creditAcct: acctProvision, amount: req.Amount);

            _ctx.JournalEntries.Add(entry);
            provision.LinkedJournalEntryId = entry.Id;
        }

        _ctx.Provisions.Add(provision);
        await _ctx.SaveChangesAsync(ct);
        return provision.Id;
    }

    private static async Task<Account?> FindOrNullAsync(
        IAccountingDbContext ctx, Guid companyId, string code, CancellationToken ct)
        => await ctx.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);

    private static JournalEntry BuildJournalEntry(
        Guid companyId, Guid sourceId, string description,
        Account debitAcct, Account creditAcct, decimal amount)
    {
        var entry = new JournalEntry
        {
            Id          = Guid.NewGuid(),
            CompanyId   = companyId,
            Date        = DateTime.UtcNow,
            Reference   = $"PROV-{sourceId.ToString()[..8].ToUpperInvariant()}",
            Description = description,
            SourceType  = "Provision",
            SourceId    = sourceId,
            IsPosted    = true,
            PostedAt    = DateTime.UtcNow
        };

        var lines = new List<JournalEntryLine>
        {
            new() { Id = Guid.NewGuid(), JournalEntryId = entry.Id,
                    AccountId = debitAcct.Id,  AccountCode = debitAcct.Code,  AccountName = debitAcct.Name,
                    Debit = amount, Credit = 0 },
            new() { Id = Guid.NewGuid(), JournalEntryId = entry.Id,
                    AccountId = creditAcct.Id, AccountCode = creditAcct.Code, AccountName = creditAcct.Name,
                    Debit = 0, Credit = amount }
        };

        entry.JournalEntryLines = lines;
        return entry;
    }
}

// ── UPDATE ────────────────────────────────────────────────────────────────────
public class UpdateProvisionHandler : IRequestHandler<UpdateProvisionCommand>
{
    private readonly IAccountingDbContext _ctx;

    public UpdateProvisionHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(UpdateProvisionCommand req, CancellationToken ct)
    {
        var prov = await _ctx.Provisions.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Provisión {req.Id} no encontrada.");

        if (prov.Status != "Active")
            throw new InvalidOperationException("Solo se pueden modificar provisiones en estado Active.");

        prov.Description = req.Description;
        prov.Amount      = req.Amount;
        prov.DueDate     = req.DueDate;
        prov.UpdatedAt   = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
    }
}

// ── RELEASE ───────────────────────────────────────────────────────────────────
public class ReleaseProvisionHandler : IRequestHandler<ReleaseProvisionCommand>
{
    private readonly IAccountingDbContext _ctx;

    public ReleaseProvisionHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(ReleaseProvisionCommand req, CancellationToken ct)
    {
        var prov = await _ctx.Provisions.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Provisión {req.Id} no encontrada.");

        if (prov.Status != "Active")
            throw new InvalidOperationException("Solo se pueden liberar provisiones activas.");

        // Asiento inverso: Debe 4xx/14x → Haber 795 (Exceso de provisiones)
        var acctProvision = await _ctx.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == prov.CompanyId && a.Code == prov.Code, ct);
        var acctExceso = await _ctx.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == prov.CompanyId && a.Code == "795", ct);

        if (acctProvision is not null && acctExceso is not null)
        {
            var entry = new JournalEntry
            {
                Id          = Guid.NewGuid(),
                CompanyId   = prov.CompanyId,
                Date        = DateTime.UtcNow,
                Reference   = $"PROV-LIB-{prov.Id.ToString()[..8].ToUpperInvariant()}",
                Description = $"Liberación provisión {prov.Code} — {prov.Description}",
                SourceType  = "ProvisionRelease",
                SourceId    = prov.Id,
                IsPosted    = true,
                PostedAt    = DateTime.UtcNow,
                JournalEntryLines = new List<JournalEntryLine>
                {
                    new() { Id = Guid.NewGuid(), JournalEntryId = Guid.Empty,
                            AccountId = acctProvision.Id, AccountCode = acctProvision.Code, AccountName = acctProvision.Name,
                            Debit = prov.Amount, Credit = 0 },
                    new() { Id = Guid.NewGuid(), JournalEntryId = Guid.Empty,
                            AccountId = acctExceso.Id, AccountCode = acctExceso.Code, AccountName = acctExceso.Name,
                            Debit = 0, Credit = prov.Amount }
                }
            };
            foreach (var l in entry.JournalEntryLines) l.JournalEntryId = entry.Id;
            _ctx.JournalEntries.Add(entry);
        }

        prov.Status    = "Released";
        prov.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }
}

// ── DELETE ────────────────────────────────────────────────────────────────────
public class DeleteProvisionHandler : IRequestHandler<DeleteProvisionCommand>
{
    private readonly IAccountingDbContext _ctx;

    public DeleteProvisionHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(DeleteProvisionCommand req, CancellationToken ct)
    {
        var prov = await _ctx.Provisions.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Provisión {req.Id} no encontrada.");

        if (prov.LinkedJournalEntryId.HasValue)
            throw new InvalidOperationException(
                "No se puede eliminar una provisión con asiento contable generado. Use la opción Liberar.");

        _ctx.Provisions.Remove(prov);
        await _ctx.SaveChangesAsync(ct);
    }
}

// ── QUERIES ───────────────────────────────────────────────────────────────────
public class GetProvisionsHandler : IRequestHandler<GetProvisionsQuery, List<ProvisionDto>>
{
    private readonly IAccountingDbContext _ctx;

    public GetProvisionsHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<ProvisionDto>> Handle(GetProvisionsQuery req, CancellationToken ct)
    {
        var q = _ctx.Provisions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(req.Status))
            q = q.Where(p => p.Status == req.Status);

        return await q.OrderBy(p => p.DueDate)
            .Select(p => new ProvisionDto(
                p.Id, p.Code, p.Description, p.Amount,
                p.DueDate, p.Status, p.LinkedJournalEntryId, p.CreatedAt))
            .ToListAsync(ct);
    }
}

public class GetProvisionHandler : IRequestHandler<GetProvisionQuery, ProvisionDto?>
{
    private readonly IAccountingDbContext _ctx;

    public GetProvisionHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<ProvisionDto?> Handle(GetProvisionQuery req, CancellationToken ct)
    {
        var p = await _ctx.Provisions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (p is null) return null;

        return new ProvisionDto(
            p.Id, p.Code, p.Description, p.Amount,
            p.DueDate, p.Status, p.LinkedJournalEntryId, p.CreatedAt);
    }
}
