using Erp.Modules.Billing.Application.Interfaces;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Documento QuestPDF para facturas españolas.
/// Cumple RD 1619/2012 (requisitos formales), Ley 11/2021 Antifraude y RD 1007/2023 Verifactu.
/// </summary>
internal sealed class InvoicePdfDocument : IDocument
{
    // ── Paleta corporativa ─────────────────────────────────────────────────────
    private static readonly string ColorPrimary    = "#1B3A6B"; // Azul corporativo oscuro
    private static readonly string ColorPrimaryBg  = "#EAF0FB"; // Fondo azul muy claro
    private static readonly string ColorBorder     = "#C8D4E0";
    private static readonly string ColorText       = "#1C2B3A";
    private static readonly string ColorMuted      = "#6B7A8D";
    private static readonly string ColorWarning    = "#F5A623"; // Badge Rectificativa
    private static readonly string ColorSuccess    = "#27AE60"; // Badge Bloqueada

    private readonly InvoicePdfData _d;

    public InvoicePdfDocument(InvoicePdfData data) => _d = data;

    public DocumentMetadata GetMetadata() => new()
    {
        Title    = $"Factura {_d.Number}",
        Author   = _d.Company.Name,
        Creator  = "DevCorp Nexus ERP",
        Subject  = $"{_d.InvoiceType} {_d.Number} — {_d.Client.Name}",
        Keywords = $"factura,{_d.Number},{_d.FiscalYear},IVA,Verifactu"
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
            page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(8.5f).FontColor(ColorText));

