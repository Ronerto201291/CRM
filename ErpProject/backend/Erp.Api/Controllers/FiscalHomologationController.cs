using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>Estado de homologación fiscal — evidencia offline sin cert AEAT (ADR-0018 #0b-#0d).</summary>
[ApiController]
[Route("api/fiscal/homologation")]
[Authorize]
public class FiscalHomologationController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public FiscalHomologationController(IConfiguration configuration) => _configuration = configuration;

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var siiSend = _configuration.GetValue<bool>("Sii:SendEnabled", false);
        var faceSend = _configuration.GetValue<bool>("Face:SendEnabled", false);
        var certPath = _configuration["Sii:CertificatePath"];
        var hasCert = !string.IsNullOrWhiteSpace(certPath) && System.IO.File.Exists(certPath);

        return Ok(new
        {
            sii = new
            {
                offlineValidation = true,
                httpSendEnabled = siiSend,
                certificatePresent = hasCert,
                homologationStatus = siiSend && hasCert ? "ready_for_test_env" : "blocked_external",
                blocker = siiSend && hasCert ? null : "Requiere certificado AEAT y Sii:SendEnabled=true en entorno de pruebas"
            },
            facturae = new
            {
                offlineValidation = true,
                faceSendEnabled = faceSend,
                homologationStatus = faceSend ? "ready_for_test_env" : "blocked_external",
                blocker = faceSend ? null : "Requiere Face:SendEnabled=true y homologación FACe en entorno test"
            },
            sepa = new
            {
                offlineValidation = true,
                bankHomologation = false,
                homologationStatus = "blocked_external",
                blocker = "Requiere validación con entidad bancaria (pain.001/008)"
            },
            nifValidation = new
            {
                implemented = true,
                note = "SpanishTaxIdValidator en CRM/Billing/Core (#0f)"
            }
        });
    }
}
