using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Queries;

// ─── DTOs ────────────────────────────────────────────────────────────────────

public class JournalLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class JournalEntryListDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public bool IsPosted { get; set; }
    public DateTime? PostedAt { get; set; }
    public List<JournalLineDto> Lines { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
}

public class TrialBalanceLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
}

public class IVADetailDto
{
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class IVASoportadoResult { public decimal Total { get; set; } public List<IVADetailDto> Details { get; set; } = new(); }
public class IVARepercutidoResult { public decimal Total { get; set; } public List<IVADetailDto> Details { get; set; } = new(); }

public class LiquidacionIVAResult
{
    public string Trimestre { get; set; } = string.Empty;
    public decimal IvaRepercutido { get; set; }
    public decimal IvaSoportado { get; set; }
    public decimal Resultado { get; set; }
    public decimal AIngresar { get; set; }
    public decimal ACompensar { get; set; }
}

public class PyGLineAccountDto { public string AccountCode { get; set; } = string.Empty; public string AccountName { get; set; } = string.Empty; public decimal Importe { get; set; } }
public class PyGResult
{
    public int Ejercicio { get; set; }
    public object Ingresos { get; set; } = new();
    public object Gastos { get; set; } = new();
    public decimal ResultadoEjercicio { get; set; }
    public decimal? Beneficio { get; set; }
    public decimal? Perdida { get; set; }
}

public class MayorMovimientoDto { public DateTime Date { get; set; } public string Reference { get; set; } = string.Empty; public string? Description { get; set; } public decimal Debit { get; set; } public decimal Credit { get; set; } }
public class MayorResult
{
    public string AccountCode { get; set; } = string.Empty;
    public int Ejercicio { get; set; }
    public List<MayorMovimientoDto> Movimientos { get; set; } = new();
    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }
    public decimal Saldo { get; set; }
}

// ─── Queries ─────────────────────────────────────────────────────────────────

public class GetJournalQuery : IRequest<List<JournalEntryListDto>> { public int? Year { get; set; } }
public class GetTrialBalanceQuery : IRequest<List<TrialBalanceLineDto>> { public int? Year { get; set; } }
public class GetIVASoportadoQuery : IRequest<IVASoportadoResult> { public int? Year { get; set; } }
public class GetIVARepercutidoQuery : IRequest<IVARepercutidoResult> { public int? Year { get; set; } }
public class GetLiquidacionIVAQuery : IRequest<LiquidacionIVAResult> { public int? Quarter { get; set; } public int? Year { get; set; } }
public class GetPyGQuery : IRequest<PyGResult> { public int? Year { get; set; } }
public class GetMayorControllerQuery : IRequest<MayorResult> { public string AccountCode { get; set; } = string.Empty; public int? Year { get; set; } }

// ─── Handlers ────────────────────────────────────────────────────────────────

public class GetJournalHandler : IRequestHandler<GetJournalQuery, List<JournalEntryListDto>>
{
    private readonly IAccountingDbContext _ctx;
    public GetJournalHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<JournalEntryListDto>> Handle(GetJournalQuery request, CancellationToken ct)
    {
        var q = _ctx.JournalEntries.Include(j => j.JournalEntryLines).AsQueryable();
        if (request.Year.HasValue) q = q.Where(j => j.Date.Year == request.Year.Value);

        return await q.OrderByDescending(j => j.Date)
            .Select(j => new JournalEntryListDto
            {
                Id = j.Id, Date = j.Date, Reference = j.Reference, Description = j.Description,
                SourceType = j.SourceType, SourceId = j.SourceId, IsPosted = j.IsPosted, PostedAt = j.PostedAt,
                Lines = j.JournalEntryLines.Select(l => new JournalLineDto
                {
                    AccountCode = l.AccountCode, AccountName = l.AccountName, Debit = l.Debit, Credit = l.Credit
                }).ToList(),
                TotalDebit  = j.JournalEntryLines.Sum(l => l.Debit),
                TotalCredit = j.JournalEntryLines.Sum(l => l.Credit)
            })
            .ToListAsync(ct);
    }
}

