using Erp.Application.Common.Interfaces;

using Erp.Modules.Expenses.Application.Features.Expenses.Queries;

using Erp.Modules.Expenses.Application.Interfaces;

using MediatR;

using Microsoft.EntityFrameworkCore;



namespace Erp.Modules.Expenses.Application.Features.Expenses.Handlers;



/// <summary>Detección heurística de anomalías en gastos (ADR-0018 #40) + resumen IA opcional.</summary>

public sealed class GetExpenseAnomaliesHandler : IRequestHandler<GetExpenseAnomaliesQuery, ExpenseAnomaliesDto>

{

    private readonly IExpensesDbContext _ctx;

    private readonly IExpenseAiAssistant? _ai;



    public GetExpenseAnomaliesHandler(IExpensesDbContext ctx, IExpenseAiAssistant? ai = null)

    {

        _ctx = ctx;

        _ai = ai;

    }



    public async Task<ExpenseAnomaliesDto> Handle(GetExpenseAnomaliesQuery request, CancellationToken ct)

    {

        var docs = await _ctx.ExpenseDocuments

            .Include(e => e.Lines)

            .AsNoTracking()

            .Where(e => e.Total.HasValue && e.Total > 0)

            .ToListAsync(ct);



        var outliers = new List<ExpenseAnomalyItemDto>();

        var duplicates = new List<ExpenseAnomalyItemDto>();



        var byCategory = docs.GroupBy(Categorize);

        foreach (var group in byCategory)

        {

            if (group.Key == "_unknown" || group.Count() < 2) continue;

            var avg = group.Average(e => e.Total!.Value);

            foreach (var doc in group.Where(e => e.Total!.Value > avg * 2))

            {

                outliers.Add(new ExpenseAnomalyItemDto(

                    doc.Id, doc.InvoiceNumber, doc.SupplierName, doc.Total!.Value,

                    doc.IssueDate, "AmountOutlier",

                    $"Importe {doc.Total:F2}€ > 2× media categoría «{group.Key}» ({avg:F2}€)"));

            }

        }



        var dupGroups = docs

            .Where(e => e.IssueDate.HasValue)

            .GroupBy(e => (

                Supplier: NormalizeSupplier(e),

                Date: e.IssueDate!.Value.Date,

                Amount: Math.Round(e.Total!.Value, 2)))

            .Where(g => g.Count() > 1);



        foreach (var g in dupGroups)

        {

            foreach (var doc in g)

            {

                duplicates.Add(new ExpenseAnomalyItemDto(

                    doc.Id, doc.InvoiceNumber, doc.SupplierName, doc.Total!.Value,

                    doc.IssueDate, "Duplicate",

                    $"Posible duplicado: mismo proveedor, fecha e importe ({g.Count()} coincidencias)"));

            }

        }



        string? aiSummary = null;

        if (_ai?.IsEnabled == true && (outliers.Count > 0 || duplicates.Count > 0))

        {

            var samples = outliers.Concat(duplicates).Select(a => a.Message).ToList();

            aiSummary = await _ai.SummarizeAnomaliesAsync(outliers.Count, duplicates.Count, samples, ct);

        }



        return new ExpenseAnomaliesDto(outliers, duplicates, aiSummary);

    }



    private static string Categorize(Domain.Entities.ExpenseDocument e)

    {

        if (!string.IsNullOrWhiteSpace(e.SupplierName))

            return e.SupplierName.Trim().ToLowerInvariant();

        var firstLine = e.Lines.OrderBy(l => l.SortOrder).FirstOrDefault()?.Description;

        return string.IsNullOrWhiteSpace(firstLine) ? "_unknown" : firstLine.Trim().ToLowerInvariant();

    }



    private static string NormalizeSupplier(Domain.Entities.ExpenseDocument e) =>

        (e.SupplierTaxId ?? e.SupplierName ?? e.Id.ToString()).Trim().ToLowerInvariant();

}



/// <summary>Sugerencia de cuenta contable por histórico/reglas + IA opcional (#40).</summary>

public sealed class SuggestExpenseCategoryHandler : IRequestHandler<SuggestExpenseCategoryQuery, ExpenseCategorySuggestionDto?>

{

    private readonly IExpensesDbContext _ctx;

    private readonly IExpenseAiAssistant? _ai;



