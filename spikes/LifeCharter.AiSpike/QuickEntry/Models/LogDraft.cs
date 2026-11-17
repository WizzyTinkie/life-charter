namespace LifeCharter.AiSpike.QuickEntry.Models;

/// <summary>A log entry proposed by the model; the real app shows it for confirmation.</summary>
public sealed record LogDraft(string Activity, double Amount, string Unit, string? Note);
