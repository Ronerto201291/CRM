using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Data;
using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Infrastructure.Data;

public class PayrollDbContext : ModuleDbContextBase, IPayrollDbContext
{
    public PayrollDbContext(DbContextOptions<PayrollDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollSettlement> PayrollSettlements => Set<PayrollSettlement>();
    public DbSet<PayrollLine> PayrollLines => Set<PayrollLine>();
    public DbSet<PayrollDeduction> PayrollDeductions => Set<PayrollDeduction>();
    public DbSet<SocialSecurityContribution> SocialSecurityContributions => Set<SocialSecurityContribution>();
    public DbSet<TaxableBase> TaxableBases => Set<TaxableBase>();
    public DbSet<PayrollTemplate> PayrollTemplates => Set<PayrollTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payroll");
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Employee>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<PayrollSettlement>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<PayrollLine>().HasQueryFilter(l =>
            l.Settlement != null && l.Settlement.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<SocialSecurityContribution>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<TaxableBase>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<PayrollTemplate>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        modelBuilder.Entity<Employee>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.TaxId });
            e.Property(x => x.WeeklyHours).HasPrecision(6, 2);
        });

        modelBuilder.Entity<PayrollSettlement>(e =>
        {
            e.HasIndex(x => new { x.CompanyId, x.Year, x.Month }).IsUnique();
        });

        modelBuilder.Entity<PayrollLine>(e =>
        {
            e.HasOne(l => l.Settlement)
                .WithMany(s => s.Lines)
                .HasForeignKey(l => l.PayrollSettlementId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Employee)
                .WithMany(emp => emp.PayrollLines)
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.GrossSalary).HasPrecision(18, 4);
            e.Property(x => x.CommonContingenciesBase).HasPrecision(18, 4);
            e.Property(x => x.EmployeeSocialSecurity).HasPrecision(18, 4);
            e.Property(x => x.EmployerSocialSecurity).HasPrecision(18, 4);
            e.Property(x => x.IrpfBase).HasPrecision(18, 4);
            e.Property(x => x.IrpfRate).HasPrecision(8, 4);
            e.Property(x => x.IrpfWithheld).HasPrecision(18, 4);
            e.Property(x => x.NetPay).HasPrecision(18, 4);
        });
    }
}
