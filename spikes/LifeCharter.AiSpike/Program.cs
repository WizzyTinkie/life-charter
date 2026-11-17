using System.ComponentModel;
using System.Diagnostics;
using LifeCharter.AI.Providers.Business;
using LifeCharter.AI.Providers.Models;
using LifeCharter.AiSpike.QuickEntry.Models;
using Microsoft.Extensions.AI;

// Phase 0 spikes (c) and (d): one tool-calling quick-entry prompt against any provider.
//   dotnet run --project spikes/LifeCharter.AiSpike -- anthropic   [model]   (ANTHROPIC_API_KEY)
//   dotnet run --project spikes/LifeCharter.AiSpike -- openai      [model]   (OPENAI_API_KEY)
//   dotnet run --project spikes/LifeCharter.AiSpike -- lemonade    [model]   (lemond on :13305)
// Add --think to keep the local model's thinking phase (off by default for Lemonade).

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
