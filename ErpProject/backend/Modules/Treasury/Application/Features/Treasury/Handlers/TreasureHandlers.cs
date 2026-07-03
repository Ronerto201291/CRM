using System.Globalization;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.Treasury.Handlers;

// ─── Bank Account Handlers ─────────────────────────────────────────────────────

public class CreateBankAccountHandler : IRequestHandler<CreateBankAccountCommand, BankAccountDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateBankAccountHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<BankAccountDto> Handle(CreateBankAccountCommand req, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var entity = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Name = req.Name,
            Iban = req.Iban.Replace(" ", "").ToUpperInvariant(),
            BIC = req.BIC,
            BankName = req.BankName,
            AccountingAccountCode = req.AccountingAccountCode ?? "572",
            CurrentBalance = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.BankAccounts.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    private static BankAccountDto ToDto(BankAccount b) => new(
        b.Id, b.Name, b.Iban, b.BIC, b.BankName,
        b.CurrentBalance, b.CurrencyCode, b.IsActive, b.Notes);
}

public class GetBankAccountHandler : IRequestHandler<GetBankAccountQuery, BankAccountDto?>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetBankAccountHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<BankAccountDto?> Handle(GetBankAccountQuery req, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var account = await _ctx.BankAccounts
            .Where(b => b.Id == req.Id && b.CompanyId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return account == null ? null : new BankAccountDto(
            account.Id, account.Name, account.Iban, account.BIC, account.BankName,
            account.CurrentBalance, account.CurrencyCode, account.IsActive, account.Notes);
    }
}

public record GetBankAccountQuery(Guid Id) : IRequest<BankAccountDto?>;

public class GetBankAccountsHandler : IRequestHandler<GetBankAccountsQuery, List<BankAccountDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetBankAccountsHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<List<BankAccountDto>> Handle(GetBankAccountsQuery req, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var q = _ctx.BankAccounts.Where(b => b.CompanyId == tenantId);
        if (req.OnlyActive) q = q.Where(b => b.IsActive);

        var accounts = await q.AsNoTracking().ToListAsync(ct);

        foreach (var account in accounts)
        {
            account.CurrentBalance = await _ctx.BankMovements
                .Where(m => m.BankAccountId == account.Id)
                .SumAsync(m => m.Amount, ct);
        }

        return accounts.Select(a => new BankAccountDto(
            a.Id, a.Name, a.Iban, a.BIC, a.BankName,
            a.CurrentBalance, a.CurrencyCode, a.IsActive, a.Notes)).ToList();
    }
}

public record GetBankAccountsQuery(bool OnlyActive = true) : IRequest<List<BankAccountDto>>;

// ─── Bank Movement Handlers ────────────────────────────────────────────────────

public class ImportBankStatementHandler : IRequestHandler<ImportBankStatementCommand, List<BankMovementDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public ImportBankStatementHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<List<BankMovementDto>> Handle(ImportBankStatementCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        // Parsear CSV
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(req.CsvContent));
        using var reader = new StreamReader(stream);

        var movements = new List<BankMovement>();
        var lineNumber = 0;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNumber++;
            if (lineNumber == 1) continue; // Saltar header

            var parts = line.Split(',');
            if (parts.Length < 3) continue;

            if (!DateTime.TryParse(parts[0].Trim(), out var date)) continue;
            if (!decimal.TryParse(parts[1].Trim(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var amount)) continue;

            var description = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var reference = parts.Length > 3 ? parts[3].Trim() : $"IMP-{lineNumber}";

            movements.Add(new BankMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                BankAccountId = req.BankAccountId,
                Date = date,
                Amount = amount,
                Type = amount >= 0 ? "Credit" : "Debit",
                Description = description,
                Reference = reference,
                Origin = "BankImport",
                OriginalBankRef = reference,
                IsReconciled = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (movements.Count > 0)
        {
            await _ctx.BankMovements.AddRangeAsync(movements, ct);
            await _ctx.SaveChangesAsync(ct);
        }

        return movements.Select(m => new BankMovementDto(
            m.Id, m.BankAccountId, m.Date, m.Reference,
            m.Description, m.Amount, m.Type, m.IsReconciled, m.Origin)).ToList();
    }
}