            page.Header().Element(ComposeHeader);
            page.Content().PaddingTop(8).Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CABECERA — empresa emisora + datos de la factura
    // ═══════════════════════════════════════════════════════════════════════════
    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            // ── Banda superior de título ──────────────────────────────────────
            col.Item().Background(ColorPrimary).Padding(10).Row(row =>
            {
                // Título + badge de tipo
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                    {
                        var title = _d.InvoiceType == "Rectificativa"
                            ? "FACTURA RECTIFICATIVA"
                            : "FACTURA";
                        t.Span(title).FontColor(Colors.White).Bold().FontSize(16);
                    });

                    if (_d.InvoiceType == "Rectificativa" && _d.RectifiedInvoiceNumber is not null)
                    {
                        c.Item().PaddingTop(2).Text(t =>
                        {
                            t.Span("Rectifica a la factura: ").FontColor(Colors.White).FontSize(7);
                            t.Span(_d.RectifiedInvoiceNumber).FontColor(Colors.White).Bold().FontSize(7);
                        });
                    }
                });

                // Número y año fiscal (alineado a la derecha)
                row.AutoItem().Column(c =>
                {
                    c.Item().AlignRight().Text(t =>
                        t.Span(_d.Number).FontColor(Colors.White).Bold().FontSize(14));
                    c.Item().AlignRight().Text(t =>
                        t.Span($"Ejercicio fiscal {_d.FiscalYear}").FontColor(Colors.White).FontSize(7));
                });
            });

            col.Item().Height(6);

            // ── Emisor (izq.) + Metadatos factura (der.) ─────────────────────
            col.Item().Row(row =>
            {
                // Datos de empresa emisora
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t => t.Span("EMISOR").Bold().FontSize(7).FontColor(ColorPrimary));
                    c.Item().Height(3);
                    c.Item().Text(t => t.Span(_d.Company.Name).Bold().FontSize(11));
                    c.Item().Text(t => t.Span($"NIF / CIF: {_d.Company.TaxId}").FontSize(8));
                    c.Item().Text(t => t.Span(_d.Company.Address).FontSize(8).FontColor(ColorMuted));
                    c.Item().Text(t => t.Span(_d.Company.Country).FontSize(8).FontColor(ColorMuted));
                });

                // Cuadro de datos de la factura
                row.ConstantItem(205).Background(ColorPrimaryBg)
                    .Border(1).BorderColor(ColorBorder)
                    .Padding(10)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.4f);
                            cols.RelativeColumn(1f);
                        });

                        MetaRow(table, "Fecha expedición:",  _d.IssueDate.ToString("dd/MM/yyyy"));
                        MetaRow(table, "Fecha vencimiento:", _d.DueDate.ToString("dd/MM/yyyy"));
                        MetaRow(table, "Serie / Secuencia:",
                            $"{_d.Series} / {_d.FiscalYear}-{_d.InvoiceType[..1]}");
                        MetaRow(table, "Estado:",
                            _d.IsLocked ? "SELLADA ✓" : _d.Status.ToUpper(),
                            bold: true,
                            valueColor: _d.IsLocked ? ColorSuccess : ColorWarning);
                    });
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CONTENIDO — cliente + líneas + desglose IVA + totales
    // ═══════════════════════════════════════════════════════════════════════════
    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            // ── Bloque cliente ────────────────────────────────────────────────
            col.Item()
               .Border(1).BorderColor(ColorBorder)
               .Background(Colors.White)
               .Padding(10)
               .Column(inner =>
               {
                   inner.Item().Text(t =>
                       t.Span("DESTINATARIO (CLIENTE/ADQUIRENTE)").Bold().FontSize(7).FontColor(ColorPrimary));
                   inner.Item().Height(4);
                   inner.Item().Row(r =>
                   {
                       r.RelativeItem().Column(cc =>
                       {
                           cc.Item().Text(t => t.Span(_d.Client.Name).Bold().FontSize(10));
                           cc.Item().Text(t => t.Span($"NIF / CIF: {_d.Client.TaxId}").FontSize(8));
                           cc.Item().Text(t => t.Span(_d.Client.Address).FontSize(8).FontColor(ColorMuted));
                       });
                       r.ConstantItem(160).Column(cc =>
                       {
                           if (!string.IsNullOrWhiteSpace(_d.Client.Email))
                               cc.Item().AlignRight().Text(t =>
                                   t.Span(_d.Client.Email).FontSize(8).FontColor(ColorMuted));
                       });
                   });
               });

            col.Item().Height(10);

            // ── Tabla de líneas de factura ────────────────────────────────────
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(5f);   // Descripción
                    cols.RelativeColumn(1.2f); // Cantidad
                    cols.RelativeColumn(1.8f); // Precio unit.
                    cols.RelativeColumn(1f);   // IVA %
                    cols.RelativeColumn(1.8f); // Base imponible
                    cols.RelativeColumn(1.8f); // Cuota IVA
                });

                // Cabecera de la tabla (función local para evitar dependencia del tipo explícito HeaderDescriptor)
                table.Header(h =>
                {
                    void LH(string text, bool right = false)
                    {
                        var cell = h.Cell().Background(ColorPrimary)
                                    .PaddingHorizontal(5).PaddingVertical(5);
                        if (right) cell = cell.AlignRight();
                        cell.Text(t => t.Span(text).FontColor(Colors.White).Bold().FontSize(7.5f));
                    }

                    LH("DESCRIPCIÓN / CONCEPTO");
                    LH("CANT.",        right: true);
                    LH("PRECIO UNIT.", right: true);
                    LH("IVA %",        right: true);
                    LH("BASE IMPON.",  right: true);
                    LH("CUOTA IVA",    right: true);
                });

                // Filas de líneas
                bool alt = false;
                foreach (var line in _d.Lines)
                {
                    var bg = alt ? ColorPrimaryBg : "#FFFFFF";
                    alt = !alt;

                    LineCell(table, line.Description, bg, false);
                    LineCell(table, line.Quantity.ToString("N2"), bg, true);
                    LineCell(table, $"{line.UnitPrice:N2} €", bg, true);
                    LineCell(table, $"{line.TaxRate:N0} %", bg, true);
                    LineCell(table, $"{line.LineTotal:N2} €", bg, true);
                    LineCell(table, $"{line.TaxAmount:N2} €", bg, true);
                }
            });

            col.Item().Height(12);

            // ── Desglose IVA (izq.) + Totales (der.) ─────────────────────────
            col.Item().Row(row =>
            {
                // Desglose por tipos de IVA
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                        t.Span("DESGLOSE POR TIPO IMPOSITIVO (RD 1619/2012)").Bold().FontSize(7).FontColor(ColorPrimary));
                    c.Item().Height(4);

                    bool hasSurcharge = _d.TaxGroups.Any(g => g.SurchargeRate > 0);

                    c.Item().Table(taxTable =>
                    {
                        taxTable.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(55);  // Tipo IVA
                            cols.RelativeColumn();     // Base imponible
                            cols.RelativeColumn();     // Cuota IVA
                            if (hasSurcharge)
                            {
                                cols.ConstantColumn(50); // Rec. Eq. %
                                cols.RelativeColumn();   // Cuota Rec.
                            }
                        });

                        // Cabecera desglose (función local para inferencia automática del tipo)
                        taxTable.Header(h =>
                        {
                            void TH(string text)
                            {
                                h.Cell().Background(ColorPrimaryBg)
                                 .Border(0.5f).BorderColor(ColorBorder)
                                 .PaddingHorizontal(4).PaddingVertical(3)
                                 .Text(t => t.Span(text).Bold().FontSize(7).FontColor(ColorPrimary));
                            }

                            TH("Tipo IVA");
                            TH("Base Impon.");
                            TH("Cuota IVA");
                            if (hasSurcharge) { TH("Rec. Eq. %"); TH("Cuota Rec."); }
                        });

                        foreach (var tg in _d.TaxGroups)
                        {
                            TaxCell(taxTable, $"{tg.TaxRate:N0} %", false);
                            TaxCell(taxTable, $"{tg.BaseAmount:N2} €", true);
                            TaxCell(taxTable, $"{tg.TaxAmount:N2} €", true);
                            if (hasSurcharge)
                            {
                                TaxCell(taxTable, $"{tg.SurchargeRate:N2} %", true);
                                TaxCell(taxTable, $"{tg.SurchargeAmount:N2} €", true);
                            }
                        }
                    });
                });

                row.ConstantItem(12);

                // Totales
                row.ConstantItem(225).Column(c =>
                {
                    c.Item().Table(totTable =>
                    {
                        totTable.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2.2f);
                            cols.RelativeColumn(1f);
                        });

                        TotalRow(totTable, "Base imponible total:", $"{_d.Subtotal:N2} €");
                        TotalRow(totTable, "Total cuotas IVA:", $"{_d.TaxAmount:N2} €");

                        if (_d.SurchargeAmount > 0)
                            TotalRow(totTable, "Recargo de equivalencia:", $"{_d.SurchargeAmount:N2} €");

                        if (_d.IrpfRate > 0)
                            TotalRow(totTable,
                                $"Retención IRPF ({_d.IrpfRate:N0} %):",
                                $"-{_d.IrpfAmount:N2} €",
                                valueColor: "#C0392B");

                        // Separador
                        totTable.Cell().ColumnSpan(2).BorderTop(1).BorderColor(ColorPrimary).Height(1);
                        totTable.Cell().ColumnSpan(2).Height(2);

                        // TOTAL destacado
                        totTable.Cell()
                            .Background(ColorPrimary).PaddingHorizontal(8).PaddingVertical(6)
                            .Text(t => t.Span("TOTAL A PAGAR").FontColor(Colors.White).Bold().FontSize(10));
                        totTable.Cell()
                            .Background(ColorPrimary).PaddingHorizontal(8).PaddingVertical(6)
                            .AlignRight()
                            .Text(t => t.Span($"{_d.Total:N2} €").FontColor(Colors.White).Bold().FontSize(11));
                    });
                });
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PIE — cumplimiento legal: hash, Verifactu QR, texto legal
    // ═══════════════════════════════════════════════════════════════════════════
    private void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().BorderTop(1).BorderColor(ColorPrimary).PaddingTop(6);

            col.Item().Row(row =>
            {
                // ── Bloque de integridad y textos legales ─────────────────────
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(t =>
                        t.Span("INTEGRIDAD Y TRAZABILIDAD — LEY 11/2021 / RD 1007/2023 VERIFACTU")
                         .Bold().FontSize(6.5f).FontColor(ColorPrimary));

                    c.Item().Height(3);

                    if (!string.IsNullOrWhiteSpace(_d.Hash))
                        c.Item().Text(t =>
                        {
                            t.Span("Hash antifraude SHA-256: ").Bold().FontSize(6).FontColor(ColorMuted);
                            t.Span(_d.Hash).FontSize(6).FontColor(ColorText);
                        });

                    if (!string.IsNullOrWhiteSpace(_d.VerifactuHuella))
                    {
                        if (_d.VerifactuRealtimeSubmission)
                        {
                            c.Item().Text(t =>
                            {
                                t.Span("VERI*FACTU — Factura verificable en la sede de la AEAT. ")
                                 .Bold().FontSize(7f).FontColor(ColorPrimary);
                                t.Span("Sistema de facturación conforme al RD 1007/2023.")
                                 .FontSize(6.5f).FontColor(ColorMuted);
                            });
                        }
                        else
                        {
                            c.Item().Text(t =>
                            {
                                t.Span("Factura emitida por SIF en modalidad sin remisión de registros ")
                                 .Bold().FontSize(6.5f).FontColor(ColorPrimary);
                                t.Span("(no VERI*FACTU) conforme al RD 1007/2023. Los registros se conservan de forma local.")
                                 .FontSize(6.5f).FontColor(ColorMuted);
                            });
                        }

                        c.Item().Height(3);

                        c.Item().Text(t =>
                        {
                            t.Span("Huella Verifactu (Anexo II): ").Bold().FontSize(6).FontColor(ColorMuted);
                            t.Span(_d.VerifactuHuella).FontSize(6).FontColor(ColorText);
                        });
                    }

                    c.Item().Height(5);

                    c.Item().Text(t =>
                        t.Span(
                            "Factura emitida de conformidad con el Real Decreto 1619/2012, de 30 de noviembre, " +
                            "por el que se aprueba el Reglamento de facturación, y la Ley 11/2021, de 9 de julio, " +
                            "de medidas de prevención y lucha contra el fraude fiscal. Esta factura forma parte de una " +
                            "cadena de hashes SHA-256 inalterable. Cualquier modificación invalida su carácter de documento " +
                            "mercantil y fiscal.")
                        .FontSize(5.5f).FontColor(ColorMuted));

                    c.Item().Height(4);

                    // Número de página
                    c.Item().Text(t =>
                    {
                        t.Span("Página ").FontSize(7).FontColor(ColorMuted);
                        t.CurrentPageNumber().FontSize(7).FontColor(ColorMuted);
                        t.Span(" de ").FontSize(7).FontColor(ColorMuted);
                        t.TotalPages().FontSize(7).FontColor(ColorMuted);
                        t.Span($"  •  {_d.Company.Name}  •  NIF: {_d.Company.TaxId}")
                         .FontSize(7).FontColor(ColorMuted);
                    });
                });

                // ── QR Verifactu (solo modo remisión TIKE) ───────────────────
                if (_d.VerifactuRealtimeSubmission && !string.IsNullOrWhiteSpace(_d.VerifactuQrUrl))
                {
                    row.ConstantItem(8);
                    row.ConstantItem(68).Column(qrCol =>
                    {
                        qrCol.Item().AlignCenter().Text(t =>
                            t.Span("Verificar en AEAT").FontSize(5.5f).FontColor(ColorPrimary).Bold());
                        qrCol.Item().Height(2);
                        qrCol.Item().Image(GenerateQrCodePng(_d.VerifactuQrUrl));
                        qrCol.Item().Height(2);
                        qrCol.Item().AlignCenter().Text(t =>
                            t.Span("Verifactu / RD 1007/2023").FontSize(5f).FontColor(ColorMuted));
                    });
                }
            });
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS de diseño
    // ═══════════════════════════════════════════════════════════════════════════

    private static void MetaRow(
        TableDescriptor table,
        string label, string value,
        bool bold = false,
        string? valueColor = null)
    {
        table.Cell().PaddingVertical(2)
             .Text(t => t.Span(label).FontSize(7.5f).FontColor("#555555"));

        var cell = table.Cell().PaddingVertical(2).AlignRight();
        cell.Text(t =>
        {
            var span = t.Span(value).FontSize(7.5f).FontColor(valueColor ?? ColorText);
            if (bold) span.Bold();
        });
    }

    private static void LineCell(TableDescriptor table, string text, string bg, bool right)
    {
        var cell = table.Cell()
                        .Background(bg)
                        .BorderBottom(0.5f).BorderColor(ColorBorder)
                        .PaddingHorizontal(5).PaddingVertical(4);
        if (right) cell = cell.AlignRight();
        cell.Text(t => t.Span(text).FontSize(8));
    }

    private static void TaxCell(TableDescriptor table, string text, bool right)
    {
        var cell = table.Cell()
                        .BorderBottom(0.5f).BorderColor(ColorBorder)
                        .PaddingHorizontal(4).PaddingVertical(3);
        if (right) cell = cell.AlignRight();
        cell.Text(t => t.Span(text).FontSize(8));
    }

    private static void TotalRow(
        TableDescriptor table,
        string label, string value,
        string? valueColor = null)
    {
        table.Cell()
             .PaddingHorizontal(6).PaddingVertical(4)
             .BorderBottom(0.5f).BorderColor(ColorBorder)
             .Text(t => t.Span(label).FontSize(8.5f).FontColor(ColorMuted));

        table.Cell()
             .AlignRight()
             .PaddingHorizontal(6).PaddingVertical(4)
             .BorderBottom(0.5f).BorderColor(ColorBorder)
             .Text(t => t.Span(value).FontSize(8.5f).FontColor(valueColor ?? ColorText));
    }

    private static byte[] GenerateQrCodePng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var qrData    = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode    = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(4); // 4 px por módulo → imagen ~168x168 px
    }
}
