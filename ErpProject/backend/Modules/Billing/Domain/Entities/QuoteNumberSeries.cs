using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities;

/// <summary>
/// Contador de numeración de presupuestos por empresa, año y prefijo.
/// Aísla la secuencia por tenant — dos empresas pueden tener PRE-2026-00001 sin conflicto.
/// </summary>
public class QuoteNumberSeries : BaseEntity
{
    public Guid CompanyId { get; set; }
    public int Year { get; set; }
    public string Prefix { get; set; } = "PRE";
    public int LastNumber { get; set; } = 0;
}
