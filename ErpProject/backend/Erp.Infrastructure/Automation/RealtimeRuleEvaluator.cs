using System.Globalization;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Automation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Automation;

/// <summary>
/// Evalúa reglas de automatización en tiempo real ante eventos de dominio.
/// </summary>
public class RealtimeRuleEvaluator
{
    private readonly IApplicationDbContext _app;
    private readonly IEmailService _email;
    private readonly ILogger<RealtimeRuleEvaluator> _logger;

    public RealtimeRuleEvaluator(
        IApplicationDbContext app,
        IEmailService email,
        ILogger<RealtimeRuleEvaluator> logger)
    {
        _app = app;
        _email = email;
        _logger = logger;
    }

    public async Task EvaluateAsync(
        string triggerEvent,
        Guid companyId,
        Func<string, string?> getField,
        CancellationToken ct = default)
    {
        var rules = await _app.Rules
            .IgnoreQueryFilters()
            .Where(r => r.IsActive && r.CompanyId == companyId && r.TriggerEvent == triggerEvent)
            .Include(r => r.Conditions)
            .Include(r => r.Actions)
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var rule in rules)
        {
            if (!RuleConditionEvaluator.MatchesAll(rule.Conditions, getField))
                continue;

            _logger.LogInformation("RealtimeRuleEvaluator: regla '{Name}' activada por {Trigger}", rule.Name, triggerEvent);
            await SendRuleEmailAsync(rule, companyId, triggerEvent, ct);
        }
    }

    private async Task SendRuleEmailAsync(Rule rule, Guid companyId, string triggerEvent, CancellationToken ct)
    {
        var adminUser = await _app.Users
            .IgnoreQueryFilters()
            .Where(u => u.CompanyId == companyId && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (adminUser?.Email is null) return;

        foreach (var action in rule.Actions.OrderBy(a => a.ExecutionOrder))
        {
            if (!string.Equals(action.Type, "SendEmail", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(action.Type, "NotifyAdmin", StringComparison.OrdinalIgnoreCase))
                continue;

            var subject = $"[ERP] Regla '{rule.Name}' — evento {triggerEvent}";
            var body = $"""
                La regla de automatización "{rule.Name}" se ha activado en tiempo real.

                Evento: {triggerEvent}
                Descripción: {rule.Description}

                Este es un mensaje automático del sistema ERP.
                """;

            try
            {
                await _email.SendAsync(adminUser.Email, subject, body, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RealtimeRuleEvaluator: fallo email regla {RuleId}", rule.Id);
            }
        }
    }
}
