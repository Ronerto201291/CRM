using Erp.Application.Common.Attributes;
using Erp.Application.Common.Fiscal;
using Erp.Modules.Accounting.Application.Features.Export;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers;

/// <summary>
/// Exports AEAT-format accounting books as downloadable CSV/TXT files.
///   GET /api/accounting/export/libro-diario   â€” Libro Diario (daily journal)
///   GET /api/accounting/export/modelo303      â€” Modelo 303 (IVA trimestral)
///   GET /api/accounting/export/modelo347      â€” Modelo 347 (operaciones con terceros)
/// Every action here is a read-only export/report, so the permission check is applied once
/// at class level instead of repeating it on each of the 19 actions (ADR-0018 #42c).
/// </summary>
[ApiController]
[Route("api/accounting/export")]
[Authorize]
[RequiredModule("Accounting")]
[RequirePermission(Permissions.Accounting.Export)]
public class AccountingExportController : ControllerBase
{
    private readonly IMediator _mediator;

    private static IActionResult FileFiscal(ControllerBase c, byte[] bytes, string contentType, string downloadName, string disclaimer)
    {
        FiscalExportHeaders.MarkAsNonOfficial(c.Response.Headers, disclaimer);
        return c.File(bytes, contentType, downloadName);
    }

    public AccountingExportController(IMediator mediator) => _mediator = mediator;

