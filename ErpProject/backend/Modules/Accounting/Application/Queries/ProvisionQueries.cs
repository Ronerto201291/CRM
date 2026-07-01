using MediatR;

namespace Erp.Modules.Accounting.Application.Queries;

public record GetProvisionsQuery(string? Status = null) : IRequest<List<ProvisionDto>>;
public record GetProvisionQuery(Guid Id) : IRequest<ProvisionDto?>;

public record ProvisionDto(
    Guid Id,
    string Code,
    string Description,
    decimal Amount,
    DateTime DueDate,
    string Status,
    Guid? LinkedJournalEntryId,
    DateTime CreatedAt
);
