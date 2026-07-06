using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Automation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Jobs;

/// <summary>
/// Notificaciones proactivas diarias (#42): facturas vencidas, stock bajo, aprobaciones pendientes.
/// Reutiliza RuleEvaluatorJob + añade gastos/PO pendientes. Respeta frecuencia por empresa.
/// </summary>
public class ProactiveNotificationsJob
{
    private readonly IApplicationDbContext _app;
    private readonly IAutomationExpensesQuery _expenses;
    private readonly IAutomationPurchasingQuery _purchasing;
    private readonly IEmailService _email;
    private readonly IWebPushService _push;
    private readonly RuleEvaluatorJob _ruleEvaluator;
    private readonly ILogger<ProactiveNotificationsJob> _logger;

    public ProactiveNotificationsJob(
        IApplicationDbContext app,
        IAutomationExpensesQuery expenses,
        IAutomationPurchasingQuery purchasing,
        IEmailService email,
        IWebPushService push,
        RuleEvaluatorJob ruleEvaluator,
        ILogger<ProactiveNotificationsJob> logger)
    {
        _app = app;
        _expenses = expenses;
        _purchasing = purchasing;
        _email = email;
        _push = push;
        _ruleEvaluator = ruleEvaluator;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("ProactiveNotificationsJob: inicio {Time}", DateTime.UtcNow);

        var companies = await _app.Companies.IgnoreQueryFilters()
            .Where(c => c.IsActive && c.ProactiveNotificationsFrequency != "disabled")
            .AsNoTracking()
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var eligible = companies.Where(c => ShouldRun(c.ProactiveNotificationsFrequency, now)).ToList();
        if (eligible.Count == 0)
        {
            _logger.LogInformation("ProactiveNotificationsJob: ninguna empresa elegible hoy.");
            return;
        }

        await _ruleEvaluator.EvaluateRulesAsync(ct);
        await NotifyPendingApprovalsAsync(eligible.Select(c => c.Id).ToHashSet(), ct);

        _logger.LogInformation("ProactiveNotificationsJob: completado para {Count} empresas.", eligible.Count);
    }

    public static bool ShouldRun(string frequency, DateTime now) =>
        frequency switch
        {
            "daily" => true,
            "weekly" => now.DayOfWeek == DayOfWeek.Monday,
            _ => false,
        };

    private async Task NotifyPendingApprovalsAsync(HashSet<Guid> eligibleCompanies, CancellationToken ct)
    {
        var expensePending = await _expenses.GetPendingExpenseApprovalsAsync(ct);
        var poPending = await _purchasing.GetPendingPurchaseOrderApprovalsAsync(ct);

        var byCompany = expensePending
            .Select(e => (CompanyId: e.CompanyId, Kind: "Gasto", Label: e.SupplierName ?? e.Id.ToString(), Amount: e.Total))
            .Concat(poPending.Select(p => (p.CompanyId, Kind: "Pedido compra", Label: p.OrderNumber, Amount: p.TotalAmount)))
            .Where(x => eligibleCompanies.Contains(x.CompanyId))
            .GroupBy(x => x.CompanyId);

        foreach (var group in byCompany)
        {
            var company = await _app.Companies.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == group.Key, ct);
            if (company is null) continue;

            var admin = await _app.Users.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(u => u.CompanyId == group.Key && u.IsActive)
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (admin?.Email is null) continue;

            var items = group.ToList();
            var subject = $"[ERP] {items.Count} aprobación(es) pendiente(s) — {company.Name}";
            var lines = items.Select(i => $"  • {i.Kind}: {i.Label} — {i.Amount:F2}€");
            var body = $"""
                Estimado equipo de {company.Name},

                Hay {items.Count} elemento(s) esperando aprobación:

                {string.Join("\n", lines)}

                Acceda al ERP para revisarlos.

                Mensaje automático del sistema ERP.
                """;

            try
            {
                await _email.SendAsync(admin.Email, subject, body, ct);
                if (_push.IsEnabled)
                    await _push.SendToUserAsync(admin.Id, subject, $"{items.Count} aprobación(es) pendiente(s)", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ProactiveNotificationsJob: fallo email aprobaciones {Email}", admin.Email);
            }
        }
    }
}
