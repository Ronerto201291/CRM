using Erp.Application.DTOs;
using MediatR;

namespace Erp.Application.Features.Auth.Commands;

/// <summary>Request to begin 2FA setup — returns QR code URI and secret</summary>
public record Setup2FaCommand(Guid UserId) : IRequest<Setup2FaResult>;

public record Setup2FaResult(string Secret, string QrCodeUri, List<string> BackupCodes);

/// <summary>Request to confirm 2FA setup with first TOTP code</summary>
public record Confirm2FaCommand(Guid UserId, string TotpCode) : IRequest<bool>;

/// <summary>Request to disable 2FA (requires current TOTP or backup code)</summary>
public record Disable2FaCommand(Guid UserId, string Code) : IRequest<bool>;

/// <summary>Request to verify TOTP during login (second factor). Returns full LoginResponseDto on success, null on failure.</summary>
public record VerifyTotpCommand(Guid UserId, string Code) : IRequest<LoginResponseDto?>;
