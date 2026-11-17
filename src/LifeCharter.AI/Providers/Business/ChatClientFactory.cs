using System.ClientModel;
using Anthropic;
using LifeCharter.AI.Providers.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace LifeCharter.AI.Providers.Business;

/// <summary>
/// Builds one <see cref="IChatClient"/> pipeline for every provider, so tools and prompts are
/// written once and work against Claude, OpenAI and local models alike.
/// </summary>
public static class ChatClientFactory
{
    private const int DefaultMaxTokens = 4096;

    public static IChatClient Create(ProviderProfile profile)
    {
        var inner = profile.Provider switch
        {
            AiProvider.Anthropic => CreateAnthropic(profile),
            AiProvider.OpenAI => CreateOpenAI(profile, endpoint: null, profile.ApiKey ?? RequiredEnvironment("OPENAI_API_KEY")),
            AiProvider.Lemonade => CreateOpenAI(profile, profile.Endpoint ?? ProviderProfile.LemonadeEndpoint, profile.ApiKey ?? "lemonade"),
            AiProvider.OpenAICompatible => CreateOpenAI(
                profile,
                profile.Endpoint ?? throw new ArgumentException("An OpenAI-compatible profile needs an endpoint.", nameof(profile)),
                profile.ApiKey ?? "none"),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.Provider, "Unknown provider."),
        };

        return inner.AsBuilder().UseFunctionInvocation().Build();
    }

    private static IChatClient CreateAnthropic(ProviderProfile profile)
    {
        // Resolve the key here instead of letting the SDK fall back silently, so a missing key
        // fails before the first request with a message that says what to set.
        var client = new AnthropicClient { ApiKey = profile.ApiKey ?? RequiredEnvironment("ANTHROPIC_API_KEY") };
        return client.AsIChatClient(profile.Model, DefaultMaxTokens);
    }

    private static IChatClient CreateOpenAI(ProviderProfile profile, Uri? endpoint, string apiKey)
    {
        var options = new OpenAIClientOptions();
        if (endpoint is not null)
            options.Endpoint = endpoint;

        return new ChatClient(profile.Model, new ApiKeyCredential(apiKey), options).AsIChatClient();
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {name} or store an API key for this provider.");
}
