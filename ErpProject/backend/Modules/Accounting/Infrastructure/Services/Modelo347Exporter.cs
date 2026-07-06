using System.Globalization;
using System.Text;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo347Exporter : IModelo347Exporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;
    private readonly IModelo347Reader _reader;

    public Modelo347Exporter(IModelo347Reader reader) => _reader = reader;

    public async Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var data = await _reader.GetYearAsync(tenantId, year, ct);
        var sb = new StringBuilder();
        sb.AppendLine("AVISO;Este CSV no sustituye la presentación oficial del modelo 347 sin validación en programa de ayuda AEAT o asesoría.");
        sb.AppendLine($"MODELO 347 — Operaciones con terceros. Año {year}");
        sb.AppendLine($"NIF Declarante;{data.NifDeclarante}");
        sb.AppendLine($"Razón Social;{data.RazonSocial}");
        sb.AppendLine($"Umbral aplicado;3.005,06 € (IVA incluido)");
        sb.AppendLine($"Nº operadores clientes;{data.Clientes.Count}");
        sb.AppendLine($"Nº operadores proveedores;{data.Proveedores.Count}");
        sb.AppendLine();
        AppendSection(sb, "CLIENTES (ventas emitidas ≥ 3.005,06 € IVA incluido)", data.Clientes);
        sb.AppendLine();
        AppendSection(sb, "PROVEEDORES (compras ≥ 3.005,06 € IVA incluido)", data.Proveedores);

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FiscalCsvExportResult
        {
            Content = bytes,
            FileName = $"Modelo347_{year}.csv",
            Disclaimer = "Modelo 347 CSV resumen interno; no es el fichero físico del diseño de registro AEAT.",
        };
    }

    public async Task<FiscalCsvExportResult> ExportAeatTxtAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var csv = await ExportAsync(tenantId, year, ct);
        var txt = Encoding.UTF8.GetString(csv.Content.Skip(3).ToArray());
        if (txt.StartsWith('\uFEFF')) txt = txt.TrimStart('\uFEFF');
        var body = "# Modelo 347 — texto plano (ORIENTATIVO)\r\n" +
                   "# NO es el registro físico del diseño de registro AEAT hasta contrastarlo con el programa de ayuda oficial.\r\n" +
                   txt.Replace("\n", "\r\n");
        var bytes = Encoding.UTF8.GetBytes(body);
        return new FiscalCsvExportResult
        {
            Content = bytes,
            ContentType = "text/plain",
            FileName = $"Modelo347_{year}_aeat.txt",
            Disclaimer = "TXT 347 orientativo; no sustituye fichero validado por la AEAT.",
        };
    }

    private static void AppendSection(StringBuilder sb, string title, IReadOnlyList<Modelo347OperatorRow> rows)
    {
        sb.AppendLine($"# {title}");
        sb.AppendLine("NIF;Nombre;ImporteTotal;BaseImponible;CuotaIVA;CuotaIRPF;NumOperaciones;TipoNIF");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(";",
                r.Nif,
                r.Nombre.Replace(";", " "),
                r.ImporteTotal.ToString("F2", Es),
                r.BaseImponible.ToString("F2", Es),
                r.CuotaIVA.ToString("F2", Es),
                r.CuotaIRPF.ToString("F2", Es),
                r.NumOperaciones.ToString(),
                r.EsPersonaFisica ? "F" : "J"));
        }
    }
}
