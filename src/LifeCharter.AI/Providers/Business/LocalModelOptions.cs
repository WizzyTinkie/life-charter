using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace LifeCharter.AI.Providers.Business;

/// <summary>Request tweaks for local models served by Lemonade / llama.cpp.</summary>
public static class LocalModelOptions
{
    /// <summary>
    /// Turns off the "thinking" phase of hybrid reasoning models such as Qwen3.5. For quick
    /// entry this cut a one-word answer from 258 to 2 generated tokens (1.56 s → 0.03 s) in
    /// Phase 0, with the same result.
    /// </summary>
    public static ChatOptions DisableThinking(ChatOptions options)
    {
        options.RawRepresentationFactory = _ =>
        {
            var completion = new ChatCompletionOptions();
#pragma warning disable SCME0001 // JsonPatch is experimental; it is the SDK's way to send extra body fields.
            completion.Patch.Set("$.chat_template_kwargs.enable_thinking"u8, false);
#pragma warning restore SCME0001
            return completion;
        };
        return options;
    }
}
