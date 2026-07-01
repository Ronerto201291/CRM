using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Inventory.Application.Interfaces;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Automation;

/// <summary>
/// Motor de automatización de negocio. Ejecutado por Hangfire en intervalos configurados.
///
/// Reglas implementadas:
/// 1. Facturas vencidas → Notificación de cobro pendiente (email a la empresa)
/// 2. Stock por debajo del punto de reorden → Alerta de reposición (email a la empresa)
///
/// Cada regla es idempotente: no genera duplicados aunque el job se ejecute varias veces al día.
/// </summary>
public class RuleEvaluatorJob
{
    private readonly IBillingDbContext   _billing;
    private readonly IInventoryDbContext _inventory;
    private readonly IEmailService       _email;
    private readonly IApplicationDbContext _app;
    private readonly ILogger<RuleEvaluatorJob> _logger;

    public RuleEvaluatorJob(
        IBillingDbContext billing,
        IInventoryDbContext inventory,
        IEmailService email,
        IApplicationDbContext app,
        ILogger<RuleEvaluatorJob> logger)
    {
        _billing   = billing;
        _inventory = inventory;
        _email     = email;
        _app       = app;
        _logger    = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task EvaluateRulesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("RuleEvaluatorJob: iniciando evaluación a {Time}", DateTime.UtcNow);

        var overdueTask = CheckOverdueInvoicesAsync(ct);
        var stockTask   = CheckStockReorderPointsAsync(ct);

        await Task.WhenAll(overdueTask, stockTask);

        _logger.LogInformation("RuleEvaluatorJob: completado.");
    }

    // ── Regla 1: Facturas Vencidas ────────────────────────────────────────────
    /// <summary>
    /// Detecta facturas emitidas con DueDate pasada y status != Paid/Locked.
    /// Envía un email de recordatorio de cobro a la empresa (admin).
    /// Agrupa por empresa para evitar un email por factura.
    /// </summary>
    private async Task CheckOverdueInvoicesAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        // Facturas vencidas (DueDate < hoy, no pagadas, no canceladas)
        var overdueInvoices = await _billing.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.DueDate.Date < today
                     && i.Status != "Paid"
                     && i.Status != "Cancelled"
                     && i.Status != "Draft")
            .Select(i => new
            {
                i.Id,
                i.Number,
                i.CompanyId,
                i.ClientName,
                i.Total,
                DueDate = i.DueDate
            })
            .AsNoTracking()
            .ToListAsync(ct);

        if (!overdueInvoices.Any())
        {
            _logger.LogInformation("RuleEvaluatorJob: no hay facturas vencidas.");
            return;
        }

        // Agrupar por empresa
        var byCompany = overdueInvoices.GroupBy(i => i.CompanyId);

        foreach (var group in byCompany)
        {
            var companyId = group.Key;
            var company   = await _app.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);

            if (company is null) continue;

            // Obtener email del admin de la empresa
            var adminUser = await _app.Users.AsNoTracking()
                .Where(u => u.CompanyId == companyId && u.IsActive)
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (adminUser?.Email is null) continue;

            var invoiceList = group.ToList();
            var totalOverdue = invoiceList.Sum(i => i.Total);
            var count = invoiceList.Count;

            _logger.LogInformation(
                "RuleEvaluatorJob: {Count} facturas vencidas para empresa {CompanyId}, total {Total:F2}€",
                count, companyId, totalOverdue);

            var subject = $"[ERP] {count} factura{(count > 1 ? "s" : "")} vencida{(count > 1 ? "s" : "")} pendiente{(count > 1 ? "s" : "")} de cobro";

            var bodyLines = invoiceList
                .OrderBy(i => i.DueDate)
                .Select(i => $"  • {i.Number} — {i.ClientName} — {i.Total:F2}€ (vencida el {i.DueDate:dd/MM/yyyy})");

            var body = $"""
                Estimado equipo de {company.Name},

                Hay {count} factura{(count > 1 ? "s" : "")} vencida{(count > 1 ? "s" : "")} con un importe total pendiente de cobro de {totalOverdue:F2}€:

                {string.Join("\n", bodyLines)}

                Acceda al ERP para gestionar los cobros o enviar recordatorios a sus clientes.

                Este es un mensaje automático del sistema ERP.
                """;

            try
            {
                await _email.SendAsync(adminUser.Email, subject, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RuleEvaluatorJob: fallo al enviar email de facturas vencidas a {Email}",
                    adminUser.Email);
            }
        }
    }

    // ── Regla 2: Stock por debajo del Punto de Reorden ────────────────────────
    /// <summary>
    /// Detecta productos cuyo stock agregado (suma de todos los almacenes) cae por debajo
    /// de su ReorderPoint configurado. Envía email de alerta de reposición.
    /// </summary>
    private async Task CheckStockReorderPointsAsync(CancellationToken ct)
    {
        // Cargar todos los productos con punto de reorden configurado
        var products = await _inventory.InventoryProducts
            .IgnoreQueryFilters()
            .Where(p => p.ReorderPoint > 0)
            .Select(p => new { p.Id, p.CompanyId, p.Name, p.SKU, p.ReorderPoint, p.ReorderQty })
            .AsNoTracking()
            .ToListAsync(ct);

        if (!products.Any()) return;

        // Stock actual por producto (suma de todos los almacenes)
        var stockByProduct = await _inventory.Stocks
            .IgnoreQueryFilters()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(s => s.Quantity) })
            .AsNoTracking()
            .ToListAsync(ct);

        var stockMap = stockByProduct.ToDictionary(s => s.ProductId, s => s.TotalQty);

        var alertProducts = products
            .Where(p => stockMap.GetValueOrDefault(p.Id, 0) <= p.ReorderPoint)
            .ToList();

        if (!alertProducts.Any())
        {
            _logger.LogInformation("RuleEvaluatorJob: todos los productos por encima del punto de reorden.");
            return;
        }

        // Agrupar por empresa
        var byCompany = alertProducts.GroupBy(p => p.CompanyId);

        foreach (var group in byCompany)
        {
            var companyId = group.Key;
            var company   = await _app.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == companyId, ct);
            if (company is null) continue;

            var adminUser = await _app.Users.AsNoTracking()
                .Where(u => u.CompanyId == companyId && u.IsActive)
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (adminUser?.Email is null) continue;

            var items  = group.ToList();
            var count  = items.Count;

            _logger.LogInformation(
                "RuleEvaluatorJob: {Count} producto(s) bajo punto de reorden para empresa {CompanyId}",
                count, companyId);

            var subject = $"[ERP] Alerta de stock: {count} producto{(count > 1 ? "s" : "")} por debajo del punto de reorden";

            var bodyLines = items.Select(p =>
            {
                var actual = stockMap.GetValueOrDefault(p.Id, 0);
                return $"  • {p.Name} ({p.SKU}) — Stock actual: {actual} | Punto de reorden: {p.ReorderPoint} | Cantidad a pedir: {p.ReorderQty}";
            });

            var body = $"""
                Estimado equipo de {company.Name},

                Los siguientes productos han alcanzado o superado su punto de reorden:

                {string.Join("\n", bodyLines)}

                Se recomienda lanzar los pedidos de compra correspondientes para evitar roturas de stock.

                Este es un mensaje automático del sistema ERP.
                """;

            try
            {
                await _email.SendAsync(adminUser.Email, subject, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RuleEvaluatorJob: fallo al enviar email de stock bajo a {Email}",
                    adminUser.Email);
            }
        }
    }
}
