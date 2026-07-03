using System.Globalization;
using Erp.Domain.Entities.Automation;

namespace Erp.Infrastructure.Automation;

internal static class RuleConditionEvaluator
{
    public static bool MatchesAll(IEnumerable<Condition> conditions, Func<string, string?> getField)
    {
        var list = conditions.ToList();
        if (list.Count == 0) return true;

        foreach (var c in list)
        {
            if (!Matches(getField(c.Field), c.Operator, c.Value))
                return false;
        }
        return true;
    }

    public static bool Matches(string? actual, string op, string expected)
    {
        if (string.IsNullOrWhiteSpace(op)) return true;

        var normalizedOp = op.Trim();
        if (decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var actualNum)
            && decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedNum))
        {
            return normalizedOp switch
            {
                ">" or "gt" => actualNum > expectedNum,
                ">=" or "gte" => actualNum >= expectedNum,
                "<" or "lt" => actualNum < expectedNum,
                "<=" or "lte" => actualNum <= expectedNum,
                "==" or "=" or "eq" => actualNum == expectedNum,
                "!=" or "<>" or "ne" => actualNum != expectedNum,
                _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
            };
        }

        return normalizedOp switch
        {
            "==" or "=" or "eq" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "!=" or "<>" or "ne" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "contains" => (actual ?? "").Contains(expected, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
        };
    }
}
