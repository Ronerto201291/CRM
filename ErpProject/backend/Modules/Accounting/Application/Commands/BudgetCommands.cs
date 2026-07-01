using MediatR;

namespace Erp.Modules.Accounting.Application.Commands;

public record CreateBudgetCommand(
    string Name,
    int FiscalYear,
    DateTime StartDate,
    DateTime EndDate
) : IRequest<Guid>;

public record ApproveBudgetCommand(Guid Id) : IRequest;
public record CloseBudgetCommand(Guid Id)   : IRequest;

public record AddBudgetLineCommand(
    Guid BudgetId,
    Guid? AccountId,
    string AccountCode,   // Código PGC para identificar la cuenta en el display
    Guid? CostCenterId,
    string Type,          // Revenue | Expense
    decimal BudgetedAmount
) : IRequest<Guid>;

public record UpdateBudgetLineCommand(
    Guid LineId,
    decimal BudgetedAmount
) : IRequest;

public record DeleteBudgetLineCommand(Guid LineId) : IRequest;
