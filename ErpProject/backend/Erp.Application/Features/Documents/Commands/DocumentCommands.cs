using Erp.Application.Features.Documents.Queries;
using MediatR;

namespace Erp.Application.Features.Documents.Commands;

public class UploadDocumentCommand : IRequest<DocumentDto>
{
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Description { get; set; }
}

public class DeleteDocumentCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
