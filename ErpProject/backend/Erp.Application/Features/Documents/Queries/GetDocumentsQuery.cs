using MediatR;

namespace Erp.Application.Features.Documents.Queries;

public record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? EntityType,
    Guid? EntityId,
    string? Description,
    DateTime UploadedAt);

public record PaginatedDocumentsResult(
    IReadOnlyList<DocumentDto> Items,
    int TotalCount);

public class GetDocumentsQuery : IRequest<PaginatedDocumentsResult>
{
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
