using MediatR;

namespace Erp.Modules.Accounting.Application.Features.AccountantExport;

public record GetAccountantExportSettingsQuery : IRequest<AccountantExportSettingsDto>;

public record UpdateAccountantExportSettingsCommand(
    string? AccountantEmail,
    string Frequency) : IRequest<AccountantExportSettingsDto>;

public record ExportAccountantPackageCommand(int? Year, int? Month, int? Quarter, bool SendEmail = false)
    : IRequest<AccountantPackageResultDto>;

public sealed class AccountantExportSettingsDto
{
    public string? AccountantEmail { get; init; }
    public string Frequency { get; init; } = "disabled";
    public DateTime? LastRunAt { get; init; }
}

public sealed class AccountantPackageResultDto
{
    public byte[] ZipContent { get; init; } = [];
    public string FileName { get; init; } = "paquete-gestoria.zip";
    public bool EmailSent { get; init; }
    public string? Message { get; init; }
}
