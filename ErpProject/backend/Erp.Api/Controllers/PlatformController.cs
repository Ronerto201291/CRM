using Erp.Application.Features.Platform;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

/// <summary>Metadatos de plataforma — roadmap producto (#38–#42f) sin stubs de negocio.</summary>
[ApiController]
[Route("api/platform")]
[Authorize]
public class PlatformController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlatformController(IMediator mediator) => _mediator = mediator;

    [HttpGet("product-roadmap")]
    public async Task<IActionResult> GetProductRoadmap(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetProductRoadmapQuery(), ct));
}