    // â”€â”€â”€ Libro Diario â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("libro-diario")]
    public async Task<IActionResult> ExportLibroDiario([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportLibroDiarioQuery(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    // â”€â”€â”€ Modelo 303 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [HttpGet("modelo303")]
    public async Task<IActionResult> ExportModelo303([FromQuery] int year, [FromQuery] int q, CancellationToken ct)
    {
        if (q < 1 || q > 4)
            return BadRequest(new { error = "q must be 1â€“4 (quarter)" });

        var result = await _mediator.Send(new ExportModelo303Query(year, q), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    // â”€â”€â”€ Modelo 303 â€” JSON (datos estructurados) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/accounting/modelo-303?year=2026&amp;quarter=1
    /// Retorna los datos del Modelo 303 en formato JSON (para el frontend o integraciÃ³n externa).
    /// </summary>
    [HttpGet("/api/accounting/modelo-303")]
    public async Task<IActionResult> GetModelo303Json([FromQuery] int year, [FromQuery] int quarter, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetModelo303JsonQuery(year, quarter), ct);
            return Ok(new
            {
                year = result.Year,
                quarter = result.Quarter,
                period = result.Period,
                nif = result.Nif,
                razonSocial = result.RazonSocial,
                devengado = result.Devengado,
                deducible = result.Deducible,
                liquidacion = result.Liquidacion,
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // â”€â”€â”€ Modelo 111 â€” Retenciones IRPF (JSON) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/accounting/modelo-111?year=2026&amp;quarter=1
    /// Modelo 111: retenciones e ingresos a cuenta de IRPF (actividades profesionales).
    /// Lista facturas emitidas con retenciÃ³n durante el trimestre.
    /// </summary>
    [HttpGet("/api/accounting/modelo-111")]
    public async Task<IActionResult> GetModelo111([FromQuery] int year, [FromQuery] int quarter, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetModelo111Query(year, quarter), ct);
            return Ok(new
            {
                year = result.Year,
                quarter = result.Quarter,
                period = result.Period,
                nif = result.Nif,
                razonSocial = result.RazonSocial,
                actividadesProfesionales = new
                {
                    casillaNumPerceptores = "07",
                    numPerceptores = result.NumPerceptores,
                    casillaBase = "08",
                    baseRetencion = result.BaseRetencionProf,
                    casillaImporte = "09",
                    importeRetencion = result.ImporteRetencionProf,
                },
                retencionesNominas = new
                {
                    nota = "Retenciones IRPF practicadas a trabajadores (liquidaciones de nÃ³mina finalizadas en el trimestre). Ver casillas del modelo 111 para trabajadores en la guÃ­a AEAT.",
                    numTrabajadores = result.NumTrabajadores,
                    baseTotal = result.BaseNominas,
                    retencionTotal = result.ImporteNominas,
                    lineas = result.LineasNominas,
                },
                totalAIngresar = new
                {
                    casilla = "28",
                    valor = result.TotalAIngresar,
                    nota = "Suma orientativa profesionales + nÃ³minas; validar con asesorÃ­a segÃºn casillas aplicables.",
                },
                facturasProfesionales = result.FacturasProfesionales.Select(i => new
                {
                    numero = i.Numero,
                    fechaExpedicion = i.FechaExpedicion,
                    clienteNif = i.ClienteNif,
                    clienteNombre = i.ClienteNombre,
                    baseRetencion = i.BaseRetencion,
                    tipoRetencion = i.TipoRetencion,
                    importeRetencion = i.ImporteRetencion,
                }),
            });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Libro registro facturas emitidas (campos habituales RIVA Art. 63) â€” CSV para archivo y revisiÃ³n.
    /// </summary>
    [HttpGet("libro-iva-emitidas")]
    public async Task<IActionResult> ExportLibroIvaEmitidas([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportLibroIvaEmitidasQuery(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    /// <summary>
    /// Libro registro facturas recibidas (gastos aprobados + IVA soportado) â€” CSV Art. 64 RIVA.
    /// </summary>
    [HttpGet("libro-iva-recibidas")]
    public async Task<IActionResult> ExportLibroIvaRecibidas([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportLibroIvaRecibidasQuery(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    // â”€â”€â”€ Modelo 347 â€” Operaciones con terceros (â‰¥ 3.005,06 â‚¬) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/accounting/export/modelo347?year=2026
    /// Modelo 347: DeclaraciÃ³n anual de operaciones con terceros (art. 29 RD 1065/2007).
    /// Umbral: operaciones â‰¥ 3.005,06 â‚¬ (IVA incluido) por NIF de contraparte.
    /// AgrupaciÃ³n por NIF (no por ClientId) para incluir operaciones manuales y NIF extranjeros.
    /// </summary>
    [HttpGet("modelo347")]
    public async Task<IActionResult> ExportModelo347([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportModelo347Query(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    /// <summary>
    /// Fichero .txt con mismos datos que el CSV 347 en formato pipe (importaciÃ³n / revisiÃ³n en software AEAT).
    /// No sustituye el fichero oficial generado por el programa de ayuda sin validaciÃ³n tÃ©cnica.
    /// </summary>
    [HttpGet("modelo347-aeat-txt")]
    public async Task<IActionResult> ExportModelo347AeatTxt([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportModelo347AeatTxtQuery(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    /// <summary>
    /// GET /api/accounting/export/modelo347-json?year=2026
    /// Vista previa JSON del modelo 347 (operadores ≥ 3.005,06 €).
    /// </summary>
    [HttpGet("modelo347-json")]
    public async Task<IActionResult> GetModelo347Json([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetModelo347JsonQuery(year), ct);
        return Ok(result);
    }

    /// <summary>
    /// Modelo 190 â€” resumen anual retenciones (trabajadores vÃ­a nÃ³minas + profesionales vÃ­a facturas con IRPF).
    /// </summary>
    [HttpGet("/api/accounting/modelo-190")]
    public async Task<IActionResult> GetModelo190([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetModelo190Query(year), ct);
        return Ok(new
        {
            year = result.Year,
            nif = result.Nif,
            razonSocial = result.RazonSocial,
            nota = result.Nota,
            perceptoresProfesionales = result.PerceptoresProfesionales.Select(p => new
            {
                nif = p.Nif,
                nombre = p.Nombre,
                baseTotal = p.BaseTotal,
                retencion = p.Retencion,
            }),
            perceptoresTrabajadores = result.PerceptoresTrabajadores.Select(t => new
            {
                nif = t.Nif,
                nombre = t.Nombre,
                baseTotal = t.BaseTotal,
                retencion = t.Retencion,
            }),
            totalRetencionesProf = result.TotalRetencionesProf,
            totalRetencionesTrab = result.TotalRetencionesTrab,
        });
    }

    /// <summary>
    /// Modelo 130 â€” pagos fraccionados IRPF actividades econÃ³micas (estructura JSON para cumplimentar).
    /// </summary>
    [HttpGet("/api/accounting/modelo-130")]
    public async Task<IActionResult> GetModelo130([FromQuery] int year, [FromQuery] int quarter, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new GetModelo130Query(year, quarter), ct);
            return Ok(new { year = result.Year, quarter = result.Quarter, nota = result.Nota, casillasOrientativas = result.CasillasOrientativas });
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Modelo 200 IS â€” esqueleto XML (declaraciÃ³n anual sociedades); cumplimentar con datos contables reales.
    /// </summary>
    [HttpGet("modelo200-xml")]
    public async Task<IActionResult> ExportModelo200Xml([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportModelo200XmlQuery(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    /// <summary>
    /// Modelo 202 â€” pagos fraccionados IS (esqueleto XML).
    /// </summary>
    [HttpGet("modelo202-xml")]
    public async Task<IActionResult> ExportModelo202Xml([FromQuery] int year, [FromQuery] int period, CancellationToken ct)
    {
        try
        {
            var result = await _mediator.Send(new ExportModelo202XmlQuery(year, period), ct);
            return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // â”€â”€â”€ Modelo 390 â€” Resumen Anual IVA (XML oficial AEAT) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/accounting/export/modelo390-xml?year=2026
    /// Modelo 390: Resumen anual de IVA (consolidaciÃ³n de los 4 modelos 303 trimestrales).
    /// Obligatorio como resumen anual. Estructura AEAT con casillas 01-65.
    /// </summary>
    [HttpGet("modelo390-xml")]
    public async Task<IActionResult> ExportModelo390Xml([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportModelo390XmlQuery(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    /// <summary>
    /// GET /api/accounting/export/modelo390?year=2026
    /// Modelo 390 en formato CSV de consulta (resumen anual IVA).
    /// </summary>
    [HttpGet("modelo390")]
    public async Task<IActionResult> ExportModelo390([FromQuery] int year, CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportModelo390Query(year), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }

    // â”€â”€â”€ Modelo 303 â€” XML oficial AEAT â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/accounting/export/modelo303-xml?year=2026&amp;q=1
    /// Genera el XML del Modelo 303 en el formato estÃ¡ndar AEAT para presentaciÃ³n telemÃ¡tica.
    /// Estructura compatible con el esquema de la AEAT (casillas Modelo 303 v20+).
    /// </summary>
    [HttpGet("modelo303-xml")]
    public async Task<IActionResult> ExportModelo303Xml([FromQuery] int year, [FromQuery] int q, CancellationToken ct)
    {
        if (q < 1 || q > 4)
            return BadRequest(new { error = "q must be 1-4 (quarter)" });
        try
        {
            var result = await _mediator.Send(new ExportModelo303XmlQuery(year, q), ct);
            return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // â”€â”€â”€ Modelo 349 â€” Operaciones intracomunitarias â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/accounting/export/modelo349?year=2026&amp;q=1
    /// Modelo 349: DeclaraciÃ³n recapitulativa de operaciones intracomunitarias (art. 164 LIVA).
    /// Obligatorio para empresas con operaciones con clientes/proveedores de la UE.
    /// Periodicidad: trimestral (mensual si acumulado > 50.000 â‚¬ en el trimestre).
    /// </summary>
    [HttpGet("modelo349")]
    public async Task<IActionResult> ExportModelo349(
        [FromQuery] int year, [FromQuery] int q, CancellationToken ct)
    {
        if (q < 1 || q > 4)
            return BadRequest(new { error = "q must be 1â€“4 (quarter)" });

        var asCsv = Request.Query.ContainsKey("format") &&
            Request.Query["format"].ToString().Equals("csv", StringComparison.OrdinalIgnoreCase);
        var result = await _mediator.Send(new ExportModelo349Query(year, q, asCsv), ct);
        return FileFiscal(this, result.Content, result.ContentType, result.FileName, result.Disclaimer);
    }
}
