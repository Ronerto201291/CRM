using MediatR;

namespace Erp.Modules.Accounting.Application.Queries;

public record GetBudgetsQuery(int? FiscalYear = null) : IRequest<List<BudgetSummaryDto>>;
public record GetBudgetQuery(Guid Id)                 : IRequest<BudgetDetailDto?>;

/// <summary>
/// Devuelve el análisis presupuesto vs real para cada línea del presupuesto.
/// El "real" se calcula sumando los movimientos contables (JournalEntryLines) de las cuentas
/// asociadas al ejercicio fiscal del presupuesto.
/// </summary>
public record GetBudgetAnalysisQuery(Guid BudgetId) : IRequest<List<BudgetLineAnalysisDto>>;

public record BudgetSummaryDto(
    Guid Id,
    string Name,
    int FiscalYear,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    int LinesCount,
    decimal TotalBudgeted,
    DateTime CreatedAt
);

public record BudgetDetailDto(
    Guid Id,
    string Name,
    int FiscalYear,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    List<BudgetLineDto> Lines
);

public record BudgetLineDto(
    Guid Id,
    Guid BudgetId,
    Guid? AccountId,
    string? AccountCode,
    Guid? CostCenterId,
    string Type,
    decimal BudgetedAmount
);

public record BudgetLineAnalysisDto(
    Guid LineId,
    string? AccountCode,
    string Type,
    decimal Budgeted,
    decimal Actual,
    decimal Variance,
    int VariancePercent,
    string Status  // OnTrack | OverBudget | UnderBudget
);
