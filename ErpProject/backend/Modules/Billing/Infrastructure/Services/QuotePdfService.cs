using Erp.Modules.Billing.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Implementación de IQuotePdfService usando QuestPDF.
/// El presupuesto no es documento fiscal — no requiere hash chain ni Verifactu.
/// </summary>
public sealed class QuotePdfService : IQuotePdfService
{
    public QuotePdfService()
    {
        // QuestPDF License ya configurada por InvoicePdfService (singleton compartido)
        // Si QuotePdfService se registra antes, forzamos aquí también
        if (QuestPDF.Settings.License == LicenseType.Community) return;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(QuotePdfData data)
    {
        IDocument document = new QuotePdfDocument(data);
        return document.GeneratePdf();
    }
}
