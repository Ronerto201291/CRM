namespace Erp.Application.Common.Interfaces;

/// <summary>Consultas de nómina para previsión de liquidez (#40) — coste recurrente mensual.</summary>
public interface IAutomationPayrollQuery
{
    /// <summary>
    /// Coste real de nómina del último mes liquidado (Status="Final"): bruto + cuota patronal
    /// SS por línea, que es el desembolso mensual recurrente real de la empresa (el neto y las
    /// retenciones ya están incluidos en el bruto). Devuelve 0 si la empresa no tiene ninguna
    /// liquidación finalizada todavía (empresa sin empleados o recién dada de alta) — no es un
    /// stub, es un estado vacío legítimo.
    /// </summary>
    Task<decimal> GetLatestMonthlyPayrollCostAsync(Guid companyId, CancellationToken ct = default);
}
