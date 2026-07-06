using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Company.Commands;
using Erp.Application.Features.Company.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Company.Handlers;

public class GetCompanyHandler : IRequestHandler<GetCompanyQuery, CompanyDto?>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetCompanyHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<CompanyDto?> Handle(GetCompanyQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        return await _ctx.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Id == companyId)
            .Select(c => new CompanyDto
            {
                Id = c.Id, Name = c.Name, TaxId = c.TaxId,
                Address = c.Address, Country = c.Country,
                PublicUploadToken = c.PublicUploadToken, QrUploadEnabled = c.QrUploadEnabled
            })
            .FirstOrDefaultAsync(ct);
    }
}

public class UpdateCompanyHandler : IRequestHandler<UpdateCompanyCommand, bool>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public UpdateCompanyHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<bool> Handle(UpdateCompanyCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var company = await _ctx.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);
        if (company == null) return false;

        company.Name           = request.Name    ?? company.Name;
        if (request.TaxId is not null)
        {
            if (!Erp.Application.Common.Validation.SpanishTaxIdValidator.IsValid(request.TaxId))
                throw new InvalidOperationException("NIF/CIF/NIE de empresa no válido.");
            company.TaxId = request.TaxId;
        }
        company.Address        = request.Address  ?? company.Address;
        company.QrUploadEnabled = request.QrUploadEnabled;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class RegenerateTokenHandler : IRequestHandler<RegenerateTokenCommand, string>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public RegenerateTokenHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<string> Handle(RegenerateTokenCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var company = await _ctx.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        company.PublicUploadToken = Guid.NewGuid().ToString("N");
        await _ctx.SaveChangesAsync(ct);

        return company.PublicUploadToken;
    }
}
