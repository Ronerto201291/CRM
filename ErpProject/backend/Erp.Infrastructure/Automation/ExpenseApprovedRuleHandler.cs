using System.Globalization;
using Erp.Application.Common.Events;
using MediatR;

namespace Erp.Infrastructure.Automation;

public class ExpenseApprovedRuleHandler : INotificationHandler<ExpenseApprovedEvent>
{
    private readonly RealtimeRuleEvaluator _evaluator;

    public ExpenseApprovedRuleHandler(RealtimeRuleEvaluator evaluator) => _evaluator = evaluator;

    public Task Handle(ExpenseApprovedEvent notification, CancellationToken ct) =>
        _evaluator.EvaluateAsync(
            "OnExpenseApproved",
            notification.CompanyId,
            field => field switch
            {
                "Total" => notification.Total.ToString(CultureInfo.InvariantCulture),
                "TaxBase" => notification.TaxBase.ToString(CultureInfo.InvariantCulture),
                "VATAmount" => notification.VATAmount.ToString(CultureInfo.InvariantCulture),
                "SupplierName" => notification.SupplierName,
                _ => null
            },
            ct);
}
