using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

// ── CREATE ────────────────────────────────────────────────────────────────────
public class CreateDeferredEntryHandler : IRequestHandler<CreateDeferredEntryCommand, Guid>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateDeferredEntryHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateDeferredEntryCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        if (req.PeriodEnd <= req.PeriodStart)
            throw new ArgumentException("PeriodEnd debe ser posterior a PeriodStart.");
        if (req.TotalAmount <= 0)
            throw new ArgumentException("TotalAmount debe ser positivo.");
        if (req.EntryType != "PrepaidExpense" && req.EntryType != "DeferredRevenue")
            throw new ArgumentException("EntryType debe ser 'PrepaidExpense' o 'DeferredRevenue'.");

        var entry = new DeferredEntry
        {
            Id                   = Guid.NewGuid(),
            CompanyId            = companyId,
            EntryType            = req.EntryType,
            Description          = req.Description,
            TotalAmount          = req.TotalAmount,
            PeriodStart          = req.PeriodStart,
            PeriodEnd            = req.PeriodEnd,
            RecognizedAmount     = 0,
            Status               = "Active",
            DeferralAccountCode  = req.DeferralAccountCode,
            CounterpartAccountCode = req.CounterpartAccountCode,
            SourceType           = req.SourceType,
            SourceId             = req.SourceId,
            CreatedAt            = DateTime.UtcNow
        };

        _ctx.DeferredEntries.Add(entry);
        await _ctx.SaveChangesAsync(ct);
        return entry.Id;
    }
}

// ── RECOGNIZE MONTH ───────────────────────────────────────────────────────────
/// <summary>
/// Genera el asiento de reconocimiento mensual para una periodificación.
/// Idempotente: si ya existe el asiento para ese mes, devuelve null.
///
/// PrepaidExpense (480):
///   Debe  6xx (CounterpartAccountCode) = cuota mensual
///   Haber 480 (DeferralAccountCode)    = cuota mensual
///
/// DeferredRevenue (485):
///   Debe  485 (DeferralAccountCode)    = cuota mensual
///   Haber 7xx (CounterpartAccountCode) = cuota mensual
/// </summary>
public class RecognizeDeferredEntryMonthHandler : IRequestHandler<RecognizeDeferredEntryMonthCommand, Guid?>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ILogger<RecognizeDeferredEntryMonthHandler> _logger;

    public RecognizeDeferredEntryMonthHandler(IAccountingDbContext ctx, ILogger<RecognizeDeferredEntryMonthHandler> logger)
    {
        _ctx    = ctx;
        _logger = logger;
    }

    public async Task<Guid?> Handle(RecognizeDeferredEntryMonthCommand req, CancellationToken ct)
    {
        var deferred = await _ctx.DeferredEntries.FindAsync([req.DeferredEntryId], ct)
            ?? throw new KeyNotFoundException($"Periodificación {req.DeferredEntryId} no encontrada.");

        if (deferred.Status == "Completed") return null;

        var monthStart = new DateTime(req.Year, req.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Solo reconocer en el rango PeriodStart–PeriodEnd
        var ps = new DateTime(deferred.PeriodStart.Year, deferred.PeriodStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var pe = new DateTime(deferred.PeriodEnd.Year,   deferred.PeriodEnd.Month,   1, 0, 0, 0, DateTimeKind.Utc);
        if (monthStart < ps || monthStart > pe) return null;

        // Idempotencia
        var reference = $"PERF-{deferred.Id:N[..8]}-{req.Year:D4}{req.Month:D2}";
        reference = $"PERF-{deferred.Id.ToString("N")[..8]}-{req.Year:D4}{req.Month:D2}";
        var exists = await _ctx.JournalEntries
            .AnyAsync(j => j.Reference == reference && j.CompanyId == deferred.CompanyId, ct);
        if (exists) return null;

        // Calcular cuota
        var totalMonths = Math.Max(1,
            ((pe.Year - ps.Year) * 12) + (pe.Month - ps.Month) + 1);
        var monthlyQuota = Math.Round(deferred.TotalAmount / totalMonths, 2);
        var remaining    = deferred.TotalAmount - deferred.RecognizedAmount;
        if (remaining <= 0.001m) return null;
        var quota = Math.Min(monthlyQuota, remaining);
        quota = Math.Round(quota, 2);
        if (quota <= 0) return null;

        // Obtener cuentas
        var acctDeferral = await _ctx.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == deferred.CompanyId && a.Code == deferred.DeferralAccountCode, ct)
            ?? throw new InvalidOperationException($"Cuenta {deferred.DeferralAccountCode} no encontrada.");

        var acctCounter = await _ctx.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == deferred.CompanyId && a.Code == deferred.CounterpartAccountCode, ct)
            ?? throw new InvalidOperationException($"Cuenta contrapartida {deferred.CounterpartAccountCode} no encontrada.");

        // Construir líneas según tipo
        JournalEntryLine lineDebit, lineCredit;
        if (deferred.EntryType == "PrepaidExpense")
        {
            // Debe: 6xx, Haber: 480
            lineDebit  = new() { Id = Guid.NewGuid(), AccountId = acctCounter.Id, AccountCode = acctCounter.Code, AccountName = acctCounter.Name, Debit = quota, Credit = 0 };
            lineCredit = new() { Id = Guid.NewGuid(), AccountId = acctDeferral.Id, AccountCode = acctDeferral.Code, AccountName = acctDeferral.Name, Debit = 0, Credit = quota };
        }
        else // DeferredRevenue
        {
            // Debe: 485, Haber: 7xx
            lineDebit  = new() { Id = Guid.NewGuid(), AccountId = acctDeferral.Id, AccountCode = acctDeferral.Code, AccountName = acctDeferral.Name, Debit = quota, Credit = 0 };
            lineCredit = new() { Id = Guid.NewGuid(), AccountId = acctCounter.Id, AccountCode = acctCounter.Code, AccountName = acctCounter.Name, Debit = 0, Credit = quota };
        }

        var entry = new JournalEntry
        {
            Id          = Guid.NewGuid(),
            CompanyId   = deferred.CompanyId,
            Date        = monthStart,
            Reference   = reference,
            Description = $"Periodificación: {deferred.Description} — {req.Month:D2}/{req.Year}",
            SourceType  = "DeferredEntry",
            SourceId    = deferred.Id,
            IsPosted    = true,
            PostedAt    = DateTime.UtcNow,
            JournalEntryLines = new List<JournalEntryLine> { lineDebit, lineCredit }
        };
        lineDebit.JournalEntryId  = entry.Id;
        lineCredit.JournalEntryId = entry.Id;

        // Actualizar periodificación
        deferred.RecognizedAmount += quota;
        deferred.UpdatedAt         = DateTime.UtcNow;
        if (deferred.RecognizedAmount >= deferred.TotalAmount - 0.01m)
            deferred.Status = "Completed";

        _ctx.JournalEntries.Add(entry);
        await _ctx.SaveChangesAsync(ct);

        _logger.LogInformation("Periodificación {Ref}: {Quota:F2} € reconocidos", reference, quota);
        return entry.Id;
    }
}

