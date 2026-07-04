using MediatR;

namespace Erp.Application.Features.Documents.Queries;

public class GetDocumentDownloadUrlQuery : IRequest<string>
{
    public Guid Id { get; set; }
}
