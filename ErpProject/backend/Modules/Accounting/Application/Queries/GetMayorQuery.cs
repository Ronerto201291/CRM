using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Queries;

/// <summary>
/// Mayor: Resumen por cuenta de todos los movimientos.
/// Muestra saldo inicial, movimientos y saldo final por cuenta.
/// </summary>
public class GetMayorQuery : IRequest<GetMayorResponse>
{
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string? CuentaFiltro { get; set; }
}

public class MayorCuentaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public decimal SaldoInicial { get; set; }
    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }
    public decimal SaldoFinal { get; set; }
}

public class GetMayorResponse
{
    public List<MayorCuentaDto> Cuentas { get; set; } = new();
    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }
    public int TotalCuentas { get; set; }
}

public class GetMayorHandler : IRequestHandler<GetMayorQuery, GetMayorResponse>
{
    private readonly IAccountingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public GetMayorHandler(IAccountingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<GetMayorResponse> Handle(GetMayorQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant no encontrado");

        var cuentas = await _context.Accounts
            .Where(a => a.CompanyId == tenantId)
            .ToListAsync(cancellationToken);

        var movimientos = await _context.JournalEntryLines
            .Include(jl => jl.Account)
            .Include(jl => jl.JournalEntry)
            .Where(jl =>
                jl.JournalEntry!.CompanyId == tenantId &&
                jl.JournalEntry.Date >= request.FechaInicio &&
                jl.JournalEntry.Date <= request.FechaFin)
            .GroupBy(jl => jl.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                TotalDebe = g.Sum(jl => jl.Debit),
                TotalHaber = g.Sum(jl => jl.Credit)
            })
            .ToListAsync(cancellationToken);

        var mayores = new List<MayorCuentaDto>();
        decimal totalDebeGlobal = 0;
        decimal totalHaberGlobal = 0;

        foreach (var cuenta in cuentas)
        {
            if (!string.IsNullOrEmpty(request.CuentaFiltro) && cuenta.Code != request.CuentaFiltro)
                continue;

            var movimiento = movimientos.FirstOrDefault(m => m.AccountId == cuenta.Id);
            var debe = movimiento?.TotalDebe ?? 0;
            var haber = movimiento?.TotalHaber ?? 0;

            var saldoFinal = cuenta.Type switch
            {
                "Asset" or "Expense" => debe - haber,
                "Liability" or "Equity" or "Income" => haber - debe,
                _ => 0
            };

            if (debe > 0 || haber > 0)
            {
                mayores.Add(new MayorCuentaDto
                {
                    Codigo = cuenta.Code,
                    Nombre = cuenta.Name,
                    Tipo = cuenta.Type,
                    SaldoInicial = 0,
                    TotalDebe = debe,
                    TotalHaber = haber,
                    SaldoFinal = saldoFinal
                });

                totalDebeGlobal += debe;
                totalHaberGlobal += haber;
            }
        }

        return new GetMayorResponse
        {
            Cuentas = mayores.OrderBy(c => c.Codigo).ToList(),
            TotalDebe = totalDebeGlobal,
            TotalHaber = totalHaberGlobal,
            TotalCuentas = mayores.Count
        };
    }
}
