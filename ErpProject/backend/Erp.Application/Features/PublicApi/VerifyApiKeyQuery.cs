using Erp.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Erp.Application.Features.PublicApi;

public record VerifyApiKeyResponse(string Message, string Status, object? CompanyId);

public class VerifyApiKeyQuery : IRequest<VerifyApiKeyResponse?> { }

public class VerifyApiKeyHandler : IRequestHandler<VerifyApiKeyQuery, VerifyApiKeyResponse?>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public VerifyApiKeyHandler(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Task<VerifyApiKeyResponse?> Handle(VerifyApiKeyQuery request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.ContainsKey(PublicApiContextKeys.ApiKeyId) != true)
            return Task.FromResult<VerifyApiKeyResponse?>(null);

        var companyId = httpContext.Items[PublicApiContextKeys.CompanyId];
        return Task.FromResult<VerifyApiKeyResponse?>(
            new VerifyApiKeyResponse("API Key válida", "authenticated", companyId));
    }
}
