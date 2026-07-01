using Erp.Application.Common.Interfaces;
using Erp.Application.Features.ApiKeys.Commands;
using Erp.Application.Features.ApiKeys.Queries;
using Erp.Domain.Entities.Api;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Erp.Application.Features.ApiKeys.Handlers;

public class GetApiKeysHandler : IRequestHandler<GetApiKeysQuery, List<ApiKeyDto>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetApiKeysHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<List<ApiKeyDto>> Handle(GetApiKeysQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        return await _ctx.ApiKeys
            .Where(k => k.CompanyId == companyId)
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto
            {
                Id = k.Id, Name = k.Name, KeyPrefix = k.KeyPrefix,
                IsActive = k.IsActive, RateLimit = k.RateLimit, CreatedAt = k.CreatedAt
            })
            .ToListAsync(ct);
    }
}

public class CreateApiKeyHandler : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateApiKeyHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawKey = Convert.ToBase64String(rawBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();

        var apiKey = new ApiKey
        {
            CompanyId = companyId,
            Name = request.Name,
            KeyPrefix = rawKey[..8],
            KeyHash = hash,
            IsActive = true,
            RateLimit = request.RateLimit > 0 ? request.RateLimit : 100
        };

        _ctx.ApiKeys.Add(apiKey);
        await _ctx.SaveChangesAsync(ct);

        return new CreateApiKeyResult
        {
            Id = apiKey.Id, Name = apiKey.Name, KeyPrefix = apiKey.KeyPrefix,
            IsActive = apiKey.IsActive, RateLimit = apiKey.RateLimit,
            CreatedAt = apiKey.CreatedAt, RawKey = rawKey
        };
    }
}

public class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public RevokeApiKeyHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<bool> Handle(RevokeApiKeyCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var key = await _ctx.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == request.Id && k.CompanyId == companyId, ct);
        if (key == null) return false;

        key.IsActive = false;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
