using Erp.Application.Features.Notifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers;

[ApiController, Route("api/notifications"), Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _mediator.Send(new GetNotificationSettingsQuery(), ct));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateNotificationSettingsRequest body, CancellationToken ct)
    {
        try
        {
            var ok = await _mediator.Send(new UpdateNotificationSettingsCommand(body.Frequency), ct);
            return ok ? Ok(new { message = "Configuración actualizada." }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("push/vapid-public-key")]
    public async Task<IActionResult> GetVapidPublicKey(CancellationToken ct)
    {
        var key = await _mediator.Send(new GetPushPublicKeyQuery(), ct);
        return key is null ? NotFound(new { error = "Push no configurado." }) : Ok(new { publicKey = key });
    }

    [HttpPost("push/subscribe")]
    public async Task<IActionResult> SubscribePush([FromBody] PushSubscribeRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Endpoint))
            return BadRequest(new { error = "Endpoint requerido." });

        await _mediator.Send(new SubscribePushCommand(body.Endpoint, body.P256dh, body.Auth), ct);
        return Ok(new { message = "Suscripción push registrada." });
    }

    [HttpDelete("push/subscribe")]
    public async Task<IActionResult> UnsubscribePush([FromBody] PushSubscribeRequest body, CancellationToken ct)
    {
        var ok = await _mediator.Send(new UnsubscribePushCommand(body.Endpoint), ct);
        return ok ? Ok(new { message = "Suscripción eliminada." }) : NotFound();
    }
}

public record UpdateNotificationSettingsRequest(string Frequency);
public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);
