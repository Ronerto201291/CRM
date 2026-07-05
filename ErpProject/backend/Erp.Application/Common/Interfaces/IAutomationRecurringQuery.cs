namespace Erp.Application.Common.Interfaces;

/// <summary>Servicios recurrentes para previsión de tesorería (#40).</summary>
public interface IAutomationRecurringQuery
{
    Task<decimal> GetMonthlyRecurringInflowAsync(Guid companyId, CancellationToken ct = default);
    Task<decimal> GetMonthlyRecurringOutflowAsync(Guid companyId, CancellationToken ct = default);
}
