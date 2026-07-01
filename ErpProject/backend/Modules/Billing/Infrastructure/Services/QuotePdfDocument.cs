using Erp.Modules.Billing.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Documento PDF de presupuesto (QuestPDF IDocument).
/// Diseño profesional compatible con normativa española:
/// - Incluye NIF del emisor, desglose IVA por tipo, fecha de validez
/// - No incluye hash chain (no es documento fiscal)
/// - Espacio de firma para aceptación manual impresa
/// </summary>
internal sealed class QuotePdfDocument : IDocument
{
    // ── Paleta de colores ─────────────────────────────────────────────────────
    private static readonly string Primary    = "#1B3A6B";
    private static readonly string PrimaryBg  = "#EAF0FB";
    private static readonly string Border     = "#C8D4E0";
    private static readonly string TextMain   = "#1C2B3A";
    private static readonly string TextMuted  = "#6B7A8D";
    private static readonly string Accent     = "#F5A623";  // badge "VÁLIDO"
    private static readonly string AccentGreen = "#27AE60"; // badge "ACEPTADO"
    private static readonly string AccentRed  = "#E53E3E";  // badge "RECHAZADO"

    private readonly QuotePdfData _d;

    public QuotePdfDocument(QuotePdfData data) => _d = data;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Presupuesto {_d.Number}",
        Author = _d.Company.Name,
        Creator = "ERP SaaS",
        Subject = $"Presupuesto {_d.Number} — {_d.Client.Name}",
        Keywords = $"presupuesto,{_d.Number},{_d.FiscalYear},IVA"
    };

    public DocumentSettings GetSettings() => new()
    {
        CompressDocument = true,
        ImageCompressionQuality = ImageCompressionQuality.High
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.4f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(8.5f).FontColor(TextMain));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingTop(8).Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ── CABECERA ─────────────────────────────────────────────────────────────

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            // Banda superior: título + badge de estado
            col.Item().Background(Primary).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("PRESUPUESTO")
                        .FontColor(Colors.White).FontSize(18).Bold();
                    c.Item().Text($"{_d.Number}  ·  v{_d.Version}")
                        .FontColor("#B0C4DE").FontSize(9);
                });

                row.ConstantItem(110).AlignMiddle().AlignRight().Column(c =>
                {
                    var (badgeColor, badgeText) = _d.Status switch
                    {
                        "Draft"    => ("#78909C", "BORRADOR"),
                        "Sent"     => (Accent, "ENVIADO"),
                        "Accepted" => (AccentGreen, "ACEPTADO"),
                        "Rejected" => (AccentRed, "RECHAZADO"),
                        "Expired"  => ("#E53E3E", "EXPIRADO"),
                        "Converted"=> (AccentGreen, "CONVERTIDO"),
                        _          => ("#78909C", _d.Status.ToUpper())
                    };

                    c.Item().Background(badgeColor).Padding(5).AlignCenter()
                        .Text(badgeText).FontColor(Colors.White).FontSize(8).Bold();
                });
            });

            // Datos empresa + cliente + metadatos
            col.Item().Background(PrimaryBg).Padding(10).Row(row =>
            {
                // Empresa (emisor)
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("EMPRESA EMISORA").FontSize(7).FontColor(TextMuted).Bold();
                    c.Item().PaddingTop(3).Text(_d.Company.Name).FontSize(10).Bold();
                    if (!string.IsNullOrEmpty(_d.Company.TaxId))
                        c.Item().Text($"NIF/CIF: {_d.Company.TaxId}").FontSize(8);
                    if (!string.IsNullOrEmpty(_d.Company.Address))
                        c.Item().Text(_d.Company.Address).FontSize(8).FontColor(TextMuted);
                    if (!string.IsNullOrEmpty(_d.Company.Country))
                        c.Item().Text(_d.Company.Country).FontSize(8).FontColor(TextMuted);
                });

                // Separador vertical
                row.ConstantItem(1).Background(Border);

                // Cliente destinatario
                row.RelativeItem().PaddingLeft(12).Column(c =>
                {
                    c.Item().Text("DESTINATARIO").FontSize(7).FontColor(TextMuted).Bold();
                    c.Item().PaddingTop(3).Text(_d.Client.Name).FontSize(10).Bold();
                    if (!string.IsNullOrEmpty(_d.Client.TaxId))
                        c.Item().Text($"NIF/CIF: {_d.Client.TaxId}").FontSize(8);
                    if (!string.IsNullOrEmpty(_d.Client.Email))
                        c.Item().Text(_d.Client.Email).FontSize(8).FontColor(TextMuted);
                    if (!string.IsNullOrEmpty(_d.Client.Address))
                        c.Item().Text(_d.Client.Address).FontSize(8).FontColor(TextMuted);
                });

                // Separador vertical
                row.ConstantItem(1).Background(Border);

                // Metadatos del presupuesto
                row.ConstantItem(140).PaddingLeft(12).Column(c =>
                {
                    c.Item().Text("DETALLES").FontSize(7).FontColor(TextMuted).Bold();
                    c.Item().PaddingTop(3).Row(r =>
                    {
                        r.ConstantItem(60).Text("Número:").FontSize(8).FontColor(TextMuted);
                        r.RelativeItem().Text(_d.Number).FontSize(8).Bold();
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(60).Text("Emisión:").FontSize(8).FontColor(TextMuted);
                        r.RelativeItem().Text(_d.IssueDate.ToString("dd/MM/yyyy")).FontSize(8);
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(60).Text("Válido hasta:").FontSize(8).FontColor(TextMuted);
                        r.RelativeItem().Text(_d.ValidUntil.ToString("dd/MM/yyyy"))
                            .FontSize(8).Bold().FontColor(_d.ValidUntil < DateTime.Today ? AccentRed : Primary);
                    });
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(60).Text("Moneda:").FontSize(8).FontColor(TextMuted);
                        r.RelativeItem().Text(_d.SeriesPrefix + " / EUR").FontSize(8);
                    });
                });
            });

            col.Item().PaddingBottom(6);
        });
    }

    // ── CONTENIDO ────────────────────────────────────────────────────────────

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            // ── Tabla de líneas
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(5);   // Descripción
                    c.RelativeColumn(1.2f); // Unidad
                    c.RelativeColumn(1.2f); // Cantidad
                    c.RelativeColumn(1.8f); // Precio ud.
                    c.RelativeColumn(1.2f); // Dto %
                    c.RelativeColumn(1.5f); // Base imp.
                    c.RelativeColumn(1);    // IVA %
                    c.RelativeColumn(1.8f); // Total
                });

                table.Header(header =>
                {
                    void LH(string text) =>
                        header.Cell().Background(Primary).Padding(5).AlignCenter()
                            .Text(text).FontColor(Colors.White).FontSize(7.5f).Bold();

                    LH("DESCRIPCIÓN");
                    LH("UNID.");
                    LH("CANT.");
                    LH("PRECIO UD.");
                    LH("DTO %");
                    LH("BASE IMP.");
                    LH("IVA %");
                    LH("TOTAL");
                });

                var altBg = PrimaryBg;
                var i = 0;
                foreach (var line in _d.Lines)
                {
                    string bg = i++ % 2 == 0 ? Colors.White : altBg;

                    void Cell(string text, bool right = false, bool bold = false)
                    {
                        var cell = table.Cell().Background(bg).Padding(4);
                        var txt = cell.Text(text).FontSize(8);
                        if (right) txt.AlignRight();
                        if (bold) txt.Bold();
                    }

                    Cell($"{line.Description}{(string.IsNullOrEmpty(line.ProductCode) ? "" : $"\n[{line.ProductCode}]")}");
                    Cell(line.Unit ?? "ud", right: true);
                    Cell(line.Quantity.ToString("N2"), right: true);
                    Cell(line.UnitPrice.ToString("N4") + " €", right: true);
                    Cell(line.DiscountPct > 0 ? $"{line.DiscountPct:N1}%" : "—", right: true);
                    Cell(line.LineTaxBase.ToString("N2") + " €", right: true);
                    Cell($"{line.TaxRate:N0}%", right: true);
                    Cell(line.LineTotalAmount.ToString("N2") + " €", right: true, bold: true);
                }
            });

            col.Item().PaddingTop(12).Row(row =>
            {
                // ── Desglose IVA (izquierda) + Notas (centro)
                row.RelativeItem(3).Column(taxCol =>
                {
                    taxCol.Item().Text("DESGLOSE FISCAL").FontSize(7.5f).Bold().FontColor(TextMuted);
                    taxCol.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                        });

                        void TH(string text) =>
                            t.Cell().Background(Primary).Padding(4).AlignCenter()
                                .Text(text).FontColor(Colors.White).FontSize(7.5f).Bold();

                        TH("TIPO IVA"); TH("BASE IMP."); TH("CUOTA IVA");

                        foreach (var g in _d.TaxGroups)
                        {
                            t.Cell().BorderBottom(0.5f).BorderColor(Border).Padding(4).AlignRight()
                                .Text($"{g.TaxRate:N0}%").FontSize(8);
                            t.Cell().BorderBottom(0.5f).BorderColor(Border).Padding(4).AlignRight()
                                .Text($"{g.BaseAmount:N2} €").FontSize(8);
                            t.Cell().BorderBottom(0.5f).BorderColor(Border).Padding(4).AlignRight()
                                .Text($"{g.TaxAmount:N2} €").FontSize(8);
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(_d.Notes))
                    {
                        taxCol.Item().PaddingTop(10).Text("CONDICIONES Y NOTAS")
                            .FontSize(7.5f).Bold().FontColor(TextMuted);
                        taxCol.Item().PaddingTop(4).Background(PrimaryBg).Padding(8)
                            .Text(_d.Notes).FontSize(8).FontColor(TextMain);
                    }
                });

                row.ConstantItem(16);

                // ── Resumen de totales (derecha)
                row.ConstantItem(200).Column(totals =>
                {
                    void Row(string label, string value, bool big = false, string? color = null)
                    {
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().AlignLeft()
                                .Text(label).FontSize(big ? 9 : 8)
                                .FontColor(color ?? TextMuted);
                            r.ConstantItem(90).AlignRight()
                                .Text(value).FontSize(big ? 9 : 8).Bold()
                                .FontColor(color ?? TextMain);
                        });
                        totals.Item().LineHorizontal(0.5f).LineColor(Border);
                    }

                    totals.Item().Text("RESUMEN").FontSize(7.5f).Bold().FontColor(TextMuted);
                    totals.Item().PaddingTop(6);

                    Row("Subtotal bruto:", $"{_d.SubtotalBeforeDisc:N2} €");

                    if (_d.GlobalDiscountPct > 0)
                        Row($"Descuento global ({_d.GlobalDiscountPct:N1}%):",
                            $"-{_d.GlobalDiscountAmount:N2} €", color: AccentRed);

                    Row("Base imponible:", $"{_d.TaxBaseAmount:N2} €");
                    Row("Total IVA:", $"{_d.TaxAmount:N2} €");

                    totals.Item().PaddingTop(4).Background(Primary).Padding(10).Row(r =>
                    {
                        r.RelativeItem().Text("TOTAL").FontColor(Colors.White).FontSize(11).Bold();
                        r.ConstantItem(90).AlignRight()
                            .Text($"{_d.TotalAmount:N2} €").FontColor(Colors.White).FontSize(14).Bold();
                    });
                });
            });

            // ── Espacio de firma (para impresión)
            col.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("ACEPTACIÓN DEL PRESUPUESTO").FontSize(7.5f).Bold().FontColor(TextMuted);
                    c.Item().PaddingTop(4).Background(PrimaryBg).Padding(12).Row(r =>
                    {
                        r.RelativeItem().Column(sig =>
                        {
                            sig.Item().PaddingBottom(28).Text("Firma y sello del cliente:").FontSize(8);
                            sig.Item().LineHorizontal(1).LineColor(Border);
                            sig.Item().PaddingTop(4).Text("Nombre y DNI/NIF:").FontSize(8);
                        });
                        r.ConstantItem(20);
                        r.RelativeItem().Column(sig =>
                        {
                            sig.Item().PaddingBottom(28).Text("Fecha de aceptación:").FontSize(8);
                            sig.Item().LineHorizontal(1).LineColor(Border);
                            sig.Item().PaddingTop(4).Text("Lugar:").FontSize(8);
                        });
                    });
                });
            });
        });
    }

    // ── PIE DE PÁGINA ─────────────────────────────────────────────────────────

    private void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Border);
            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Presupuesto válido hasta el ").FontSize(7).FontColor(TextMuted);
                    text.Span(_d.ValidUntil.ToString("dd/MM/yyyy")).FontSize(7).Bold().FontColor(Primary);
                    text.Span(" · Salvo error u omisión · Precios en EUR con IVA desglosado conforme a Ley 37/1992")
                        .FontSize(7).FontColor(TextMuted);
                });
                row.ConstantItem(50).AlignRight().Text(text =>
                {
                    text.CurrentPageNumber().FontSize(7).FontColor(TextMuted);
                    text.Span(" / ").FontSize(7).FontColor(TextMuted);
                    text.TotalPages().FontSize(7).FontColor(TextMuted);
                });
            });
        });
    }
}
