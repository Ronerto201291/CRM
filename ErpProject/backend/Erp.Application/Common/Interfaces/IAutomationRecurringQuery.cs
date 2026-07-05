namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Ingresos recurrentes (servicios contratados por cliente, CRM) para previsión de tesorería
/// (#40). El outflow recurrente (nómina) vive en <see cref="IAutomationPayrollQuery"/> —
/// deliberadamente separado: unificar ambos en esta misma interfaz habría obligado a la
/// implementación (Crm.Infrastructure) a depender también de Payroll, repitiendo la violación
/// de dirección de dependencias ya corregida en ADR-0018 #13. Antes tenía un método
/// `GetMonthlyRecurringOutflowAsync` que siempre devolvía 0 (bug real, ver ADR-0018 ítem 66
/// del catálogo de contra-auditoría) — se eliminó en vez de implementarlo aquí por ese motivo.
/// </summary>
public interface IAutomationRecurringQuery
{
    Task<decimal> GetMonthlyRecurringInflowAsync(Guid companyId, CancellationToken ct = default);
}
