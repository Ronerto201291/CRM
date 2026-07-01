using Erp.Modules.Treasury.Domain.Entities;
using MediatR;

namespace Erp.Modules.Treasury.Application.Features.Treasury.Commands;

// ─── Bank Accounts ─────────────────────────────────────────────────────────────

public record CreateBankAccountCommand(
    string Name,
    string Iban,
    string? BIC,
    string BankName,
    string? AccountingAccountCode
) : IRequest<BankAccountDto>;

public record UpdateBankAccountCommand(
    Guid Id,
    string Name,
    bool IsActive,
    string? Notes
) : IRequest<BankAccountDto>;

// ─── Bank Movements ───────────────────────────────────────────────────────────

public record ImportBankStatementCommand(
    Guid BankAccountId,
    string CsvContent
) : IRequest<List<BankMovementDto>>;

public record CreateBankMovementCommand(
    Guid BankAccountId,
    DateTime Date,
    decimal Amount,
    string Type,
    string Reference,
    string Description
) : IRequest<BankMovementDto>;

// ─── Reconciliation ────────────────────────────────────────────────────────────

public record ReconcileBankAccountCommand(Guid BankAccountId)
    : IRequest<ReconciliationResultDto>;

// ─── Cash Effects ─────────────────────────────────────────────────────────────

public record CreateCashEffectCommand(
    Guid? ClientId,
    string ClientName,
    string ClientTaxId,
    string EffectNumber,
    DateTime IssueDate,
    DateTime DueDate,
    decimal Amount,
    Guid? BankAccountId,
    string? Notes
) : IRequest<CashEffectDto>;

public record UpdateCashEffectStatusCommand(
    Guid Id,
    string NewStatus
) : IRequest<CashEffectDto>;

// ─── Payment Orders ───────────────────────────────────────────────────────────

public record CreatePaymentOrderCommand(
    string PaymentType,
    string BeneficiaryName,
    string BeneficiaryTaxId,
    string BeneficiaryIban,
    string Description,
    decimal Amount,
    DateTime? ScheduledDate,
    Guid? BankAccountId,
    Guid? SourceId,
    string? SourceType
) : IRequest<PaymentOrderDto>;

public record ExecutePaymentOrderCommand(Guid Id) : IRequest<PaymentOrderDto>;

// ─── Cash Flow Forecast ────────────────────────────────────────────────────────

public record GenerateCashFlowForecastCommand(int Year, int Month)
    : IRequest<List<CashFlowForecastDto>>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record BankAccountDto(
    Guid Id, string Name, string Iban, string? BIC, string BankName,
    decimal CurrentBalance, string CurrencyCode, bool IsActive, string? Notes);

public record BankMovementDto(
    Guid Id, Guid BankAccountId, DateTime Date, string Reference,
    string Description, decimal Amount, string Type, bool IsReconciled, string Origin);

public record ReconciliationResultDto(
    int MatchedCount, decimal MatchedAmount, string Message);

public record CashEffectDto(
    Guid Id, string EffectNumber, string ClientName, string ClientTaxId,
    decimal Amount, DateTime IssueDate, DateTime DueDate, string Status,
    Guid? BankAccountId, string? SEPAXml);

public record PaymentOrderDto(
    Guid Id, string PaymentType, string BeneficiaryName, string BeneficiaryIban,
    decimal Amount, string Status, DateTime? ScheduledDate, DateTime? ExecutedAt,
    Guid? BankAccountId, string? Notes);

public record CashFlowForecastDto(
    Guid Id, DateTime ForecastDate, decimal ExpectedInflow,
    decimal ExpectedOutflow, decimal ExpectedBalance,
    string Source, Guid? SourceId, bool IsActual, string? Notes);
