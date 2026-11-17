using Avalonia;

namespace LifeCharter.App.SkillTree.Models;

/// <summary>Arguments of the canvas' selection and editing events.</summary>
public sealed class TreeCanvasEventArgs(Guid? nodeId, Guid? otherNodeId, Point world) : EventArgs
{
    public Guid? NodeId { get; } = nodeId;
    public Guid? OtherNodeId { get; } = otherNodeId;
    public Point World { get; } = world;
}
