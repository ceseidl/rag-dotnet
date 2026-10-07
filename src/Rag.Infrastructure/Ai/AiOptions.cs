namespace Rag.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Offline (fakes) ou OpenAiCompatible.</summary>
    public string Mode { get; set; } = "Offline";

    /// <summary>Base URL do endpoint (API OpenAI).</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Segredo: user-secrets ou Ai__ApiKey.</summary>
    public string? ApiKey { get; set; }

    public string ChatModel { get; set; } = "gpt-4.1-mini";
    public string EmbeddingModel { get; set; } =
        "text-embedding-3-small";

    public int AttemptTimeoutSeconds { get; set; } = 30;
    public int TotalTimeoutSeconds { get; set; } = 90;
    public int MaxRetries { get; set; } = 3;
}
