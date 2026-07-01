using Erp.Modules.Payroll.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Erp.Modules.Payroll.Application.Interfaces;

public interface IPayrollDbContext
{
    DbSet<Employee> Employees { get; }
    DbSet<PayrollSettlement> PayrollSettlements { get; }
    DbSet<PayrollLine> PayrollLines { get; }
    DbSet<PayrollDeduction> PayrollDeductions { get; }
    DbSet<SocialSecurityContribution> SocialSecurityContributions { get; }
    DbSet<TaxableBase> TaxableBases { get; }
    DbSet<PayrollTemplate> PayrollTemplates { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
