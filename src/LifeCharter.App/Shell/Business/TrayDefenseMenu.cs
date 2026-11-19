using Avalonia.Controls;
using LifeCharter.App.Siege.Business;
using LifeCharter.Core.Common.Business;
using LifeCharter.Core.Goals.Business;
using LifeCharter.Core.Journal.Models;
using LifeCharter.Core.Retention.Models;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Shell.Business;

/// <summary>
/// The tray menu's "Today's defenses": one item per attack. Clicking a habit, fitness or motor
/// skill node defends it by logging its usual amount; a Memory node opens its recall ratings.
/// </summary>
public sealed class TrayDefenseMenu(ChartSession session)
{
    private static readonly ReviewRating[] Ratings = [ReviewRating.Again, ReviewRating.Hard, ReviewRating.Good, ReviewRating.Easy];

    private readonly NativeMenu menu = new();

    public NativeMenuItem Item { get; } = new("Today's defenses");

    /// <summary>The evening badge: undefended attacks that deal damage, after this hour.</summary>
    public const int AlertHour = 18;

    public bool ShouldAlert(DateTimeOffset now) =>
        now.Hour >= AlertHour && session.Progression.Siege.Today.Attacks.Any(attack => !attack.Defended && attack.Threat > 0);

    public void Refresh()
    {
        Item.Menu = menu;
        menu.Items.Clear();
        var today = session.Progression.Siege.Today;
        var nodes = session.Chart.Nodes.ToDictionary(node => node.Id);

        Item.Header = today.Attacks.Count == 0 ? "Today's defenses" : $"Today's defenses ({today.Defended}/{today.Attacks.Count})";
        if (today.Kind != SiegeDayKind.Siege || today.Attacks.Count == 0)
        {
            menu.Items.Add(new NativeMenuItem(today.Kind == SiegeDayKind.Siege ? "No attacks today" : RetentionText.DayKind(today.Kind)) { IsEnabled = false });
            return;
        }

        foreach (var attack in today.Attacks)
        {
            var node = nodes[attack.NodeId];
            var item = new NativeMenuItem($"{node.Title} · {RetentionText.Attack(attack)}")
            {
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = attack.Defended,
                IsEnabled = !attack.Defended,
            };

            if (node.RetentionModel == RetentionModel.Memory)
            {
                var ratings = new NativeMenu();
                foreach (var rating in Ratings)
                {
                    var choice = new NativeMenuItem($"Recalled: {rating}");
                    choice.Click += (_, _) => _ = session.ReviewAsync(node.Id, rating, EntrySource.Tray);
                    ratings.Items.Add(choice);
                }

                item.ToggleType = MenuItemToggleType.None;
                item.Menu = ratings;
            }
            else
            {
                var goal = session.Chart.Goals.FirstOrDefault(g => g.NodeId == node.Id && g.DeletedAt is null);
                item.Click += (_, _) => _ = session.LogAsync(
                    node.Id, goal?.Id, goal is null ? 1 : GoalDefaults.Amount(goal), goal?.Unit ?? "", "", EntrySource.Tray);
            }

            menu.Items.Add(item);
        }
    }
}
