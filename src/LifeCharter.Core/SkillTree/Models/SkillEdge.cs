using LifeCharter.Core.Sync.Models;

namespace LifeCharter.Core.SkillTree.Models;

public sealed class SkillEdge : MutableEntity
{
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public EdgeKind Kind { get; set; } = EdgeKind.Path;
}
