using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.SkillTree.Models;

public readonly record struct CanvasEdge(Guid From, Guid To, EdgeKind Kind);
