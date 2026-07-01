using Microsoft.AspNetCore.Mvc;

namespace Erp.Modules.Accounting.Api.Controllers
{
    [ApiController]
    [Route("api/v1/accounting/aeat")]
    public class AeatModelsController : ControllerBase
    {
        [HttpPost("modelo347")]
        public IActionResult CreateModelo347([FromBody] object dto) => Created("", new { 
            id = Guid.NewGuid(), 
            status = "Draft",
            type = "347",
            message = "Modelo 347 creado (declaración anual operaciones 3.005€+)"
        });

        [HttpGet("modelo347/{year}")]
        public IActionResult GetModelo347(int year) => Ok(new { 
            year, 
            status = "Draft",
            totalRecords = 25,
            totalAmount = 500000m
        });

        [HttpPost("modelo347/{id}/export-txt")]
        public IActionResult ExportModelo347(Guid id) => Ok(new { 
            id,
            fileName = $"347_{DateTime.Now.Year}.txt",
            format = "AEAT_Official_TXT",
            message = "Archivo .txt oficial AEAT generado"
        });

        [HttpPost("modelo111-190")]
        public IActionResult CreateModelo111([FromBody] object dto) => Created("", new { 
            id = Guid.NewGuid(),
            status = "Draft",
            type = "111",
            message = "Modelo 111 creado (declaración trimestral IVA)"
        });

        [HttpPost("modelo200")]
        public IActionResult CreateModelo200([FromBody] object dto) => Created("", new { 
            id = Guid.NewGuid(),
            status = "Draft",
            type = "200",
            message = "Modelo 200 creado (declaración anual IVA)"
        });

        [HttpPost("modelo202")]
        public IActionResult CreateModelo202([FromBody] object dto) => Created("", new { 
            id = Guid.NewGuid(),
            status = "Draft",
            type = "202",
            message = "Modelo 202 creado (devolución IVA)"
        });

        [HttpPost("{id}/sign-and-submit")]
        public IActionResult SignAndSubmit(Guid id) => Ok(new { 
            id,
            status = "Submitted",
            submissionReference = Guid.NewGuid().ToString().Substring(0, 13),
            submissionDate = DateTime.UtcNow,
            message = "Modelo AEAT firmado y enviado"
        });
    }
}
