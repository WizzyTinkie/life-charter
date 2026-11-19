namespace LifeCharter.Core.Siege.Models;

/// <summary>What happens when a node's integrity reaches 0.</summary>
public enum ZeroIntegrityEffect
{
    /// <summary>Nothing; the node only shows it is worn down.</summary>
    Nothing,

    /// <summary>The node is dimmed until it is cleansed, but keeps its XP.</summary>
    Dim,

    /// <summary>The node is corrupted: dimmed and its mastery XP is withheld until it is cleansed.</summary>
    Corrupt,
}
