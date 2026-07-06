namespace Erp.Application.Common.Interfaces;

public sealed record CompanyMembershipLimitResult(
    bool Allowed,
    int CompaniesUsed,
    int MaxCompanies,
    string? Reason);

/// <summary>
/// Valida límite de empresas por plan SaaS (ADR-0018 #42a fase 4).
/// </summary>
public interface ICompanyMembershipLimitService
{
    Task<CompanyMembershipLimitResult> CheckCanAddCompanyAsync(Guid userId, CancellationToken ct = default);

    Task<(int CompaniesUsed, int MaxCompanies)> GetUsageForUserAsync(Guid userId, CancellationToken ct = default);
}
