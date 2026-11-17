using System.ComponentModel;
using System.Diagnostics;
using LifeCharter.AI.Providers.Business;
using LifeCharter.AI.Providers.Models;
using LifeCharter.AiSpike.QuickEntry.Models;
using Microsoft.Extensions.AI;

// Phase 0 spikes (c) and (d): one tool-calling quick-entry prompt against any provider.
//   dotnet run --project spikes/LifeCharter.AiSpike -- anthropic   [model]   (ANTHROPIC_API_KEY or prompt)
//   dotnet run --project spikes/LifeCharter.AiSpike -- openai      [model]   (OPENAI_API_KEY or prompt)
//   dotnet run --project spikes/LifeCharter.AiSpike -- lemonade    [model]   (lemond on :13305)
// Add --think to keep the local model's thinking phase (off by default for Lemonade).
// Without the environment variable, the spike asks for the key with hidden input; it is
// kept in memory for this run only and never printed or stored.

var think = args.Contains("--think");
args = [.. args.Where(arg => arg != "--think")];
var providerName = args.Length > 0 ? args[0].ToLowerInvariant() : "lemonade";
var profile = providerName switch
{
    "anthropic" => new ProviderProfile(AiProvider.Anthropic, args.Length > 1 ? args[1] : "claude-haiku-5-5"),
    "openai" => new ProviderProfile(AiProvider.OpenAI, args.Length > 1 ? args[1] : "gpt-6-luna"),
    "lemonade" => new ProviderProfile(AiProvider.Lemonade, args.Length > 1 ? args[1] : "Qwen3.5-4B-GGUF"),
    _ => throw new ArgumentException($"Unknown provider '{providerName}'. Use anthropic, openai or lemonade."),
};

var keyVariable = profile.Provider switch
{
    AiProvider.Anthropic => "ANTHROPIC_API_KEY",
    AiProvider.OpenAI => "OPENAI_API_KEY",
    _ => null,
};
if (keyVariable is not null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(keyVariable)))
{
    var enteredKey = ReadSecret($"{keyVariable} is not set. Paste the key (input hidden) and press Enter: ");
    if (enteredKey.Length == 0)
        throw new InvalidOperationException("No API key entered.");
    profile = profile with { ApiKey = enteredKey };
}

using var client = ChatClientFactory.Create(profile);
var drafts = new List<LogDraft>();

var proposeLog = AIFunctionFactory.Create(
    ([Description("Short activity name, e.g. running, reading, water")] string activity,
     [Description("Numeric amount")] double amount,
     [Description("Unit, e.g. km, min, pages, l")] string unit,
     [Description("Optional extra detail")] string? note) =>
    {
        drafts.Add(new LogDraft(activity, amount, unit, note));
        return "Draft recorded; the user will confirm it.";
    },
    "propose_log",
    "Propose one activity log entry parsed from the user's note.");

var options = new ChatOptions { Tools = [proposeLog], Temperature = 0 };
if (profile.Provider == AiProvider.Lemonade && !think)
    LocalModelOptions.DisableThinking(options);
string[] phrases =
[
    "ran 5k in 28 minutes",
    "přečetl jsem 30 stran knihy",
    "drank 2 liters of water",
];

Console.WriteLine($"Provider: {profile.Provider}, model: {profile.Model}, thinking: {(profile.Provider != AiProvider.Lemonade || think ? "default" : "off")}");
foreach (var phrase in phrases)
{
    drafts.Clear();
    var stopwatch = Stopwatch.StartNew();
    List<ChatMessage> messages =
    [
        new(ChatRole.System, "You turn short activity notes (any language) into log drafts. Call propose_log once per activity, then reply with one short confirmation in the user's language."),
        new(ChatRole.User, phrase),
    ];

    var response = await client.GetResponseAsync(messages, options);
    stopwatch.Stop();

    Console.WriteLine($"\n> {phrase}  ({stopwatch.Elapsed.TotalSeconds:F1} s)");
    foreach (var draft in drafts)
        Console.WriteLine($"  draft: {draft.Activity} · {draft.Amount} {draft.Unit}{(draft.Note is null ? "" : " · " + draft.Note)}");
    if (drafts.Count == 0)
        Console.WriteLine("  (no tool call)");
    Console.WriteLine($"  reply: {response.Text.Trim()}");
}

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    if (Console.IsInputRedirected)
        return Console.ReadLine()?.Trim() ?? "";

    var secret = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
            break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (secret.Length > 0)
                secret.Length--;
            continue;
        }

        if (!char.IsControl(key.KeyChar))
            secret.Append(key.KeyChar);
    }

    Console.WriteLine($" ({secret.Length} characters)");
    return secret.ToString().Trim();
}
