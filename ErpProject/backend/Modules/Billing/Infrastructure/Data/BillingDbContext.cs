using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Infrastructure.Data;

public class BillingDbContext : ModuleDbContextBase, IBillingDbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<InvoiceLine> InvoiceLines { get; set; } = null!;
    public DbSet<Quote> Quotes { get; set; } = null!;
    public DbSet<QuoteLine> QuoteLines { get; set; } = null!;
    public DbSet<QuoteStatusHistory> QuoteStatusHistory { get; set; } = null!;
    public DbSet<QuoteNumberSeries> QuoteNumberSeries { get; set; } = null!;
    public DbSet<VerifactuSubmissionLog> VerifactuSubmissionLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("billing");
        base.OnModelCreating(modelBuilder);

        // ── Multi-tenant query filters ─────────────────────────────────────────
        // InvoiceLine/QuoteLine/QuoteStatusHistory have no CompanyId → accessed via parent
        modelBuilder.Entity<Invoice>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Quote>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<QuoteNumberSeries>().HasQueryFilter(s => s.CompanyId == TenantContext.TenantId);

        // ── Invoice configurations ─────────────────────────────────────────────
        modelBuilder.Entity<Invoice>()
            .HasIndex(e => new { e.CompanyId, e.Number }).IsUnique();

        modelBuilder.Entity<Invoice>().HasIndex(e => e.CompanyId);

        // PublicViewToken index para búsquedas del portal público (ADR-0018 #39)
        modelBuilder.Entity<Invoice>()
            .HasIndex(e => e.PublicViewToken).IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.RectifiedInvoice)
            .WithMany()
            .HasForeignKey(i => i.RectifiedInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceLine>()
            .HasOne(l => l.Invoice)
            .WithMany(i => i.InvoiceLines)
            .HasForeignKey(l => l.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Quote configurations ───────────────────────────────────────────────
        // Unique number per company (mismo patrón que facturas)
        modelBuilder.Entity<Quote>()
            .HasIndex(q => new { q.CompanyId, q.Number }).IsUnique();

        modelBuilder.Entity<Quote>().HasIndex(q => q.CompanyId);

        // AcceptanceToken index para búsquedas del portal público
        modelBuilder.Entity<Quote>()
            .HasIndex(q => q.AcceptanceToken).IsUnique();

        // TaxBreakdown como JSONB nativo de PostgreSQL
        modelBuilder.Entity<Quote>()
            .Property(q => q.TaxBreakdown).HasColumnType("jsonb");

        // Self-reference: versionado de presupuestos
        modelBuilder.Entity<Quote>()
            .HasOne(q => q.ParentQuote)
            .WithMany()
            .HasForeignKey(q => q.ParentQuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        // UnitPrice con precision extendida para B2B (6 decimales)
        modelBuilder.Entity<QuoteLine>()
            .Property(l => l.UnitPrice).HasPrecision(18, 6);

        // QuoteLine → Quote FK (cascade delete: si se borra el presupuesto, se borran las líneas)
        modelBuilder.Entity<QuoteLine>()
            .HasOne(l => l.Quote)
            .WithMany(q => q.Lines)
            .HasForeignKey(l => l.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuoteLine>().HasIndex(l => l.QuoteId);

        // QuoteStatusHistory → Quote FK (cascade delete)
        modelBuilder.Entity<QuoteStatusHistory>()
            .HasOne(h => h.Quote)
            .WithMany(q => q.StatusHistory)
            .HasForeignKey(h => h.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuoteStatusHistory>().HasIndex(h => h.QuoteId);

        // Metadata de evidencia de aceptación como JSONB
        modelBuilder.Entity<QuoteStatusHistory>()
            .Property(h => h.Metadata).HasColumnType("jsonb");

        // QuoteNumberSeries: único por (CompanyId, Year, Prefix)
        modelBuilder.Entity<QuoteNumberSeries>()
            .HasIndex(s => new { s.CompanyId, s.Year, s.Prefix }).IsUnique();

        modelBuilder.Entity<VerifactuSubmissionLog>()
            .HasIndex(l => new { l.CompanyId, l.InvoiceId });

        // Soft cross-module references (columnas sin FK EF, aplicadas en application layer)
        // Quote.ClientId → CRM.Clients / Leads
        // QuoteLine.ProductId → Inventory.Products
        // Quote.ConvertedToInvoiceId → billing.Invoices (mismo módulo, pero soft para evitar ciclos)
    }
}
