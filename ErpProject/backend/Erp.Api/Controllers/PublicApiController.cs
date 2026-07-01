using Asp.Versioning;
using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
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
        private readonly IApiKeyValidator _apiKeyValidator;
        private readonly ILogger<PublicApiController> _logger;

        public PublicApiController(
            IMediator mediator,
            IApiKeyValidator apiKeyValidator,
            ILogger<PublicApiController> logger)
        {
            _mediator = mediator;
            _apiKeyValidator = apiKeyValidator;
            _logger = logger;
        }

        /// <summary>
        /// Validar API Key
        /// </summary>
        [HttpGet("auth/verify")]
        [ProducesResponseType(typeof(ApiKeyValidationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerifyApiKey()
        {
            var apiKey = Request.Headers["X-API-Key"].ToString();
            if (string.IsNullOrEmpty(apiKey))
                return Unauthorized(new { error = "API Key requerida" });

            var isValid = await _apiKeyValidator.ValidateAsync(apiKey);
            if (!isValid)
                return Unauthorized(new { error = "API Key inválida o expirada" });

            return Ok(new { message = "API Key válida", status = "authenticated" });
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
        public async Task<IActionResult> GetInvoices([FromQuery] string? status = null)
        {
            var result = await _mediator.Send(new GetInvoicesQuery { Status = status });
            return Ok(result);
        }

        /// <summary>
        /// Obtener factura por ID
        /// </summary>
        [HttpGet("invoices/{id}")]
        [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInvoice(Guid id)
        {
            var list = await _mediator.Send(new GetInvoicesQuery());
            var result = list.FirstOrDefault(i => i.Id == id);
            if (result == null)
                return NotFound(new { error = "Factura no encontrada" });
            return Ok(result);
        }

        /// <summary>
        /// Crear nueva factura
        /// </summary>
        [HttpPost("invoices")]
        [ProducesResponseType(typeof(InvoiceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceCommand command)
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetInvoice), new { id = result.Id }, result);
        }

        /// <summary>
        /// Marcar factura como pagada
        /// </summary>
        [HttpPost("invoices/{id}/pay")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> PayInvoice(Guid id)
        {
            var ok = await _mediator.Send(new MarkPaidCommand { Id = id });
            return ok ? Ok(new { message = "Factura marcada como pagada" }) : NotFound();
        }

        /// <summary>
        /// Bloquear/Contabilizar factura — ACCIÓN IRREVERSIBLE
        /// </summary>
        [HttpPost("invoices/{id}/lock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> LockInvoice(Guid id)
        {
            var ok = await _mediator.Send(new LockInvoiceCommand { Id = id });
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
        public async Task<IActionResult> GetClients([FromQuery] string? search = null)
        {
            var result = await _mediator.Send(new GetClientsQuery { SearchTerm = search });
            return Ok(result);
        }

        /// <summary>
        /// Obtener cliente por ID
        /// </summary>
        [HttpGet("clients/{id}")]
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClient(Guid id)
        {
            var list = await _mediator.Send(new GetClientsQuery());
            var result = list.FirstOrDefault(c => c.Id == id);
            if (result == null)
                return NotFound(new { error = "Cliente no encontrado" });
            return Ok(result);
        }

        /// <summary>
        /// Crear cliente
        /// </summary>
        [HttpPost("clients")]
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientCommand command)
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetClient), new { id = result.Id }, result);
        }

        /// <summary>
        /// Actualizar cliente
        /// </summary>
        [HttpPatch("clients/{id}")]
        [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateClient(Guid id, [FromBody] UpdateClientCommand command)
        {
            command.Id = id;
            var result = await _mediator.Send(command);
            return Ok(result);
        }

        // ===================================================================
        // REPORTES - ENDPOINTS PÚBLICOS
        // ===================================================================

        /// <summary>
        /// Obtener Libro Diario
        /// </summary>
        [HttpGet("reports/diario")]
        [ProducesResponseType(typeof(DiarioReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDiarioReport(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin)
        {
            var query = new GetDiarioQuery
            {
                FechaInicio = DateTime.Parse(fechaInicio),
                FechaFin = DateTime.Parse(fechaFin)
            };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Obtener Mayor Contable
        /// </summary>
        [HttpGet("reports/mayor")]
        [ProducesResponseType(typeof(MayorReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMayorReport(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin)
        {
            var query = new GetMayorQuery
            {
                FechaInicio = DateTime.Parse(fechaInicio),
                FechaFin = DateTime.Parse(fechaFin)
            };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Obtener Balance Sheet
        /// </summary>
        [HttpGet("reports/balance")]
        [ProducesResponseType(typeof(BalanceSheetReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBalanceReport([FromQuery] string fechaCorte)
        {
            var query = new GetBalanceSheetQuery { FechaCorte = DateTime.Parse(fechaCorte) };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        /// <summary>
        /// Obtener Profit & Loss
        /// </summary>
        [HttpGet("reports/pyg")]
        [ProducesResponseType(typeof(PyGReportDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPyGReport(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin)
        {
            var query = new GetProfitAndLossQuery
            {
                FechaInicio = DateTime.Parse(fechaInicio),
                FechaFin = DateTime.Parse(fechaFin)
            };
            var result = await _mediator.Send(query);
            return Ok(result);
        }

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
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                version = "1.0"
            });
        }
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