public class GetTrialBalanceHandler : IRequestHandler<GetTrialBalanceQuery, List<TrialBalanceLineDto>>
{
    private readonly IAccountingDbContext _ctx;
    public GetTrialBalanceHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<TrialBalanceLineDto>> Handle(GetTrialBalanceQuery request, CancellationToken ct)
    {
        var q = _ctx.JournalEntryLines.AsQueryable();
        if (request.Year.HasValue) q = q.Where(l => l.JournalEntry!.Date.Year == request.Year.Value);

        return await q.GroupBy(l => new { l.AccountCode, l.AccountName })
            .Select(g => new TrialBalanceLineDto
            {
                AccountCode = g.Key.AccountCode, AccountName = g.Key.AccountName,
                TotalDebit  = g.Sum(l => l.Debit),
                TotalCredit = g.Sum(l => l.Credit),
                Balance     = g.Sum(l => l.Debit) - g.Sum(l => l.Credit)
            })
            .OrderBy(b => b.AccountCode)
            .ToListAsync(ct);
    }
}

public class GetIVASoportadoHandler : IRequestHandler<GetIVASoportadoQuery, IVASoportadoResult>
{
    private readonly IAccountingDbContext _ctx;
    public GetIVASoportadoHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<IVASoportadoResult> Handle(GetIVASoportadoQuery request, CancellationToken ct)
    {
        var q = _ctx.JournalEntryLines.Where(l => l.AccountCode == "472");
        if (request.Year.HasValue) q = q.Where(l => l.JournalEntry!.Date.Year == request.Year.Value);

        var total   = await q.SumAsync(l => l.Debit, ct);
        var details = await q.Include(l => l.JournalEntry)
            .OrderByDescending(l => l.JournalEntry!.Date)
            .Select(l => new IVADetailDto { Date = l.JournalEntry!.Date, Reference = l.JournalEntry.Reference, Amount = l.Debit })
            .ToListAsync(ct);

        return new IVASoportadoResult { Total = total, Details = details };
    }
}

public class GetIVARepercutidoHandler : IRequestHandler<GetIVARepercutidoQuery, IVARepercutidoResult>
{
    private readonly IAccountingDbContext _ctx;
    public GetIVARepercutidoHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<IVARepercutidoResult> Handle(GetIVARepercutidoQuery request, CancellationToken ct)
    {
        var q = _ctx.JournalEntryLines.Where(l => l.AccountCode == "477");
        if (request.Year.HasValue) q = q.Where(l => l.JournalEntry!.Date.Year == request.Year.Value);

        var total   = await q.SumAsync(l => l.Credit, ct);
        var details = await q.Include(l => l.JournalEntry)
            .OrderByDescending(l => l.JournalEntry!.Date)
            .Select(l => new IVADetailDto { Date = l.JournalEntry!.Date, Reference = l.JournalEntry.Reference, Amount = l.Credit })
            .ToListAsync(ct);

        return new IVARepercutidoResult { Total = total, Details = details };
    }
}

public class GetLiquidacionIVAHandler : IRequestHandler<GetLiquidacionIVAQuery, LiquidacionIVAResult>
{
    private readonly IAccountingDbContext _ctx;
    public GetLiquidacionIVAHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<LiquidacionIVAResult> Handle(GetLiquidacionIVAQuery request, CancellationToken ct)
    {
        var y = request.Year ?? DateTime.UtcNow.Year;
        var q = request.Quarter ?? ((DateTime.UtcNow.Month - 1) / 3 + 1);
        var startMonth = (q - 1) * 3 + 1;
        var start = new DateTime(y, startMonth, 1);
        var end   = start.AddMonths(3);

        var repercutido = await _ctx.JournalEntryLines
            .Where(l => l.AccountCode == "477" && l.JournalEntry!.Date >= start && l.JournalEntry.Date < end)
            .SumAsync(l => l.Credit, ct);

        var soportado = await _ctx.JournalEntryLines
            .Where(l => l.AccountCode == "472" && l.JournalEntry!.Date >= start && l.JournalEntry.Date < end)
            .SumAsync(l => l.Debit, ct);

        var resultado = repercutido - soportado;
        return new LiquidacionIVAResult
        {
            Trimestre = $"T{q} {y}", IvaRepercutido = repercutido, IvaSoportado = soportado,
            Resultado = resultado, AIngresar = resultado > 0 ? resultado : 0,
            ACompensar = resultado < 0 ? Math.Abs(resultado) : 0
        };
    }
}

