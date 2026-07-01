using MediatR;

namespace Erp.Modules.Accounting.Application.Queries;

public record GetFixedAssetsQuery(string? Status = null) : IRequest<List<FixedAssetDto>>;
public record GetFixedAssetQuery(Guid Id) : IRequest<FixedAssetDto?>;

public record FixedAssetDto(
    Guid Id,
    string AssetCode,
    string Name,
    string? Description,
    DateTime AcquisitionDate,
    DateTime CommissioningDate,
    decimal AcquisitionCost,
    decimal ResidualValue,
    int UsefulLifeYears,
    string AmortizationMethod,
    string AssetAccountCode,
    string DepreciationAccountCode,
    string AccumDepreciationAccountCode,
    decimal AccumulatedDepreciation,
    decimal NetBookValue,
    decimal MonthlyDepreciation,
    DateTime? LastAmortizationDate,
    string Status,
    DateTime? DisposedAt,
    string? Notes,
    DateTime CreatedAt
);
