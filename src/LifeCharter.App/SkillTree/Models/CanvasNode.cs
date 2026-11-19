using Avalonia;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.SkillTree.Models;

/// <summary>
/// A node prepared for drawing: domain values plus derived state, progress, integrity and
/// whether it is under attack today and not yet defended.
/// </summary>
public sealed class CanvasNode(
    Guid id,
    Point position,
    NodeKind kind,
    LifeAttribute attribute,
    string title,
    NodeState state,
    double fraction,
    int integrity = 100,
    bool underAttack = false)
{
    public Guid Id { get; } = id;
    public Point Position { get; set; } = position;
    public NodeKind Kind { get; } = kind;
    public LifeAttribute Attribute { get; } = attribute;
    public string Title { get; } = title;
    public NodeState State { get; } = state;
    public double Fraction { get; } = fraction;
    public int Integrity { get; } = integrity;
    public bool UnderAttack { get; } = underAttack;

    public double Radius => Kind switch
    {
        NodeKind.Root => 30,
        NodeKind.Keystone => 26,
        NodeKind.Notable => 20,
        _ => 15,
    };
}
