namespace Erp.Application.Options;

/// <summary>Proveedor LLM OpenAI-compatible (#40). Deshabilitado por defecto.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}
