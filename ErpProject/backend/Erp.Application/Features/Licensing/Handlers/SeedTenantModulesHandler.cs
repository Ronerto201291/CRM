using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Application.Features.Licensing.Handlers;

/// <summary>
/// Enables every module included in the tenant's plan as soon as the company is created.
/// Without this, ModuleAuthorizationHandler fails closed for every real signup: it requires
/// an explicit TenantModule row (IsEnabled=true) in addition to the plan's PlanModule row,
/// and nothing else in the signup flow ever created one (only the dev bootstrap in
/// Program.cs did). See ADR-0018 #42c.
/// </summary>
public class SeedTenantModulesHandler : INotificationHandler<CompanyCreatedEvent>
{
    private readonly ILicensingDbContext _ctx;
    private readonly ILogger<SeedTenantModulesHandler> _logger;

    public SeedTenantModulesHandler(ILicensingDbContext ctx, ILogger<SeedTenantModulesHandler> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    public async Task Handle(CompanyCreatedEvent notification, CancellationToken ct)
    {
        // IgnoreQueryFilters: this runs right after signup, before any tenant context is
        // resolved for the new company (the caller isn't authenticated as that tenant yet),
        // so the CompanyId query filter on Subscriptions/TenantModules would otherwise match
        // nothing and silently no-op.
        var subscription = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == notification.CompanyId && s.IsActive, ct);
        if (subscription == null)
        {
            _logger.LogWarning(
                "SeedTenantModulesHandler: no active subscription for company {CompanyId}, skipping module seed.",
                notification.CompanyId);
            return;
        }

        var includedModules = await _ctx.Plans
            .Where(p => p.Name == subscription.PlanName && p.IsActive)
            .SelectMany(p => p.PlanModules)
            .Where(pm => pm.IsIncluded)
            .Select(pm => pm.ModuleName)
            .ToListAsync(ct);

        var existing = await _ctx.TenantModules
            .IgnoreQueryFilters()
            .Where(m => m.CompanyId == notification.CompanyId)
            .Select(m => m.ModuleName)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        foreach (var moduleName in includedModules)
        {
            if (existingSet.Contains(moduleName)) continue;
            _ctx.TenantModules.Add(new TenantModule
            {
                Id = Guid.NewGuid(),
                CompanyId = notification.CompanyId,
                ModuleName = moduleName,
                IsEnabled = true
            });
        }

        await _ctx.SaveChangesAsync(ct);
    }
}
