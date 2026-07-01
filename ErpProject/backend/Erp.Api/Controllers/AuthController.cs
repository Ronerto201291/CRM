using Erp.Application.Features.Auth.Commands;
using Erp.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Erp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _ctx;

    public AuthController(IMediator mediator, IApplicationDbContext ctx)
    {
        _mediator = mediator;
        _ctx = ctx;
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    [HttpPost("register"), AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterCompanyCommand command)
    {
        try { return Ok(await _mediator.Send(command)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("accept-invite"), AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteCommand command)
    {
        try { return Ok(await _mediator.Send(command)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }


    [HttpPost("refresh"), AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
    {
        try { return Ok(await _mediator.Send(command)); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    // ─────── 2FA TOTP ───────

    /// <summary>
    /// Initiates 2FA setup. Returns QR code URI for authenticator app and backup codes.
    /// Call this, show QR to user, then call /2fa/confirm with first TOTP code.
    /// </summary>
    [HttpPost("2fa/setup"), Authorize]
    public async Task<IActionResult> Setup2Fa()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _mediator.Send(new Setup2FaCommand(userId.Value));
        return Ok(new
        {
            qrCodeUri = result.QrCodeUri,
            secret = result.Secret,
            backupCodes = result.BackupCodes,
            message = "Scan the QR code with your authenticator app, then call /api/auth/2fa/confirm"
        });
    }

    /// <summary>
    /// Confirms 2FA setup by verifying the first TOTP code from the authenticator app.
    /// 2FA is only activated after successful confirmation.
    /// </summary>
    [HttpPost("2fa/confirm"), Authorize]
    public async Task<IActionResult> Confirm2Fa([FromBody] ConfirmTotpRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var success = await _mediator.Send(new Confirm2FaCommand(userId.Value, request.Code));
        if (!success)
            return BadRequest(new { error = "Invalid TOTP code. Please verify your authenticator app is synced." });

        return Ok(new { message = "2FA enabled successfully. Keep your backup codes safe." });
    }

    /// <summary>
    /// Disables 2FA. Requires current TOTP code or a backup code.
    /// </summary>
    [HttpPost("2fa/disable"), Authorize]
    public async Task<IActionResult> Disable2Fa([FromBody] ConfirmTotpRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        try
        {
            var success = await _mediator.Send(new Disable2FaCommand(userId.Value, request.Code));
            if (!success)
                return BadRequest(new { error = "Invalid code. Use your TOTP app or a backup code." });
            return Ok(new { message = "2FA disabled successfully." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Verifies TOTP code during login second factor.
    /// Called after successful password login when user has 2FA enabled.
    /// </summary>
    [HttpPost("2fa/verify"), AllowAnonymous]
    public async Task<IActionResult> VerifyTotp([FromBody] VerifyTotpRequest request)
    {
        var result = await _mediator.Send(new VerifyTotpCommand(request.UserId, request.Code));
        if (result == null)
            return BadRequest(new { error = "Invalid or expired TOTP code." });

        return Ok(result);
    }

    // ─────── Password Recovery ───────

    /// <summary>
    /// Initiates password reset. Always returns 200 to prevent email enumeration.
    /// Sends a reset link to the registered email if the address exists.
    /// </summary>
    [HttpPost("forgot-password"), AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _mediator.Send(new ForgotPasswordCommand(request.Email));
        return Ok(new { message = "Si existe una cuenta con ese email, recibirás un enlace de restablecimiento." });
    }

    /// <summary>
    /// Resets the user's password using a valid token received by email.
    /// </summary>
    [HttpPost("reset-password"), AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var ok = await _mediator.Send(new ResetPasswordCommand(request.Token, request.NewPassword));
        return ok
            ? Ok(new { message = "Contraseña restablecida correctamente. Ya puedes iniciar sesión." })
            : BadRequest(new { error = "El enlace no es válido o ha expirado. Solicita uno nuevo." });
    }

    // ─────── Email Confirmation ───────

    /// <summary>
    /// Sends (or resends) the email confirmation link to the current user.
    /// </summary>
    [HttpPost("resend-confirmation"), Authorize]
    public async Task<IActionResult> ResendConfirmation()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        await _mediator.Send(new SendEmailConfirmationCommand(userId.Value));
        return Ok(new { message = "Correo de confirmación enviado. Revisa tu bandeja de entrada." });
    }

    /// <summary>
    /// Confirms the user's email address using the token received by email.
    /// </summary>
    [HttpGet("confirm-email"), AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
    {
        var ok = await _mediator.Send(new ConfirmEmailCommand(token));
        return ok
            ? Ok(new { message = "Email confirmado correctamente. Tu cuenta está activa." })
            : BadRequest(new { error = "El enlace no es válido o ha expirado." });
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record ConfirmTotpRequest(string Code);
public record VerifyTotpRequest(Guid UserId, string Code);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Token, string NewPassword);
