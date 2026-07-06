namespace Erp.Modules.Treasury.Application.Options;

public sealed class OpenBankingOptions
{
    public const string SectionName = "OpenBanking";

    /// <summary>Mock | Stub | (futuro: Nordigen, Tink, …)</summary>
    public string Provider { get; set; } = "Mock";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
}
