using Erp.Application.Common.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Application.Features.Employees;

public record EmployeeListItemDto(
    Guid Id, string TaxId, string FullName, string? SocialSecurityNumber,
    DateTime HireDate, string ContractType, decimal WeeklyHours, bool IsActive);

public record GetEmployeesQuery : IRequest<IReadOnlyList<EmployeeListItemDto>>;

public record CreateEmployeeCommand(
    string TaxId, string FullName, string? SocialSecurityNumber,
    DateTime? HireDate, string? ContractType, decimal? WeeklyHours) : IRequest<CreateEmployeeResult>;

public record CreateEmployeeResult(Guid Id);

public class GetEmployeesHandler : IRequestHandler<GetEmployeesQuery, IReadOnlyList<EmployeeListItemDto>>
{
    private readonly IPayrollDbContext _ctx;

    public GetEmployeesHandler(IPayrollDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<EmployeeListItemDto>> Handle(GetEmployeesQuery request, CancellationToken ct)
    {
        return await _ctx.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeListItemDto(
                e.Id, e.TaxId, e.FullName, e.SocialSecurityNumber, e.HireDate,
                e.ContractType, e.WeeklyHours, e.IsActive))
            .ToListAsync(ct);
    }
}

public class CreateEmployeeHandler : IRequestHandler<CreateEmployeeCommand, CreateEmployeeResult>
{
    private readonly IPayrollDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateEmployeeHandler(IPayrollDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CreateEmployeeResult> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TaxId) || string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("TaxId y FullName son obligatorios.");

        var e = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved"),
            TaxId = request.TaxId.Trim(),
            FullName = request.FullName.Trim(),
            SocialSecurityNumber = request.SocialSecurityNumber?.Trim(),
            HireDate = request.HireDate ?? DateTime.UtcNow,
            ContractType = request.ContractType ?? "Indefinido",
            WeeklyHours = request.WeeklyHours ?? 40m,
            IsActive = true,
        };

        _ctx.Employees.Add(e);
        await _ctx.SaveChangesAsync(ct);
        return new CreateEmployeeResult(e.Id);
    }
}
