using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Queries;

/// <summary>
/// Libro Diario: Registro cronológico de todas las operaciones contables.
/// Conforme a normativa española (obligatorio para inspección).
/// </summary>
public class GetDiarioQuery : IRequest<GetDiarioResponse>
{
    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    public string? Cuenta { get; set; }
}

public class DiarioLineaDto
{
    public DateTime Fecha { get; set; }
    public string Referencia { get; set; } = string.Empty;
    public string Cuenta { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
    public decimal Saldo { get; set; }
}

public class GetDiarioResponse
{
    public List<DiarioLineaDto> Lineas { get; set; } = new();
    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }
    public int TotalRegistros { get; set; }
}

public class GetDiarioHandler : IRequestHandler<GetDiarioQuery, GetDiarioResponse>
{
    private readonly IAccountingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public GetDiarioHandler(IAccountingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<GetDiarioResponse> Handle(GetDiarioQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant no encontrado");

        var journalEntries = await _context.JournalEntries
            .Include(je => je.JournalEntryLines)
            .ThenInclude(jl => jl.Account)
            .Where(je =>
                je.CompanyId == tenantId &&
                je.Date >= request.FechaInicio &&
                je.Date <= request.FechaFin)
            .OrderBy(je => je.Date)
            .ThenBy(je => je.Reference)
            .ToListAsync(cancellationToken);

        var lineas = new List<DiarioLineaDto>();
        decimal saldoAcumulado = 0;

        foreach (var entry in journalEntries)
        {
            foreach (var line in entry.JournalEntryLines.OrderBy(jl => jl.Account?.Code))
            {
                if (!string.IsNullOrEmpty(request.Cuenta) && line.Account?.Code != request.Cuenta)
                    continue;

                var saldoLinea = line.Debit - line.Credit;
                saldoAcumulado += saldoLinea;

                lineas.Add(new DiarioLineaDto
                {
                    Fecha = entry.Date,
                    Referencia = entry.Reference,
                    Cuenta = line.Account?.Code ?? "---",
                    Descripcion = line.Account?.Name ?? line.AccountName,
                    Debe = line.Debit,
                    Haber = line.Credit,
                    Saldo = saldoAcumulado
                });
            }
        }

        return new GetDiarioResponse
        {
            Lineas = lineas,
            TotalDebe = lineas.Sum(l => l.Debe),
            TotalHaber = lineas.Sum(l => l.Haber),
            TotalRegistros = lineas.Count
        };
    }
}
