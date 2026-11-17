namespace LifeCharter.AI.Providers.Models;

/// <summary>
/// One configured AI provider. <paramref name="ApiKey"/> comes from the OS keychain at runtime
/// and is never persisted with the profile.
/// </summary>
public sealed record ProviderProfile(AiProvider Provider, string Model, Uri? Endpoint = null, string? ApiKey = null)
{
    public static readonly Uri LemonadeEndpoint = new("http://localhost:13305/api/v1");
}
