namespace LifeCharter.AI.Providers.Models;

public enum AiProvider
{
    Anthropic,
    OpenAI,

    /// <summary>Local Lemonade server (OpenAI-compatible).</summary>
    Lemonade,

    /// <summary>Any other OpenAI-compatible server (Ollama, LM Studio, …).</summary>
    OpenAICompatible,
}