// ── QUERIES ───────────────────────────────────────────────────────────────────
public class GetDeferredEntriesHandler : IRequestHandler<GetDeferredEntriesQuery, List<DeferredEntryDto>>
{
    private readonly IAccountingDbContext _ctx;

    public GetDeferredEntriesHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<DeferredEntryDto>> Handle(GetDeferredEntriesQuery req, CancellationToken ct)
    {
        var q = _ctx.DeferredEntries.AsNoTracking();
        if (!string.IsNullOrEmpty(req.Status))
            q = q.Where(d => d.Status == req.Status);

        return await q.OrderByDescending(d => d.CreatedAt)
            .Select(d => ToDto(d))
            .ToListAsync(ct);
    }

    private static DeferredEntryDto ToDto(DeferredEntry d)
    {
        var totalMonths = Math.Max(1,
            ((d.PeriodEnd.Year - d.PeriodStart.Year) * 12) + (d.PeriodEnd.Month - d.PeriodStart.Month) + 1);
        return new DeferredEntryDto(
            d.Id, d.EntryType, d.Description, d.TotalAmount,
            d.PeriodStart, d.PeriodEnd,
            d.RecognizedAmount,
            d.TotalAmount - d.RecognizedAmount,
            totalMonths > 0 ? Math.Round(d.TotalAmount / totalMonths, 2) : 0m,
            totalMonths,
            d.Status, d.DeferralAccountCode, d.CounterpartAccountCode,
            d.SourceType, d.SourceId, d.CreatedAt
        );
    }
}

public class GetDeferredEntryHandler : IRequestHandler<GetDeferredEntryQuery, DeferredEntryDto?>
{
    private readonly IAccountingDbContext _ctx;

    public GetDeferredEntryHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<DeferredEntryDto?> Handle(GetDeferredEntryQuery req, CancellationToken ct)
    {
        var d = await _ctx.DeferredEntries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (d is null) return null;

        var totalMonths = Math.Max(1,
            ((d.PeriodEnd.Year - d.PeriodStart.Year) * 12) + (d.PeriodEnd.Month - d.PeriodStart.Month) + 1);
        return new DeferredEntryDto(
            d.Id, d.EntryType, d.Description, d.TotalAmount,
            d.PeriodStart, d.PeriodEnd,
            d.RecognizedAmount,
            d.TotalAmount - d.RecognizedAmount,
            totalMonths > 0 ? Math.Round(d.TotalAmount / totalMonths, 2) : 0m,
            totalMonths,
            d.Status, d.DeferralAccountCode, d.CounterpartAccountCode,
            d.SourceType, d.SourceId, d.CreatedAt
        );
    }
}
