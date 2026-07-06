using Erp.Modules.Payroll.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Erp.Modules.Payroll.Infrastructure.Services;

/// <summary>Generación PDF de recibo de nómina orientativo (QuestPDF).</summary>
public sealed class PayrollPayslipPdfService : IPayrollPayslipPdfService
{
    public PayrollPayslipPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(PayrollPayslipPdfData data)
    {
        IDocument document = new PayrollPayslipPdfDocument(data);
        return document.GeneratePdf();
    }
}

internal sealed class PayrollPayslipPdfDocument : IDocument
{
    private static readonly string ColorPrimary = "#1B3A6B";
    private static readonly string ColorMuted = "#6B7A8D";
    private static readonly string ColorText = "#1C2B3A";

    private readonly PayrollPayslipPdfData _d;

    public PayrollPayslipPdfDocument(PayrollPayslipPdfData data) => _d = data;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Recibo nómina {_d.Line.Year}-{_d.Line.Month:D2} — {_d.Employee.FullName}",
        Author = _d.Company.Name,
        Creator = "DevCorp Nexus ERP",
    };

    public DocumentSettings GetSettings() => new() { CompressDocument = true };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(ColorText));

            page.Header().Background(ColorPrimary).Padding(10).Column(col =>
            {
                col.Item().Text("RECIBO DE SALARIOS / LIQUIDACIÓN").FontColor(Colors.White).Bold().FontSize(14);
                col.Item().Text($"Período: {_d.Line.Month:D2}/{_d.Line.Year}").FontColor(Colors.White).FontSize(10);
            });

            page.Content().PaddingTop(12).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(_d.Company.Name).Bold().FontSize(11);
                        c.Item().Text($"NIF: {_d.Company.TaxId}").FontColor(ColorMuted);
                        if (!string.IsNullOrWhiteSpace(_d.Company.Address))
                            c.Item().Text(_d.Company.Address!).FontColor(ColorMuted);
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text(_d.Employee.FullName).Bold();
                        c.Item().Text($"NIF: {_d.Employee.TaxId}").FontColor(ColorMuted);
                        if (!string.IsNullOrWhiteSpace(_d.Employee.SocialSecurityNumber))
                            c.Item().Text($"NAF: {_d.Employee.SocialSecurityNumber}").FontColor(ColorMuted);
                    });
                });

                col.Item().PaddingTop(16).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(1);
                    });

                    void Row(string label, decimal amount, bool bold = false)
                    {
                        var cellLabel = table.Cell().BorderBottom(0.5f).BorderColor(ColorMuted).Padding(6)
                            .Text(label);
                        if (bold) cellLabel.Bold();
                        var cellAmount = table.Cell().BorderBottom(0.5f).BorderColor(ColorMuted).Padding(6)
                            .AlignRight().Text($"€ {amount:N2}");
                        if (bold) cellAmount.Bold();
                    }

                    Row("Salario bruto", _d.Line.GrossSalary);
                    Row("Base cotización CC", _d.Line.CommonContingenciesBase);
                    Row("Cuota SS trabajador", _d.Line.EmployeeSocialSecurity);
                    Row("Cuota SS empresa (informativa)", _d.Line.EmployerSocialSecurity);
                    Row("Base retención IRPF", _d.Line.IrpfBase);
                    Row($"Retención IRPF ({_d.Line.IrpfRate:N2} %)", _d.Line.IrpfWithheld);
                    Row("LÍQUIDO A PERCIBIR", _d.Line.NetPay, bold: true);
                });

                col.Item().PaddingTop(20).Background("#FFF8E6").Padding(8).Text(_d.Line.Disclaimer)
                    .FontSize(7.5f).FontColor("#92400e");
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Documento orientativo — ").FontColor(ColorMuted).FontSize(7);
                t.Span("DevCorp Nexus ERP").FontColor(ColorMuted).FontSize(7).Italic();
            });
        });
    }
}
