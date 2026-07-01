using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Erp.Application.Features.Sii.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Xml.Serialization;

namespace Erp.Infrastructure.Services.Sii;

/// <summary>
/// Generates SII-AEAT compliant XML for RegistroFacturasEmitidas and RegistroFacturasRecibidas.
/// </summary>
public class SiiXmlGenerator
{
    private readonly IApplicationDbContext _ctx;
    private readonly IBillingDbContext     _billing;
    private readonly IExpensesDbContext    _expenses;

    public SiiXmlGenerator(IApplicationDbContext ctx, IBillingDbContext billing, IExpensesDbContext expenses)
    {
        _ctx      = ctx;
        _billing  = billing;
        _expenses = expenses;
    }

    public async Task<string> GenerateFacturasEmitidasAsync(
        Guid companyId, int year, int month, CancellationToken ct = default)
    {
        var company = await _ctx.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found");

        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == companyId
                && i.IsLocked
                && i.IssueDate >= startDate
                && i.IssueDate < endDate)
            .OrderBy(i => i.IssueDate)
            .ToListAsync(ct);

        var suministro = new SuministroLRFacturasEmitidas
        {
            Cabecera = new CabeceraSii
            {
                Titular = new PersonaFisicaJuridica { NombreRazon = company.Name, NIF = company.TaxId },
                TipoComunicacion = "A0"
            },
            Registros = invoices.Select(inv => new RegistroFacturaEmitida
            {
                PeriodoLiquidacion = new PeriodoLiquidacion
                {
                    Ejercicio = year.ToString(),
                    Periodo = month.ToString("D2")
                },
                IDFactura = new IDFactura
                {
                    IDEmisorFactura = new PersonaFisicaJuridica { NombreRazon = company.Name, NIF = company.TaxId },
                    NumSerieFacturaEmisor = inv.Number,
                    FechaExpedicion = inv.IssueDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)
                },
                FacturaExpedida = new FacturaExpedida
                {
                    TipoFactura = inv.InvoiceType == "Rectificativa" ? "R1" : "F1",
                    ClaveRegimen = "01",
                    DescripcionOperacion = "Factura emitida",
                    ImporteTotal = inv.Total.ToString("F2", CultureInfo.InvariantCulture),
                    TipoDesglose = new TipoDesglose
                    {
                        DesgloseFactura = new DesgloseFactura
                        {
                            Sujeta = new Sujeta
                            {
                                NoExenta = new NoExenta
                                {
                                    TipoNoExenta = "S1",
                                    DesgloseIVA = new DesgloseIVA
                                    {
                                        DetalleIVA = inv.InvoiceLines
                                            .GroupBy(l => l.TaxRate)
                                            .Select(g => new DetalleIVA
                                            {
                                                TipoImpositivo = g.Key.ToString("F0", CultureInfo.InvariantCulture),
                                                BaseImponible = g.Sum(l => l.LineTotal).ToString("F2", CultureInfo.InvariantCulture),
                                                CuotaRepercutida = g.Sum(l => l.TaxAmount).ToString("F2", CultureInfo.InvariantCulture)
                                            }).ToList()
                                    }
                                }
                            }
                        }
                    }
                }
            }).ToList()
        };

        return SerializeToXml(suministro);
    }

    public async Task<string> GenerateFacturasRecibidasAsync(
        Guid companyId, int year, int month, CancellationToken ct = default)
    {
        var company = await _ctx.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new InvalidOperationException("Company not found");

        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        var expenses = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == companyId
                && e.Status == "Approved"
                && e.IssueDate.HasValue
                && e.IssueDate >= startDate
                && e.IssueDate < endDate)
            .OrderBy(e => e.IssueDate)
            .ToListAsync(ct);

        var suministro = new SuministroLRFacturasRecibidas
        {
            Cabecera = new CabeceraSii
            {
                Titular = new PersonaFisicaJuridica { NombreRazon = company.Name, NIF = company.TaxId },
                TipoComunicacion = "A0"
            },
            Registros = expenses.Select(exp => new RegistroFacturaRecibida
            {
                PeriodoLiquidacion = new PeriodoLiquidacion
                {
                    Ejercicio = year.ToString(),
                    Periodo = month.ToString("D2")
                },
                IDFactura = new IDFacturaRecibida
                {
                    IDEmisorFactura = new PersonaFisicaJuridica
                    {
                        NombreRazon = exp.SupplierName ?? "Desconocido",
                        NIF = exp.SupplierTaxId ?? string.Empty
                    },
                    NumSerieFacturaEmisor = exp.InvoiceNumber ?? exp.Id.ToString(),
                    FechaExpedicion = exp.IssueDate!.Value.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)
                },
                FacturaRecibida = new FacturaRecibida
                {
                    TipoFactura = "F1",
                    ClaveRegimen = "01",
                    DescripcionOperacion = "Factura recibida",
                    Contraparte = new PersonaFisicaJuridica
                    {
                        NombreRazon = exp.SupplierName ?? "Desconocido",
                        NIF = exp.SupplierTaxId ?? string.Empty
                    },
                    FechaRegContable = exp.CreatedAt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
                    ImporteTotal = (exp.Total ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                    CuotaDeducible = (exp.VATAmount ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                    DesgloseFactura = new DesgloseFacturaRecibida
                    {
                        DesgloseIVA = new DesgloseIVA
                        {
                            DetalleIVA = new List<DetalleIVA>
                            {
                                new()
                                {
                                    TipoImpositivo = (exp.VATRate ?? 21).ToString("F0", CultureInfo.InvariantCulture),
                                    BaseImponible = (exp.TaxBase ?? 0).ToString("F2", CultureInfo.InvariantCulture),
                                    CuotaRepercutida = (exp.VATAmount ?? 0).ToString("F2", CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    }
                }
            }).ToList()
        };

        return SerializeToXml(suministro);
    }

    private static string SerializeToXml<T>(T obj)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var sw = new System.IO.StringWriter();
        using var writer = System.Xml.XmlWriter.Create(sw, new System.Xml.XmlWriterSettings
        {
            Indent = true,
            Encoding = System.Text.Encoding.UTF8
        });
        serializer.Serialize(writer, obj);
        return sw.ToString();
    }
}
