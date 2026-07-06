using Erp.Application.Common.Interfaces;
using Erp.Application.Common.Validation;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Onboarding;

public record ImportOnboardingClientsCommand(string? CsvContent = null, byte[]? FileBytes = null, string? FileName = null)
    : IRequest<ImportOnboardingClientsResult>;

public record ImportOnboardingClientsResult(int Imported, int Skipped, IReadOnlyList<string> Errors);

/// <summary>Importación CSV/Excel de clientes en onboarding (#41).</summary>
public sealed class ImportOnboardingClientsHandler : IRequestHandler<ImportOnboardingClientsCommand, ImportOnboardingClientsResult>
{
    private readonly IMediator _mediator;
    private readonly ICrmDbContext _crm;
    private readonly ITenantContext _tenant;

    public ImportOnboardingClientsHandler(IMediator mediator, ICrmDbContext crm, ITenantContext tenant)
    {
        _mediator = mediator;
        _crm = crm;
        _tenant = tenant;
    }

    public async Task<ImportOnboardingClientsResult> Handle(ImportOnboardingClientsCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var rows = ParseRows(request);
        if (rows.Count == 0)
            return new ImportOnboardingClientsResult(0, 0, ["No se encontraron filas de datos."]);

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var (row, index) in rows.Select((r, i) => (r, i + 2)))
        {
            var (valid, error) = OnboardingImportParser.ValidateRow(row);
            if (!valid)
            {
                errors.Add($"Línea {index}: {error}");
                skipped++;
                continue;
            }

            var exists = await _crm.Clients.IgnoreQueryFilters()
                .AnyAsync(c => c.CompanyId == companyId && c.TaxId == row.TaxId, ct);
            if (exists) { skipped++; continue; }

            try
            {
                await _mediator.Send(new CreateClientCommand
                {
                    Name = row.Name,
                    TaxId = row.TaxId,
                    Email = row.Email ?? $"import-{Guid.NewGuid():N}@placeholder.local",
                    Phone = row.Phone ?? "",
                    Address = row.Address ?? "",
                }, ct);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Línea {index}: {ex.Message}");
                skipped++;
            }
        }

        return new ImportOnboardingClientsResult(imported, skipped, errors);
    }

    private static IReadOnlyList<OnboardingClientRow> ParseRows(ImportOnboardingClientsCommand request)
    {
        if (request.FileBytes is { Length: > 0 })
        {
            var name = request.FileName?.ToLowerInvariant() ?? "";
            if (name.EndsWith(".xlsx") || name.EndsWith(".xls"))
                return OnboardingImportParser.ParseExcel(request.FileBytes);
        }

        if (!string.IsNullOrWhiteSpace(request.CsvContent))
            return OnboardingImportParser.ParseCsv(request.CsvContent);

        return [];
    }
}
