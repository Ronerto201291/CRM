using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Queries;

/// <summary>
/// Profit &amp; Loss (PyG): Estado de resultados.
/// Muestra ingresos, gastos y resultado neto en un período.
/// </summary>
public class GetProfitAndLossQuery : IRequest<GetProfitAndLossResponse>
{
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
}

public class PyGSectionDto
{
    public string Nombre { get; set; } = string.Empty;
    public List<PyGLineDto> Lineas { get; set; } = new();
    public decimal SubTotal { get; set; }
}

public class PyGLineDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

public class GetProfitAndLossResponse
{
    public PyGSectionDto Ingresos { get; set; } = new();
    public PyGSectionDto Gastos { get; set; } = new();

    public decimal TotalIngresos => Ingresos.SubTotal;
    public decimal TotalGastos => Gastos.SubTotal;
    public decimal ResultadoBruto => TotalIngresos - TotalGastos;
    public decimal ResultadoNeto => ResultadoBruto;
}

public class GetProfitAndLossHandler : IRequestHandler<GetProfitAndLossQuery, GetProfitAndLossResponse>
{
    private readonly IAccountingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public GetProfitAndLossHandler(IAccountingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<GetProfitAndLossResponse> Handle(GetProfitAndLossQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant no encontrado");

        var movimientos = await _context.JournalEntryLines
            .Include(jl => jl.Account)
            .Include(jl => jl.JournalEntry)
            .Where(jl =>
                jl.JournalEntry!.CompanyId == tenantId &&
                jl.JournalEntry.Date >= request.FechaInicio &&
                jl.JournalEntry.Date <= request.FechaFin &&
                (jl.Account!.Type == "Income" || jl.Account.Type == "Expense"))
            .GroupBy(jl => jl.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                TotalDebe = g.Sum(jl => jl.Debit),
                TotalHaber = g.Sum(jl => jl.Credit),
                Account = g.First().Account
            })
            .ToListAsync(cancellationToken);

        var ingresos = new List<PyGLineDto>();
        var gastos = new List<PyGLineDto>();

        foreach (var mov in movimientos)
        {
            if (mov.Account == null) continue;

            var saldo = mov.Account.Type switch
            {
                "Income" => mov.TotalHaber - mov.TotalDebe,
                "Expense" => mov.TotalDebe - mov.TotalHaber,
                _ => 0
            };

            if (Math.Abs(saldo) < 0.01m) continue;

            var linea = new PyGLineDto
            {
                Codigo = mov.Account.Code,
                Descripcion = mov.Account.Name,
                Monto = Math.Abs(saldo)
            };

            if (mov.Account.Type == "Income")
                ingresos.Add(linea);
            else
                gastos.Add(linea);
        }

        return new GetProfitAndLossResponse
        {
            Ingresos = new PyGSectionDto
            {
                Nombre = "INGRESOS",
                Lineas = ingresos.OrderBy(l => l.Codigo).ToList(),
                SubTotal = ingresos.Sum(l => l.Monto)
            },
            Gastos = new PyGSectionDto
            {
                Nombre = "GASTOS",
                Lineas = gastos.OrderBy(l => l.Codigo).ToList(),
                SubTotal = gastos.Sum(l => l.Monto)
            }
        };
    }
}
