using Erp.Modules.Billing.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Implementación de IInvoicePdfService usando QuestPDF (motor SkiaSharp, sin dependencias nativas).
/// Conforme a RD 1619/2012 y Ley 11/2021 Antifraude.
/// </summary>
public sealed class InvoicePdfService : IInvoicePdfService
{
    public InvoicePdfService()
    {
        // QuestPDF Community License: gratuita para ingresos < 1M$/año.
        // Para ingresos superiores, adquirir licencia Professional/Enterprise en questpdf.com
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(InvoicePdfData data)
    {
        // GeneratePdf() es un extension method sobre IDocument (QuestPDF.Fluent)
        IDocument document = new InvoicePdfDocument(data);
        return document.GeneratePdf();
    }
}