public class CreateBankMovementHandler : IRequestHandler<CreateBankMovementCommand, BankMovementDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateBankMovementHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<BankMovementDto> Handle(CreateBankMovementCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var movement = new BankMovement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            BankAccountId = req.BankAccountId,
            Date = req.Date,
            Amount = req.Amount,
            Type = req.Type,
            Description = req.Description,
            Reference = req.Reference,
            Origin = "Manual",
            IsReconciled = false,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.BankMovements.Add(movement);
        await _ctx.SaveChangesAsync(ct);

        return new BankMovementDto(
            movement.Id, movement.BankAccountId, movement.Date, movement.Reference,
            movement.Description, movement.Amount, movement.Type, movement.IsReconciled, movement.Origin);
    }
}

public record PaginatedBankMovementsResult(
    IReadOnlyList<BankMovementDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetBankMovementsHandler : IRequestHandler<GetBankMovementsQuery, PaginatedBankMovementsResult>
{
    private readonly ITreasuryDbContext _ctx;

    public GetBankMovementsHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<PaginatedBankMovementsResult> Handle(GetBankMovementsQuery req, CancellationToken ct)
    {
        var page = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 500);

        var q = _ctx.BankMovements
            .Where(m => m.BankAccountId == req.BankAccountId)
            .AsQueryable();

        if (req.OnlyUnreconciled)
            q = q.Where(m => !m.IsReconciled);
        if (req.From.HasValue)
            q = q.Where(m => m.Date >= req.From.Value);
        if (req.To.HasValue)
            q = q.Where(m => m.Date <= req.To.Value);

        var totalCount = await q.CountAsync(ct);
        var movements = await q.OrderByDescending(m => m.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking().ToListAsync(ct);

        var items = movements.Select(m => new BankMovementDto(
            m.Id, m.BankAccountId, m.Date, m.Reference,
            m.Description, m.Amount, m.Type, m.IsReconciled, m.Origin)).ToList();

        return new PaginatedBankMovementsResult(items, totalCount, page, pageSize);
    }
}

public record GetBankMovementsQuery(
    Guid BankAccountId,
    bool OnlyUnreconciled = false,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PaginatedBankMovementsResult>;

// ─── Reconciliation Handler ────────────────────────────────────────────────────

public class ReconcileBankAccountHandler : IRequestHandler<ReconcileBankAccountCommand, ReconciliationResultDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly IAccountingDbContext _accCtx;
    private readonly ITenantContext _tenant;

    public ReconcileBankAccountHandler(
        ITreasuryDbContext ctx,
        IAccountingDbContext accCtx,
        ITenantContext tenant)
    {
        _ctx = ctx;
        _accCtx = accCtx;
        _tenant = tenant;
    }

    public async Task<ReconciliationResultDto> Handle(ReconcileBankAccountCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var movements = await _ctx.BankMovements
            .Where(m => m.BankAccountId == req.BankAccountId && !m.IsReconciled && m.Origin != "System")
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        var bankAccount = await _ctx.BankAccounts
            .Where(b => b.Id == req.BankAccountId)
            .AsNoTracking().FirstOrDefaultAsync(ct);

        var accountCode = bankAccount?.AccountingAccountCode ?? "572";
        var accountLines = await _accCtx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == companyId
                     && l.AccountCode.StartsWith("572")
                     && l.JournalEntry.IsPosted)
            .AsNoTracking().ToListAsync(ct);

        var unmatchedLines = accountLines
            .Where(l => l.Credit > 0 || l.Debit > 0)
            .ToList();

        var result = new ReconciliationResultDto(0, 0, string.Empty);
        var batchId = Guid.NewGuid();

        foreach (var movement in movements)
        {
            var match = FindMatch(movement, unmatchedLines);
            if (match == null) continue;

            movement.IsReconciled = true;
            movement.MatchedJournalEntryLineId = match.Id;
            movement.ReconciliationBatchId = batchId;

            result = result with
            {
                MatchedCount = result.MatchedCount + 1,
                MatchedAmount = result.MatchedAmount + Math.Abs(movement.Amount)
            };
            unmatchedLines.Remove(match);
        }

        if (result.MatchedCount > 0)
        {
            var batch = new ReconciliationBatch
            {
                Id = batchId,
                CompanyId = companyId,
                BankAccountId = req.BankAccountId,
                ReconciledAt = DateTime.UtcNow,
                ItemsCount = result.MatchedCount,
                TotalAmount = result.MatchedAmount,
                Type = "Auto"
            };
            _ctx.ReconciliationBatches.Add(batch);
            await _ctx.SaveChangesAsync(ct);
            result = result with { Message = $"Conciliados {result.MatchedCount} movimientos ({result.MatchedAmount:F2} €)" };
        }
        else
        {
            result = result with { Message = "No se encontraron coincidencias automáticas" };
        }

        return result;
    }

    private JournalEntryLine? FindMatch(BankMovement movement, List<JournalEntryLine> candidates)
    {
        var movementAbs = Math.Abs(movement.Amount);

        // Fase 1: importe exacto + fecha exacta
        var match = candidates.FirstOrDefault(l =>
            Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
            && l.JournalEntry.Date.Date == movement.Date.Date);
        if (match != null) return match;

        // Fase 2: importe + referencia factura en descripción
        var refs = ExtractInvoiceRefs(movement.Description);
        if (refs.Count > 0)
        {
            match = candidates.FirstOrDefault(l =>
                Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
                && refs.Any(r =>
                    (l.JournalEntry.Description?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)));
            if (match != null) return match;
        }

        // Fase 3: importe + fecha ±3 días
        return candidates.FirstOrDefault(l =>
            Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
            && Math.Abs((l.JournalEntry.Date.Date - movement.Date.Date).TotalDays) <= 3);
    }

    private static List<string> ExtractInvoiceRefs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        var refs = new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(
            text, @"[A-Z]*-?\d{4,}");
        foreach (System.Text.RegularExpressions.Match m in matches)
            refs.Add(m.Value);
        return refs;
    }
}

