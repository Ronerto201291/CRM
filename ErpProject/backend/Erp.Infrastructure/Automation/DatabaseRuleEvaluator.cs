using System.Globalization;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace Erp.Infrastructure.Automation;

/// <summary>
/// Evalúa reglas personalizadas persistidas en Rule/Condition/Action.
/// </summary>
internal class DatabaseRuleEvaluator
{
    private readonly IApplicationDbContext _app;
    private readonly IAutomationBillingQuery _billing;
    private readonly IAutomationInventoryQuery _inventory;
    private readonly IEmailService _email;
    private readonly ILogger _logger;

    public DatabaseRuleEvaluator(
        IApplicationDbContext app,
        IAutomationBillingQuery billing,
        IAutomationInventoryQuery inventory,
        IEmailService email,
        ILogger logger)
    {
        _app = app;
        _billing = billing;
        _inventory = inventory;
        _email = email;
        _logger = logger;
    }
    public async Task EvaluateAsync(CancellationToken ct)
    {
        var rules = await _app.Rules
            .IgnoreQueryFilters()
            .Where(r => r.IsActive)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .AsNoTracking()
            .ToListAsync(ct);

        if (rules.Count == 0)
        {
            _logger.LogInformation("RuleEvaluatorJob: no hay reglas personalizadas activas en BD.");
            return;
        }

        foreach (var rule in rules)
        {
            try
            {
                var matchCount = await EvaluateRuleAsync(rule, ct);
                if (matchCount > 0)
                    await ExecuteActionsAsync(rule, matchCount, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RuleEvaluatorJob: fallo evaluando regla {RuleId} ({Name})", rule.Id, rule.Name);
            }
        }
    }

    private async Task<int> EvaluateRuleAsync(Rule rule, CancellationToken ct)
    {
        return rule.TriggerEvent switch
        {
            "OnStockBelowReorder" => await EvaluateStockRuleAsync(rule, ct),
            "OnInvoiceCreated" or "OnInvoiceOverdue" => await EvaluateInvoiceRuleAsync(rule, ct),
            _ => await EvaluateInvoiceRuleAsync(rule, ct)
        };
    }

    private async Task<int> EvaluateStockRuleAsync(Rule rule, CancellationToken ct)
    {
        var products = await _inventory.GetProductsBelowReorderForCompanyAsync(rule.CompanyId, ct);
        var matches = products.Where(p =>
            RuleConditionEvaluator.MatchesAll(rule.Conditions, field => field switch
            {
                "Stock" or "Quantity" or "StockQty" => p.CurrentStock.ToString(CultureInfo.InvariantCulture),
                "ReorderPoint" => p.ReorderPoint.ToString(CultureInfo.InvariantCulture),
                "SKU" => p.SKU,
                "Name" => p.Name,
                _ => null
            })).ToList();
        if (matches.Count > 0)
        {
            _logger.LogInformation(
                "RuleEvaluatorJob: regla '{Name}' ({RuleId}) — {Count} producto(s) coinciden",
                rule.Name, rule.Id, matches.Count);
        }

        return matches.Count;
    }

    private async Task<int> EvaluateInvoiceRuleAsync(Rule rule, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var invoices = await _billing.GetInvoicesForRuleAsync(rule.CompanyId, rule.TriggerEvent, today, ct);

        var matches = invoices.Where(i =>            RuleConditionEvaluator.MatchesAll(rule.Conditions, field => field switch
            {
                "Total" => i.Total.ToString(CultureInfo.InvariantCulture),
                "Subtotal" or "SubTotal" => i.Subtotal.ToString(CultureInfo.InvariantCulture),
                "TaxAmount" => i.TaxAmount.ToString(CultureInfo.InvariantCulture),
                "Status" => i.Status,
                "Number" => i.Number,
                "ClientName" => i.ClientName,
                _ => null
            })).ToList();

        if (matches.Count > 0)
        {
            _logger.LogInformation(
                "RuleEvaluatorJob: regla '{Name}' ({RuleId}) — {Count} factura(s) coinciden",
                rule.Name, rule.Id, matches.Count);
        }

        return matches.Count;
    }

    private async Task ExecuteActionsAsync(Rule rule, int matchCount, CancellationToken ct)
    {
        var adminUser = await _app.Users
            .IgnoreQueryFilters()
            .Where(u => u.CompanyId == rule.CompanyId && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (adminUser?.Email is null) return;

        foreach (var action in rule.Actions.OrderBy(a => a.ExecutionOrder))
        {
            if (!string.Equals(action.Type, "SendEmail", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(action.Type, "NotifyAdmin", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("RuleEvaluatorJob: acción {Type} no implementada para regla {RuleId}", action.Type, rule.Id);
                continue;
            }

            var subject = $"[ERP] Regla '{rule.Name}' activada ({matchCount} coincidencia{(matchCount > 1 ? "s" : "")})";
            var body = $"""
                La regla de automatización "{rule.Name}" se ha activado.

                Evento: {rule.TriggerEvent}
                Coincidencias detectadas: {matchCount}
                Descripción: {rule.Description}

                Revise el ERP para tomar las acciones correspondientes.

                Este es un mensaje automático del sistema ERP.
                """;

            try
            {
                await _email.SendAsync(adminUser.Email, subject, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RuleEvaluatorJob: fallo al enviar email de regla {RuleId} a {Email}", rule.Id, adminUser.Email);
            }
        }
    }
}
