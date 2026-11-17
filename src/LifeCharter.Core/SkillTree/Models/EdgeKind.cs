namespace LifeCharter.Core.SkillTree.Models;

public enum EdgeKind
{
    /// <summary>Undirected: a mastered neighbour makes the other node available.</summary>
    Path,

    /// <summary>Directed: <c>From</c> must be mastered before <c>To</c> becomes available.</summary>
    Requires,
}
