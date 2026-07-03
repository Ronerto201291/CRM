using Erp.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Subscriptions;

public record GetCurrentSubscriptionQuery : IRequest<CurrentSubscriptionDto?>;

public sealed class CurrentSubscriptionDto
{
    public string Plan { get; init; } = "Free";
    public bool IsActive { get; init; }
    public string? StripeStatus { get; init; }
    public DateTime? ExpirationDate { get; init; }
    public IReadOnlyList<string> Modules { get; init; } = Array.Empty<string>();
    public string? Message { get; init; }
}

public class GetCurrentSubscriptionHandler : IRequestHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto?>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetCurrentSubscriptionHandler(IApplicationDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CurrentSubscriptionDto?> Handle(GetCurrentSubscriptionQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var sub = await _ctx.Subscriptions.FirstOrDefaultAsync(s => s.CompanyId == tenantId, ct);
        if (sub == null)
        {
            return new CurrentSubscriptionDto
            {
                Plan = "Free",
                IsActive = false,
                Message = "No active subscription."
            };
        }

        var plan = await _ctx.Plans
            .Include(p => p.PlanModules)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == sub.PlanName, ct);

        return new CurrentSubscriptionDto
        {
            Plan = sub.PlanName,
            IsActive = sub.IsActive,
            StripeStatus = sub.StripeStatus,
            ExpirationDate = sub.ExpirationDate,
            Modules = plan?.PlanModules.Where(m => m.IsIncluded).Select(m => m.ModuleName).ToList()
                ?? new List<string>()
        };
    }
}

public record GetSubscriptionPlansQuery : IRequest<IReadOnlyList<SubscriptionPlanDto>>;

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string? Description,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int MaxUsers,
    int MaxInvoicesPerMonth,
    IReadOnlyList<string> Modules);

public class GetSubscriptionPlansHandler : IRequestHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    private readonly IApplicationDbContext _ctx;

    public GetSubscriptionPlansHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<SubscriptionPlanDto>> Handle(GetSubscriptionPlansQuery request, CancellationToken ct)
    {
        return await _ctx.Plans
            .Include(p => p.PlanModules)
            .Where(p => p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .AsNoTracking()
            .Select(p => new SubscriptionPlanDto(
                p.Id,
                p.Name,
                p.Description,
                p.MonthlyPrice,
                p.YearlyPrice,
                p.MaxUsers,
                p.MaxInvoicesPerMonth,
                p.PlanModules.Where(m => m.IsIncluded).Select(m => m.ModuleName).ToList()))
            .ToListAsync(ct);
    }
}

public record GetBillingHistoryQuery : IRequest<IReadOnlyList<SubscriptionInvoiceDto>>;

public class GetBillingHistoryHandler : IRequestHandler<GetBillingHistoryQuery, IReadOnlyList<SubscriptionInvoiceDto>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ISubscriptionBillingService _billing;
    private readonly ITenantContext _tenant;

    public GetBillingHistoryHandler(
        IApplicationDbContext ctx,
        ISubscriptionBillingService billing,
        ITenantContext tenant)
    {
        _ctx = ctx;
        _billing = billing;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<SubscriptionInvoiceDto>> Handle(GetBillingHistoryQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var company = await _ctx.Companies.FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        if (string.IsNullOrEmpty(company?.StripeCustomerId))
            return Array.Empty<SubscriptionInvoiceDto>();

        return await _billing.ListInvoicesAsync(tenantId, ct);
    }
}

public record CreateCheckoutSessionCommand(string PlanName, string SuccessUrl, string CancelUrl) : IRequest<string>;

public class CreateCheckoutSessionHandler : IRequestHandler<CreateCheckoutSessionCommand, string>
{
    private readonly ISubscriptionBillingService _billing;
    private readonly ITenantContext _tenant;

    public CreateCheckoutSessionHandler(ISubscriptionBillingService billing, ITenantContext tenant)
    {
        _billing = billing;
        _tenant = tenant;
    }

    public Task<string> Handle(CreateCheckoutSessionCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return _billing.CreateCheckoutSessionAsync(tenantId, request.PlanName, request.SuccessUrl, request.CancelUrl, ct);
    }
}

public record CreatePortalSessionCommand(string ReturnUrl) : IRequest<string>;

public class CreatePortalSessionHandler : IRequestHandler<CreatePortalSessionCommand, string>
{
    private readonly ISubscriptionBillingService _billing;
    private readonly ITenantContext _tenant;

    public CreatePortalSessionHandler(ISubscriptionBillingService billing, ITenantContext tenant)
    {
        _billing = billing;
        _tenant = tenant;
    }

    public Task<string> Handle(CreatePortalSessionCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return _billing.CreatePortalSessionAsync(tenantId, request.ReturnUrl, ct);
    }
}
