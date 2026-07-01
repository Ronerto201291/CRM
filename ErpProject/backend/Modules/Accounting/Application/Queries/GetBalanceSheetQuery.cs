using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Queries;

/// <summary>
/// Balance Sheet: Estado de situación financiera (Activo = Pasivo + Patrimonio).
/// Muestra la posición financiera a una fecha específica.
/// </summary>
public class GetBalanceSheetQuery : IRequest<GetBalanceSheetResponse>
{
    public DateTime FechaCorte { get; set; }
}

public class BalanceSheetSectionDto
{
    public string Nombre { get; set; } = string.Empty;
    public List<BalanceSheetLineDto> Lineas { get; set; } = new();
    public decimal Total { get; set; }
}

public class BalanceSheetLineDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

public class GetBalanceSheetResponse
{
    public BalanceSheetSectionDto Activo { get; set; } = new();
    public BalanceSheetSectionDto Pasivo { get; set; } = new();
    public BalanceSheetSectionDto Patrimonio { get; set; } = new();

    public decimal TotalActivo => Activo.Total;
    public decimal TotalPasivoPatrimonio => Pasivo.Total + Patrimonio.Total;
    public bool EstaBalanceado => Math.Abs(TotalActivo - TotalPasivoPatrimonio) < 0.01m;
}

public class GetBalanceSheetHandler : IRequestHandler<GetBalanceSheetQuery, GetBalanceSheetResponse>
{
    private readonly IAccountingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public GetBalanceSheetHandler(IAccountingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<GetBalanceSheetResponse> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant no encontrado");

        var movimientos = await _context.JournalEntryLines
            .Include(jl => jl.Account)
            .Include(jl => jl.JournalEntry)
            .Where(jl =>
                jl.JournalEntry!.CompanyId == tenantId &&
                jl.JournalEntry.Date <= request.FechaCorte)
            .GroupBy(jl => jl.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                TotalDebe = g.Sum(jl => jl.Debit),
                TotalHaber = g.Sum(jl => jl.Credit),
                Account = g.First().Account
            })
            .ToListAsync(cancellationToken);

        var activos = new List<BalanceSheetLineDto>();
        var pasivos = new List<BalanceSheetLineDto>();
        var patrimonios = new List<BalanceSheetLineDto>();

        foreach (var mov in movimientos)
        {
            if (mov.Account == null) continue;

            var saldo = mov.Account.Type switch
            {
                "Asset" or "Expense" => mov.TotalDebe - mov.TotalHaber,
                "Liability" or "Equity" or "Income" => mov.TotalHaber - mov.TotalDebe,
                _ => 0
            };

            if (Math.Abs(saldo) < 0.01m) continue;

            var linea = new BalanceSheetLineDto
            {
                Codigo = mov.Account.Code,
                Descripcion = mov.Account.Name,
                Monto = Math.Abs(saldo)
            };

            switch (mov.Account.Type)
            {
                case "Asset":   activos.Add(linea); break;
                case "Liability": pasivos.Add(linea); break;
                case "Equity":  patrimonios.Add(linea); break;
            }
        }

        return new GetBalanceSheetResponse
        {
            Activo = new BalanceSheetSectionDto
            {
                Nombre = "ACTIVO",
                Lineas = activos.OrderBy(l => l.Codigo).ToList(),
                Total = activos.Sum(l => l.Monto)
            },
            Pasivo = new BalanceSheetSectionDto
            {
                Nombre = "PASIVO",
                Lineas = pasivos.OrderBy(l => l.Codigo).ToList(),
                Total = pasivos.Sum(l => l.Monto)
            },
            Patrimonio = new BalanceSheetSectionDto
            {
                Nombre = "PATRIMONIO",
                Lineas = patrimonios.OrderBy(l => l.Codigo).ToList(),
                Total = patrimonios.Sum(l => l.Monto)
            }
        };
    }
}
