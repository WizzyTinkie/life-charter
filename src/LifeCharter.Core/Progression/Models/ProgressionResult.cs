namespace LifeCharter.Core.Progression.Models;

public sealed record ProgressionResult(CharacterProgress Character, IReadOnlyDictionary<Guid, NodeProgress> Nodes);
