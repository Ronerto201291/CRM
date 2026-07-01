using Erp.Modules.Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Interfaces;

public interface ICrmDbContext
{
    DbSet<Client> Clients { get; }
    DbSet<Lead> Leads { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<ActivityLog> ActivityLogs { get; }
    DbSet<CrmNote> Notes { get; }
    DbSet<ScheduledAlert> ScheduledAlerts { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
