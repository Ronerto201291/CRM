using Erp.Application.Common.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Application.Services;
using Erp.Modules.Payroll.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Application.Features.Templates;

public record PayrollTemplateDto(
    Guid Id, string Name, string? Description, string DefaultContractType,
    decimal DefaultWeeklyHours, decimal EmployeeSsRatePercent, decimal EmployerSsRatePercent,
    decimal DefaultIrpfRatePercent, bool IsDefault, bool IsActive);

public record GetPayrollTemplatesQuery : IRequest<IReadOnlyList<PayrollTemplateDto>>;

public record CreatePayrollTemplateCommand(
    string Name, string? Description, string? DefaultContractType,
    decimal? DefaultWeeklyHours, decimal? EmployeeSsRatePercent,
    decimal? EmployerSsRatePercent, decimal? DefaultIrpfRatePercent,
    bool IsDefault) : IRequest<CreatePayrollTemplateResult>;

public record CreatePayrollTemplateResult(Guid Id);

public class GetPayrollTemplatesHandler : IRequestHandler<GetPayrollTemplatesQuery, IReadOnlyList<PayrollTemplateDto>>
{
    private readonly IPayrollDbContext _ctx;

    public GetPayrollTemplatesHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<PayrollTemplateDto>> Handle(GetPayrollTemplatesQuery request, CancellationToken ct)
        => await _ctx.PayrollTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new PayrollTemplateDto(
                t.Id, t.Name, t.Description, t.DefaultContractType,
                t.DefaultWeeklyHours, t.EmployeeSsRatePercent, t.EmployerSsRatePercent,
                t.DefaultIrpfRatePercent, t.IsDefault, t.IsActive))
            .ToListAsync(ct);
}

public class CreatePayrollTemplateHandler : IRequestHandler<CreatePayrollTemplateCommand, CreatePayrollTemplateResult>
{
    private readonly IPayrollDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreatePayrollTemplateHandler(IPayrollDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CreatePayrollTemplateResult> Handle(CreatePayrollTemplateCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Nombre obligatorio.");

        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        if (request.IsDefault)
        {
            var existing = await _ctx.PayrollTemplates.Where(t => t.IsDefault).ToListAsync(ct);
            foreach (var t in existing) t.IsDefault = false;
        }

        var template = new PayrollTemplate
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DefaultContractType = request.DefaultContractType?.Trim() ?? "Indefinido",
            DefaultWeeklyHours = request.DefaultWeeklyHours ?? 40m,
            EmployeeSsRatePercent = request.EmployeeSsRatePercent ?? 6.35m,
            EmployerSsRatePercent = request.EmployerSsRatePercent ?? 30m,
            DefaultIrpfRatePercent = request.DefaultIrpfRatePercent ?? 15m,
            IsDefault = request.IsDefault,
        };

        _ctx.PayrollTemplates.Add(template);
        await _ctx.SaveChangesAsync(ct);
        return new CreatePayrollTemplateResult(template.Id);
    }
}