// ─── Cash Effect Handlers ──────────────────────────────────────────────────────

public class CreateCashEffectHandler : IRequestHandler<CreateCashEffectCommand, CashEffectDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateCashEffectHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CashEffectDto> Handle(CreateCashEffectCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var effect = new CashEffect
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ClientId = req.ClientId,
            ClientName = req.ClientName,
            ClientTaxId = req.ClientTaxId,
            EffectNumber = req.EffectNumber,
            IssueDate = req.IssueDate,
            DueDate = req.DueDate,
            Amount = req.Amount,
            Status = "Pending",
            BankAccountId = req.BankAccountId,
            Notes = req.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.CashEffects.Add(effect);
        await _ctx.SaveChangesAsync(ct);

        return ToDto(effect);
    }

    private static CashEffectDto ToDto(CashEffect e) => new(
        e.Id, e.EffectNumber, e.ClientName, e.ClientTaxId,
        e.Amount, e.IssueDate, e.DueDate, e.Status, e.BankAccountId, null);
}

public record PaginatedCashEffectsResult(
    IReadOnlyList<CashEffectDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetCashEffectsHandler : IRequestHandler<GetCashEffectsQuery, PaginatedCashEffectsResult>
{
    private readonly ITreasuryDbContext _ctx;

    public GetCashEffectsHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<PaginatedCashEffectsResult> Handle(GetCashEffectsQuery req, CancellationToken ct)
    {
        var page = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 500);

        var q = _ctx.CashEffects.AsQueryable();
        if (req.Status != null) q = q.Where(e => e.Status == req.Status);

        var totalCount = await q.CountAsync(ct);
        var effects = await q.OrderBy(e => e.DueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking().ToListAsync(ct);

        var items = effects.Select(e => new CashEffectDto(
            e.Id, e.EffectNumber, e.ClientName, e.ClientTaxId,
            e.Amount, e.IssueDate, e.DueDate, e.Status, e.BankAccountId, null)).ToList();

        return new PaginatedCashEffectsResult(items, totalCount, page, pageSize);
    }
}

public record GetCashEffectsQuery(string? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<PaginatedCashEffectsResult>;

public class UpdateCashEffectStatusHandler : IRequestHandler<UpdateCashEffectStatusCommand, CashEffectDto>
{
    private readonly ITreasuryDbContext _ctx;

    public UpdateCashEffectStatusHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<CashEffectDto> Handle(UpdateCashEffectStatusCommand req, CancellationToken ct)
    {
        var effect = await _ctx.CashEffects.FirstOrDefaultAsync(e => e.Id == req.Id, ct)
            ?? throw new InvalidOperationException($"CashEffect {req.Id} no encontrado");

        var previousStatus = effect.Status;
        effect.Status = req.NewStatus;
        await _ctx.SaveChangesAsync(ct);

        return new CashEffectDto(
            effect.Id, effect.EffectNumber, effect.ClientName, effect.ClientTaxId,
            effect.Amount, effect.IssueDate, effect.DueDate, effect.Status, effect.BankAccountId, null);
    }
}

// ─── Payment Order Handlers ────────────────────────────────────────────────────

public class CreatePaymentOrderHandler : IRequestHandler<CreatePaymentOrderCommand, PaymentOrderDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreatePaymentOrderHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<PaymentOrderDto> Handle(CreatePaymentOrderCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PaymentType = req.PaymentType,
            BeneficiaryName = req.BeneficiaryName,
            BeneficiaryTaxId = req.BeneficiaryTaxId,
            BeneficiaryIban = req.BeneficiaryIban.Replace(" ", "").ToUpperInvariant(),
            Description = req.Description,
            Amount = req.Amount,
            ScheduledDate = req.ScheduledDate,
            BankAccountId = req.BankAccountId,
            SourceId = req.SourceId,
            SourceType = req.SourceType,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        _ctx.PaymentOrders.Add(order);
        await _ctx.SaveChangesAsync(ct);

        return ToDto(order);
    }

    private static PaymentOrderDto ToDto(PaymentOrder p) => new(
        p.Id, p.PaymentType, p.BeneficiaryName, p.BeneficiaryIban,
        p.Amount, p.Status, p.ScheduledDate, p.ExecutedAt, p.BankAccountId, p.Notes);
}

public record PaginatedPaymentOrdersResult(
    IReadOnlyList<PaymentOrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetPaymentOrdersHandler : IRequestHandler<GetPaymentOrdersQuery, PaginatedPaymentOrdersResult>
{
    private readonly ITreasuryDbContext _ctx;

    public GetPaymentOrdersHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<PaginatedPaymentOrdersResult> Handle(GetPaymentOrdersQuery req, CancellationToken ct)
    {
        var page = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 500);

        var q = _ctx.PaymentOrders.AsQueryable();
        if (req.Status != null) q = q.Where(p => p.Status == req.Status);

        var totalCount = await q.CountAsync(ct);
        var orders = await q.OrderBy(p => p.ScheduledDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking().ToListAsync(ct);

        var items = orders.Select(p => new PaymentOrderDto(
            p.Id, p.PaymentType, p.BeneficiaryName, p.BeneficiaryIban,
            p.Amount, p.Status, p.ScheduledDate, p.ExecutedAt, p.BankAccountId, p.Notes)).ToList();

        return new PaginatedPaymentOrdersResult(items, totalCount, page, pageSize);
    }
}

public record GetPaymentOrdersQuery(string? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<PaginatedPaymentOrdersResult>;

public class ExecutePaymentOrderHandler : IRequestHandler<ExecutePaymentOrderCommand, PaymentOrderDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public ExecutePaymentOrderHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<PaymentOrderDto> Handle(ExecutePaymentOrderCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var order = await _ctx.PaymentOrders.FirstOrDefaultAsync(p => p.Id == req.Id && p.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException($"PaymentOrder {req.Id} no encontrado");

        if (order.Status != "Approved" && order.Status != "Draft")
            throw new InvalidOperationException($"No se puede ejecutar una orden con estado {order.Status}");

        // Crear movimiento bancario negativo (pago)
        var movement = new BankMovement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            BankAccountId = order.BankAccountId ?? Guid.Empty,
            Date = DateTime.UtcNow,
            Amount = -order.Amount,
            Type = "Debit",
            Reference = $"PO-{order.Id.ToString()[..8]}",
            Description = $"Pago: {order.Description}",
            Origin = "System",
            IsReconciled = false,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.BankMovements.Add(movement);

        order.Status = "Executed";
        order.ExecutedAt = DateTime.UtcNow;
        order.GeneratedBankMovementId = movement.Id;

        await _ctx.SaveChangesAsync(ct);

        return new PaymentOrderDto(
            order.Id, order.PaymentType, order.BeneficiaryName, order.BeneficiaryIban,
            order.Amount, order.Status, order.ScheduledDate, order.ExecutedAt, order.BankAccountId, order.Notes);
    }
}

// ─── Cash Flow Forecast Handlers ──────────────────────────────────────────────

public class GetCashFlowForecastHandler : IRequestHandler<GetCashFlowForecastQuery, List<CashFlowForecastDto>>
{
    private readonly ITreasuryDbContext _ctx;

    public GetCashFlowForecastHandler(ITreasuryDbContext ctx) => _ctx = ctx;

    public async Task<List<CashFlowForecastDto>> Handle(GetCashFlowForecastQuery req, CancellationToken ct)
    {
        var q = _ctx.CashFlowForecasts.AsQueryable();

        if (req.Year.HasValue)
            q = q.Where(f => f.ForecastDate.Year == req.Year.Value);
        if (req.Month.HasValue)
            q = q.Where(f => f.ForecastDate.Month == req.Month.Value);

        var forecasts = await q.OrderBy(f => f.ForecastDate).AsNoTracking().ToListAsync(ct);
        return forecasts.Select(f => new CashFlowForecastDto(
            f.Id, f.ForecastDate, f.ExpectedInflow, f.ExpectedOutflow,
            f.ExpectedBalance, f.Source, f.SourceId, f.IsActual, f.Notes)).ToList();
    }
}

public record GetCashFlowForecastQuery(int? Year = null, int? Month = null) : IRequest<List<CashFlowForecastDto>>;

/// <summary>
/// Genera la previsión de cash flow para un mes concreto calculando:
///
/// ENTRADAS (Inflow):
///   - Movimientos bancarios positivos (Credits) realizados en el mes → IsActual=true
///   - Efectos comerciales (CashEffects) con DueDate en el mes y status Pending/Endorsed → IsActual=false
///
/// SALIDAS (Outflow):
///   - Movimientos bancarios negativos (Debits) realizados en el mes → IsActual=true
///   - Órdenes de pago con ScheduledDate en el mes y status != Executed/Cancelled → IsActual=false
///
/// Saldo previsto = saldo bancario actual + inflow previsto - outflow previsto
/// </summary>
public class GenerateCashFlowForecastHandler : IRequestHandler<GenerateCashFlowForecastCommand, List<CashFlowForecastDto>>
{
    private readonly ITreasuryDbContext    _ctx;
    private readonly ITenantContext        _tenant;

    public GenerateCashFlowForecastHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<List<CashFlowForecastDto>> Handle(GenerateCashFlowForecastCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var from = new DateTime(req.Year, req.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1).AddTicks(-1);

        // ── 1. Movimientos bancarios reales del mes ─────────────────────────────
        var realMovements = await _ctx.BankMovements
            .Where(m => m.Date >= from && m.Date <= to)
            .AsNoTracking()
            .ToListAsync(ct);

        var actualInflow  = realMovements.Where(m => m.Amount > 0).Sum(m => m.Amount);
        var actualOutflow = Math.Abs(realMovements.Where(m => m.Amount < 0).Sum(m => m.Amount));

        // ── 2. Efectos comerciales pendientes con vencimiento en el mes ─────────
        var pendingEffects = await _ctx.CashEffects
            .Where(e => e.DueDate >= from && e.DueDate <= to
                     && (e.Status == "Pending" || e.Status == "Endorsed"))
            .AsNoTracking()
            .ToListAsync(ct);

        var forecastInflow = pendingEffects.Sum(e => e.Amount);

        // ── 3. Órdenes de pago programadas en el mes ────────────────────────────
        var pendingOrders = await _ctx.PaymentOrders
            .Where(p => p.ScheduledDate.HasValue
                     && p.ScheduledDate.Value >= from && p.ScheduledDate.Value <= to
                     && p.Status != "Executed" && p.Status != "Cancelled")
            .AsNoTracking()
            .ToListAsync(ct);

        var forecastOutflow = pendingOrders.Sum(p => p.Amount);

        // ── 4. Saldo bancario actual (suma de todos los movimientos hasta hoy) ──
        var currentBalance = await _ctx.BankMovements
            .Where(m => m.Date < from)
            .AsNoTracking()
            .SumAsync(m => m.Amount, ct);

        var totalInflow  = actualInflow  + forecastInflow;
        var totalOutflow = actualOutflow + forecastOutflow;
        var expectedBal  = currentBalance + totalInflow - totalOutflow;

        // ── 5. Upsert del registro mensual (idempotente) ─────────────────────────
        var existing = await _ctx.CashFlowForecasts
            .FirstOrDefaultAsync(f => f.ForecastDate == from && f.Source == "Generated", ct);

        if (existing is not null)
        {
            existing.ExpectedInflow  = Math.Round(totalInflow,  2);
            existing.ExpectedOutflow = Math.Round(totalOutflow, 2);
            existing.ExpectedBalance = Math.Round(expectedBal,  2);
            existing.IsActual        = false;
            existing.UpdatedAt       = DateTime.UtcNow;
            existing.Notes           = BuildNotes(actualInflow, actualOutflow, forecastInflow, forecastOutflow, currentBalance);
        }
        else
        {
            existing = new CashFlowForecast
            {
                Id               = Guid.NewGuid(),
                CompanyId        = companyId,
                ForecastDate     = from,
                ExpectedInflow   = Math.Round(totalInflow,  2),
                ExpectedOutflow  = Math.Round(totalOutflow, 2),
                ExpectedBalance  = Math.Round(expectedBal,  2),
                Source           = "Generated",
                IsActual         = false,
                Notes            = BuildNotes(actualInflow, actualOutflow, forecastInflow, forecastOutflow, currentBalance),
                CreatedAt        = DateTime.UtcNow
            };
            _ctx.CashFlowForecasts.Add(existing);
        }

        await _ctx.SaveChangesAsync(ct);

        return new List<CashFlowForecastDto>
        {
            new(existing.Id, existing.ForecastDate,
                existing.ExpectedInflow, existing.ExpectedOutflow, existing.ExpectedBalance,
                existing.Source, existing.SourceId, existing.IsActual, existing.Notes)
        };
    }

    private static string BuildNotes(
        decimal actualIn, decimal actualOut,
        decimal forecastIn, decimal forecastOut,
        decimal openingBal)
        => $"Saldo apertura: {openingBal:F2}€ | " +
           $"Cobros reales: {actualIn:F2}€ | Pagos reales: {actualOut:F2}€ | " +
           $"Cobros previstos (efectos): {forecastIn:F2}€ | Pagos previstos (órdenes): {forecastOut:F2}€";
}
