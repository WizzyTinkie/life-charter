using Avalonia;

namespace LifeCharter.App.SkillTree.Models;

/// <summary>A node as the canvas draws it: world position, kind, attribute and allocation.</summary>
public sealed class TreeNode(int index, Point position, NodeKind kind, TreeAttribute attribute, string label)
{
    public int Index { get; } = index;
    public Point Position { get; } = position;
    public NodeKind Kind { get; } = kind;
    public TreeAttribute Attribute { get; } = attribute;
    public string Label { get; } = label;
    public bool Allocated { get; set; }

    public double Radius => Kind switch
    {
        NodeKind.Root => 26,
        NodeKind.Keystone => 22,
        NodeKind.Notable => 14,
        _ => 8,
    };
}
