using MediatR;

namespace Erp.Modules.Accounting.Application.Queries;

public record GetDeferredEntriesQuery(string? Status = null) : IRequest<List<DeferredEntryDto>>;
public record GetDeferredEntryQuery(Guid Id) : IRequest<DeferredEntryDto?>;

public record DeferredEntryDto(
    Guid Id,
    string EntryType,
    string Description,
    decimal TotalAmount,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal RecognizedAmount,
    decimal RemainingAmount,
    decimal MonthlyAmount,
    int TotalMonths,
    string Status,
    string DeferralAccountCode,
    string CounterpartAccountCode,
    string? SourceType,
    Guid? SourceId,
    DateTime CreatedAt
);
