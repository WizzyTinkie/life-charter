using LifeCharter.App.Siege.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Shell.Business;

/// <summary>The text <c>lifecharter status</c> prints.</summary>
public static class StatusReport
{
    public static string Build(ChartSession session)
    {
        var character = session.Progression.Character;
        var siege = session.Progression.Siege;
        var nodes = session.Chart.Nodes.ToDictionary(node => node.Id);
        var lines = new List<string>
        {
            $"Lv {character.Level.Level} · {character.Level.XpIntoLevel}/{character.Level.XpForNextLevel} XP · {character.SkillPoints} skill points",
        };

        if (siege.Today.Kind != SiegeDayKind.Siege)
        {
            lines.Add(RetentionText.DayKind(siege.Today.Kind));
        }
        else if (siege.Today.Attacks.Count == 0)
        {
            lines.Add("No attacks today");
        }
        else
        {
            var attacks = siege.Today.Attacks.Select(attack =>
            {
                var node = nodes[attack.NodeId];
                var how = attack.Defended ? "✓"
                    : node.RetentionModel == RetentionModel.Memory ? "recall"
                    : attack.Threat > 0 ? $"−{attack.Threat} HP" : "reminder";
                return $"{node.Title} ({how})";
            });
            lines.Add($"Defend today {siege.Today.Defended}/{siege.Today.Attacks.Count}: {string.Join(", ", attacks)}");
        }

        if (siege.IsStarted)
            lines.Add($"Wards {siege.Wards}/{siege.WardCap} · streak {siege.Streak} (best {siege.BestStreak})");

        return string.Join(Environment.NewLine, lines);
    }
}