public class GetPyGHandler : IRequestHandler<GetPyGQuery, PyGResult>
{
    private readonly IAccountingDbContext _ctx;
    public GetPyGHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<PyGResult> Handle(GetPyGQuery request, CancellationToken ct)
    {
        var y     = request.Year ?? DateTime.UtcNow.Year;
        var start = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(y + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var lines = await _ctx.JournalEntryLines
            .Where(l => l.JournalEntry!.Date >= start && l.JournalEntry.Date < end && l.JournalEntry.IsPosted)
            .Select(l => new { l.AccountCode, l.AccountName, l.Debit, l.Credit })
            .ToListAsync(ct);

        var gastos = lines.Where(l => l.AccountCode.StartsWith("6"))
            .GroupBy(l => new { l.AccountCode, l.AccountName })
            .Select(g => new PyGLineAccountDto { AccountCode = g.Key.AccountCode, AccountName = g.Key.AccountName, Importe = g.Sum(l => l.Debit) - g.Sum(l => l.Credit) })
            .Where(g => g.Importe != 0).OrderBy(g => g.AccountCode).ToList();

        var ingresos = lines.Where(l => l.AccountCode.StartsWith("7"))
            .GroupBy(l => new { l.AccountCode, l.AccountName })
            .Select(g => new PyGLineAccountDto { AccountCode = g.Key.AccountCode, AccountName = g.Key.AccountName, Importe = g.Sum(l => l.Credit) - g.Sum(l => l.Debit) })
            .Where(g => g.Importe != 0).OrderBy(g => g.AccountCode).ToList();

        var totalIngresos = ingresos.Sum(i => i.Importe);
        var totalGastos = gastos.Sum(g => g.Importe);
        var resultado = totalIngresos - totalGastos;

        return new PyGResult
        {
            Ejercicio = y,
            Ingresos  = new { detalle = ingresos, total = totalIngresos },
            Gastos    = new { detalle = gastos, total = totalGastos },
            ResultadoEjercicio = resultado,
            Beneficio = resultado > 0 ? resultado : (decimal?)null,
            Perdida   = resultado < 0 ? Math.Abs(resultado) : (decimal?)null
        };
    }
}

public class GetMayorControllerHandler : IRequestHandler<GetMayorControllerQuery, MayorResult>
{
    private readonly IAccountingDbContext _ctx;
    public GetMayorControllerHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<MayorResult> Handle(GetMayorControllerQuery request, CancellationToken ct)
    {
        var y     = request.Year ?? DateTime.UtcNow.Year;
        var start = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end   = new DateTime(y + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var movimientos = await _ctx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.AccountCode == request.AccountCode
                && l.JournalEntry!.Date >= start && l.JournalEntry.Date < end
                && l.JournalEntry.IsPosted)
            .OrderBy(l => l.JournalEntry!.Date)
            .Select(l => new MayorMovimientoDto
            {
                Date = l.JournalEntry!.Date, Reference = l.JournalEntry.Reference,
                Description = l.JournalEntry.Description, Debit = l.Debit, Credit = l.Credit
            })
            .ToListAsync(ct);

        var totalDebe  = movimientos.Sum(m => m.Debit);
        var totalHaber = movimientos.Sum(m => m.Credit);

        return new MayorResult
        {
            AccountCode = request.AccountCode, Ejercicio = y,
            Movimientos = movimientos, TotalDebe = totalDebe,
            TotalHaber = totalHaber, Saldo = totalDebe - totalHaber
        };
    }
}
