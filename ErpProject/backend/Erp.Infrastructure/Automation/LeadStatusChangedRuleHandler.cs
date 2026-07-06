using Erp.Application.Common.Events;
using MediatR;

namespace Erp.Infrastructure.Automation;

public class LeadStatusChangedRuleHandler : INotificationHandler<LeadStatusChangedEvent>
{
    private readonly RealtimeRuleEvaluator _evaluator;

    public LeadStatusChangedRuleHandler(RealtimeRuleEvaluator evaluator) => _evaluator = evaluator;

    public Task Handle(LeadStatusChangedEvent notification, CancellationToken ct) =>
        _evaluator.EvaluateAsync(
            "OnLeadStatusChanged",
            notification.CompanyId,
            field => field switch
            {
                "NewStatus" or "Status" => notification.NewStatus,
                "PreviousStatus" => notification.PreviousStatus,
                "LeadName" or "Name" => notification.LeadName,
                _ => null
            },
            ct);
}