    private static readonly Dictionary<string, string> KeywordRules = new(StringComparer.OrdinalIgnoreCase)

    {

        ["combustible"] = "628", ["gasolina"] = "628", ["gas"] = "628", ["electricidad"] = "628",

        ["alquiler"] = "621", ["arrendamiento"] = "621",

        ["seguro"] = "625", ["prima"] = "625",

        ["publicidad"] = "627", ["marketing"] = "627",

        ["transporte"] = "624", ["taxi"] = "624", ["envio"] = "624",

        ["banco"] = "626", ["comision"] = "626",

        ["abogado"] = "623", ["consultor"] = "623", ["asesor"] = "623",

        ["mercaderia"] = "600", ["compra"] = "600", ["material"] = "601",

    };



    private static readonly Dictionary<string, string> AccountNames = new()

    {

        ["600"] = "Compras de mercaderías", ["601"] = "Compras de materias primas",

        ["621"] = "Arrendamientos y cánones", ["622"] = "Reparaciones y conservación",

        ["623"] = "Servicios profesionales", ["624"] = "Transportes",

        ["625"] = "Primas de seguros", ["626"] = "Servicios bancarios",

        ["627"] = "Publicidad", ["628"] = "Suministros", ["629"] = "Otros servicios",

    };



    public SuggestExpenseCategoryHandler(IExpensesDbContext ctx, IExpenseAiAssistant? ai = null)

    {

        _ctx = ctx;

        _ai = ai;

    }



    public async Task<ExpenseCategorySuggestionDto?> Handle(SuggestExpenseCategoryQuery request, CancellationToken ct)

    {

        if (_ai?.IsEnabled == true)

        {

            var aiCode = await _ai.SuggestAccountCodeAsync(request.Description, request.SupplierTaxId, ct);

            if (aiCode is not null && AccountNames.ContainsKey(aiCode))

                return BuildSuggestion(aiCode, "Sugerencia IA", 0.85m);

        }



        if (request.SupplierId.HasValue || !string.IsNullOrWhiteSpace(request.SupplierTaxId))

        {

            var history = await _ctx.ExpenseDocuments

                .Include(e => e.Lines)

                .AsNoTracking()

                .Where(e => e.Status == "Approved"

                    && (request.SupplierId.HasValue && e.SupplierId == request.SupplierId

                        || request.SupplierTaxId != null && e.SupplierTaxId == request.SupplierTaxId))

                .OrderByDescending(e => e.ValidatedAt)

                .Take(20)

                .ToListAsync(ct);



            if (history.Count > 0)

            {

                var entries = await _ctx.AccountingEntries

                    .AsNoTracking()

                    .Where(a => history.Select(h => h.Id).Contains(a.ExpenseDocumentId))

                    .ToListAsync(ct);



                if (entries.Count > 0)

                {

                    var topAccount = entries.GroupBy(e => e.AccountDebit)

                        .OrderByDescending(g => g.Count())

                        .First().Key;

                    return BuildSuggestion(topAccount, "Histórico proveedor", 0.9m);

                }



                var keyword = history.SelectMany(h => h.Lines)

                    .Select(l => l.Description ?? "")

                    .FirstOrDefault(d => KeywordRules.Keys.Any(k => d.Contains(k, StringComparison.OrdinalIgnoreCase)));

                if (keyword != null)

                {

                    var rule = KeywordRules.First(k => keyword.Contains(k.Key, StringComparison.OrdinalIgnoreCase));

                    return BuildSuggestion(rule.Value, $"Regla «{rule.Key}» en histórico", 0.75m);

                }

            }

        }



        if (!string.IsNullOrWhiteSpace(request.Description))

        {

            var match = KeywordRules.FirstOrDefault(k =>

                request.Description.Contains(k.Key, StringComparison.OrdinalIgnoreCase));

            if (match.Key != null)

                return BuildSuggestion(match.Value, $"Regla descripción «{match.Key}»", 0.6m);

        }



        return BuildSuggestion("629", "Cuenta por defecto", 0.3m);

    }



    private static ExpenseCategorySuggestionDto BuildSuggestion(string code, string reason, decimal confidence) =>

        new(code, AccountNames.GetValueOrDefault(code, "Gasto"), reason, confidence);

}

