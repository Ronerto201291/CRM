using MediatR;

namespace Erp.Application.Features.ApiKeys.Queries;

public class ApiKeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RateLimit { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetApiKeysQuery : IRequest<List<ApiKeyDto>>
{
}
