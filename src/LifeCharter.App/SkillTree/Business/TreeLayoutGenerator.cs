using Avalonia;
using LifeCharter.App.SkillTree.Models;

namespace LifeCharter.App.SkillTree.Business;

/// <summary>
/// Generates a radial dummy tree for the rendering spike: a root and three 120° sectors of
/// growing rings, each node linked to its nearest neighbour on the previous ring.
/// </summary>
public static class TreeLayoutGenerator
{
    private const double FirstRingRadius = 160;
    private const double RingSpacing = 120;
    private const double SectorSpreadDegrees = 100;

    public static TreeLayout Generate(int nodeCount, int seed = 42)
    {
        var random = new Random(seed);
        var nodes = new List<TreeNode> { new(0, new Point(0, 0), NodeKind.Root, TreeAttribute.Might, "You") { Allocated = true } };
        var edges = new List<TreeEdge>();

        TreeAttribute[] attributes = [TreeAttribute.Might, TreeAttribute.Health, TreeAttribute.Stamina];
        double[] sectorCenters = [-90, 30, 150];
        var perSector = (nodeCount - 1) / attributes.Length;

        for (var sector = 0; sector < attributes.Length; sector++)
        {
            var attribute = attributes[sector];
            var target = sector == attributes.Length - 1 ? nodeCount - 1 - perSector * sector : perSector;
            var previousRing = new List<int> { 0 };
            var placed = 0;

            for (var ring = 1; placed < target; ring++)
            {
                var capacity = Math.Min(3 + ring * 2, target - placed);
                var thisRing = new List<int>();

                for (var i = 0; i < capacity; i++)
                {
                    var t = capacity == 1 ? 0.5 : i / (double)(capacity - 1);
                    var degrees = sectorCenters[sector] + (t - 0.5) * SectorSpreadDegrees + (random.NextDouble() - 0.5) * 4;
                    var radius = FirstRingRadius + (ring - 1) * RingSpacing + (random.NextDouble() - 0.5) * 30;
                    var angle = degrees * Math.PI / 180;
                    var position = new Point(Math.Cos(angle) * radius, Math.Sin(angle) * radius);

                    var kind = ring > 5 && ring % 4 == 1 && i == capacity / 2
                        ? NodeKind.Keystone
                        : placed % 9 == 4 ? NodeKind.Notable : NodeKind.Small;

                    var index = nodes.Count;
                    nodes.Add(new TreeNode(index, position, kind, attribute, $"{attribute} {placed + 1}")
                    {
                        Allocated = ring <= 2,
                    });

                    edges.Add(new TreeEdge(Nearest(nodes, previousRing, position), index));
                    if (i > 0 && i % 3 == 0)
                        edges.Add(new TreeEdge(thisRing[^1], index));

                    thisRing.Add(index);
                    placed++;
                }

                previousRing = thisRing;
            }
        }

        return new TreeLayout(nodes, edges);
    }

    private static int Nearest(List<TreeNode> nodes, List<int> candidates, Point position)
    {
        var best = candidates[0];
        var bestDistance = double.MaxValue;
        foreach (var candidate in candidates)
        {
            var delta = nodes[candidate].Position - position;
            var distance = delta.X * delta.X + delta.Y * delta.Y;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }
}
