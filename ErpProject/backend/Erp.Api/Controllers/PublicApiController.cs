using Asp.Versioning;
using Erp.Application.DTOs;
using Erp.Application.Features.PublicApi;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers
{
    /// <summary>
    /// API Pública v1 - Acceso a datos del ERP mediante API Key
    /// Rate Limiting: Según configuración de la API Key
    /// Autenticación: X-API-Key header
    /// </summary>
    [ApiController]
    [Route("api/v1")]
    [ApiVersion("1.0")]
    public class PublicApiController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PublicApiController(IMediator mediator) => _mediator = mediator;

        /// <summary>
        /// Validar API Key
        /// </summary>
        [HttpGet("auth/verify")]
        [ProducesResponseType(typeof(VerifyApiKeyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyApiKey(CancellationToken ct)
        {
            var result = await _mediator.Send(new VerifyApiKeyQuery(), ct);
            return result is null
                ? Unauthorized(new { error = "API Key requerida" })
                : Ok(result);
        }

        // ===================================================================
        // FACTURAS - ENDPOINTS PÚBLICOS
        // ===================================================================

        /// <summary>
        /// Obtener todas las facturas
        /// </summary>
        [HttpGet("invoices")]
        [ProducesResponseType(typeof(List<InvoiceDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetInvoices([FromQuery] GetInvoicesQuery query, CancellationToken ct)
        {
            var result = await _mediator.Send(query, ct);
            Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
            return Ok(result);
        }

        /// <summary>
        /// Obtener factura por ID
        /// </summary>
        [HttpGet("invoices/{id}")]
        [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
        {
            var result = await _mediator.Send(new GetInvoiceByIdQuery { Id = id }, ct);
            return result is null ? NotFound(new { error = "Factura no encontrada" }) : Ok(result);
        }

        /// <summary>
        /// Crear nueva factura
        /// </summary>
        [HttpPost("invoices")]
        [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceCommand command, CancellationToken ct)
        {
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetInvoice), new { id = result.Id }, result);
        }

        /// <summary>
        /// Marcar factura como pagada
        /// </summary>
        [HttpPost("invoices/{id}/pay")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PayInvoice(Guid id, CancellationToken ct)
        {
            var ok = await _mediator.Send(new MarkPaidCommand { Id = id }, ct);
            return ok ? Ok(new { message = "Factura marcada como pagada" }) : NotFound();
        }

        /// <summary>
        /// Bloquear/Contabilizar factura — ACCIÓN IRREVERSIBLE
        /// </summary>
        [HttpPost("invoices/{id}/lock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LockInvoice(Guid id, CancellationToken ct)
        {
            var ok = await _mediator.Send(new LockInvoiceCommand { Id = id }, ct);
            return ok ? Ok(new { message = "Factura bloqueada y contabilizada" }) : NotFound();
        }

        // ===================================================================
        // CLIENTES - ENDPOINTS PÚBLICOS
        // ===================================================================

        /// <summary>
        /// Obtener todos los clientes
        /// </summary>
        [HttpGet("clients")]
        [ProducesResponseType(typeof(List<ClientDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetClients([FromQuery] GetClientsQuery query, CancellationToken ct)
            => Ok(await _mediator.Send(query, ct));

        /// <summary>
        /// Obtener cliente por ID
        /// </summary>
        [HttpGet("clients/{id}")]
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClient(Guid id, CancellationToken ct)
        {
            var result = await _mediator.Send(new GetClientByIdQuery { Id = id }, ct);
            return result is null ? NotFound(new { error = "Cliente no encontrado" }) : Ok(result);
        }

        /// <summary>
        /// Crear cliente
        /// </summary>
        [HttpPost("clients")]
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientCommand command, CancellationToken ct)
        {
            var result = await _mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetClient), new { id = result.Id }, result);
        }

        /// <summary>
        /// Actualizar cliente
        /// </summary>
        [HttpPatch("clients/{id}")]
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateClient(Guid id, [FromBody] UpdateClientCommand command, CancellationToken ct)
        {
            command.Id = id;
            return Ok(await _mediator.Send(command, ct));
        }

        // ===================================================================
        // REPORTES - ENDPOINTS PÚBLICOS
        // ===================================================================

        /// <summary>
        /// Obtener Libro Diario
        /// </summary>
        [HttpGet("reports/diario")]
        [ProducesResponseType(typeof(DiarioReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDiarioReport([FromQuery] GetDiarioQuery query, CancellationToken ct)
            => Ok(await _mediator.Send(query, ct));

        /// <summary>
        /// Obtener Mayor Contable
        /// </summary>
        [HttpGet("reports/mayor")]
        [ProducesResponseType(typeof(MayorReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMayorReport([FromQuery] GetMayorQuery query, CancellationToken ct)
            => Ok(await _mediator.Send(query, ct));

        /// <summary>
        /// Obtener Balance Sheet
        /// </summary>
        [HttpGet("reports/balance")]
        [ProducesResponseType(typeof(BalanceSheetReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBalanceReport([FromQuery] GetBalanceSheetQuery query, CancellationToken ct)
            => Ok(await _mediator.Send(query, ct));

        /// <summary>
        /// Obtener Profit & Loss
        /// </summary>
        [HttpGet("reports/pyg")]
        [ProducesResponseType(typeof(PyGReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPyGReport([FromQuery] GetProfitAndLossQuery query, CancellationToken ct)
            => Ok(await _mediator.Send(query, ct));

        // ===================================================================
        // SALUD DEL API
        // ===================================================================

        /// <summary>
        /// Health Check
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Health()
            => Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "1.0" });
    }

    // ===================================================================
    // DTOs de Respuesta
    // ===================================================================

    public record ApiKeyValidationResponse(string Message, string Status);

    public record DiarioReportDto(
        List<DiarioLineaDto> Lineas,
        decimal TotalDebe,
        decimal TotalHaber,
        int TotalRegistros);

    public record DiarioLineaDto(
        string Fecha,
        string Numero,
        string Cuenta,
        string Descripcion,
        decimal Debe,
        decimal Haber,
        decimal Saldo);

    public record MayorReportDto(
        List<MayorCuentaDto> Cuentas,
        decimal TotalDebe,
        decimal TotalHaber,
        int TotalCuentas);

    public record MayorCuentaDto(
        string Codigo,
        string Nombre,
        string Tipo,
        decimal SaldoInicial,
        decimal TotalDebe,
        decimal TotalHaber,
        decimal SaldoFinal);

    public record BalanceSheetReportDto(
        BalanceSectionDto Activo,
        BalanceSectionDto Pasivo,
        BalanceSectionDto Patrimonio,
        decimal TotalActivo,
        decimal TotalPasivoPatrimonio,
        bool EstaBalanceado);

    public record BalanceSectionDto(
        string Nombre,
        List<BalanceLineDto> Lineas,
        decimal Total);

    public record BalanceLineDto(
        string Codigo,
        string Descripcion,
        decimal Monto);

    public record PyGReportDto(
        PyGSectionDto Ingresos,
        PyGSectionDto Gastos,
        decimal TotalIngresos,
        decimal TotalGastos,
        decimal ResultadoBruto,
        decimal ResultadoNeto);

    public record PyGSectionDto(
        string Nombre,
        List<PyGLineaDto> Lineas,
        decimal SubTotal);

    public record PyGLineaDto(
        string Codigo,
        string Descripcion,
        decimal Monto);
}
