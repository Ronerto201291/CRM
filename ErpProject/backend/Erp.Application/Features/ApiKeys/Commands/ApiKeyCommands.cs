using MediatR;

namespace Erp.Application.Features.ApiKeys.Commands;

public class CreateApiKeyResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RateLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RawKey { get; set; } = string.Empty;
}

public class CreateApiKeyCommand : IRequest<CreateApiKeyResult>
{
    public string Name { get; set; } = string.Empty;
    public int RateLimit { get; set; } = 100;
}

public class RevokeApiKeyCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}
