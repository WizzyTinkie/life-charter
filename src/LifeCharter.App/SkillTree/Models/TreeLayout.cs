namespace LifeCharter.App.SkillTree.Models;

public sealed record TreeLayout(IReadOnlyList<TreeNode> Nodes, IReadOnlyList<TreeEdge> Edges);
