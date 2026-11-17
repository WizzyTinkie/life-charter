using LifeCharter.App.Help.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Help.Business;

/// <summary>
/// The help text. Every number comes from <see cref="ProgressionRules"/> and
/// <see cref="LevelCurve"/>, so the help stays right when the rules are tuned.
/// </summary>
public static class HelpContent
{
    private const int LevelRows = 8;

    public static IReadOnlyList<HelpSection> Sections { get; } =
    [
        new("The idea",
            "LifeCharter is a passive skill tree for real life, in the style of Path of Exile. You grow three attributes by doing things and spend skill points to choose what to work on next.",
            [
                "Health (green): lifestyle — sleep, water, food, mind, screen time, social life.",
                "Stamina (red): exercise — cardio, strength, mobility, steps, sport.",
                "Might (blue): learning — languages, programming, reading, courses, creative skills.",
                "Every node has goals. Finishing all goals of a node masters it and opens the way to its neighbours.",
            ]),
        new("Node states",
            "A node's look tells you where it stands.",
            [
                "Locked (dark, grey outline): not reachable yet. Master a neighbouring node or its prerequisites first.",
                "Available (coloured outline): you can allocate it with a skill point.",
                "Training (tinted, with a gold progress ring): allocated; its goals are in progress.",
                "Mastered (filled with colour): all goals complete. Its neighbours become available.",
                "The gold node in the centre is you — the root, always mastered.",
            ]),
        new("Skill points",
            "Skill points decide what you commit to. You earn them by levelling up and spend them on nodes.",
            [
                $"You start with {ProgressionRules.StartingSkillPoints} points and get +1 for every new level.",
                "Allocating a node costs 1 point and puts it into Training.",
                $"A node can be allocated when it is Available, has at least one goal, you have a point left, and fewer than {ProgressionRules.WorkInProgressLimit} nodes of the same attribute are in Training.",
                $"The limit of {ProgressionRules.WorkInProgressLimit} nodes in Training per attribute keeps you from taking on too much at once.",
                "Refund gives the point back while a node is still in Training — no shame in changing your mind.",
                "A mastered node keeps its point for good.",
                $"Unspent points = {ProgressionRules.StartingSkillPoints} + (level − 1) − allocated nodes.",
            ]),
        new("XP and levels",
            "XP comes from logging what you did and from mastering nodes. XP goes to the attribute of the node.",
            [
                $"Each log entry gives {ProgressionRules.EntryXp} XP.",
                $"Only the first {ProgressionRules.DailyEntryCapPerGoal} entries per goal and day earn XP, so spamming one easy habit does not pay. Extra entries still count towards the goal.",
                $"Mastering a node gives a bonus: Small +{ProgressionRules.DefaultXpReward(NodeKind.Small)}, Notable +{ProgressionRules.DefaultXpReward(NodeKind.Notable)}, Keystone +{ProgressionRules.DefaultXpReward(NodeKind.Keystone)} XP.",
                "The XP needed for the next level grows with each level (100 × level^1.5); see the table below.",
            ]),
        new("Goals",
            "A goal is a measurable target on a node. A node is mastered when all of its goals are complete.",
            [
                "Count: every entry counts as 1 (sessions, meals, meetups).",
                "Sum: entry amounts add up (km, minutes, pages, litres).",
                "Milestone: done once — a single entry completes it.",
                "Total: everything since the goal started counts towards one target (for example 100 pages).",
                "Daily / Weekly: the target must be met on a number of days or weeks (for example 2 l a day on 14 days). Weeks run Monday to Sunday.",
                "In a row: those days or weeks must follow each other without a gap — a streak.",
                "Entries logged through a goal count only for that goal. Other entries on the node count for every goal with the same unit.",
            ]),
        new("Logging",
            "Log what you did, as soon as you did it.",
            [
                "In the tree: select a node, set the amount on a goal's Log row, optionally add a note, press Log.",
                "On the desktop widget: tick an item under \"Today\". It logs the usual amount for that goal.",
                "Entries cannot be edited later. That keeps the history honest and makes syncing between your machines safe.",
            ]),
        new("Editing the tree",
            "Press \"Edit tree\" in the header to change the tree itself.",
            [
                "Drag a node to move it.",
                "Shift-drag from one node to another to connect them; do it again to disconnect them.",
                "Double-click empty space to add a node. Its attribute follows the sector you clicked in, and it connects to the selected node (or the nearest one).",
                "Use the side panel to rename a node, change its attribute, kind or retention model, and to add, change or delete goals.",
                "Delete node removes the node with its connections and goals. The root cannot be deleted.",
            ]),
        new("Desktop widget, tray and shortcuts",
            "LifeCharter lives in the tray and on your desktop.",
            [
                "The widget sits under all windows. Show the desktop (Meta+D on KDE, Win+D on Windows) to see it.",
                "Peek shows the widget on top of everything: tray menu → Peek widget, or run lifecharter peek. Esc or a click elsewhere hides it.",
                "Clicking the tray icon opens the tree. Closing the tree keeps LifeCharter running in the tray; use Quit in the tray menu to exit.",
                "Commands: lifecharter tree, lifecharter peek, lifecharter help, lifecharter quit. Bind them to keyboard shortcuts in your desktop settings.",
            ]),
        new("Coming next",
            "Planned in docs/PLAN.md:",
            [
                "Daily siege: fading skills are attacked each day; you defend them by practising.",
                "Retention models: spaced repetition, habit forming and fitness maintenance decide when a skill is learned for good (Engraved).",
                "Quick entry: type \"run 5\" or a sentence in the widget.",
                "Sync between your machines through Syncthing, and an AI coach for goal planning.",
            ]),
    ];

    public static IReadOnlyList<LevelRow> Levels { get; } = BuildLevels();

    private static List<LevelRow> BuildLevels()
    {
        var rows = new List<LevelRow>();
        var total = 0;
        for (var level = 2; level <= LevelRows + 1; level++)
        {
            var step = LevelCurve.XpToNextLevel(level - 1);
            total += step;
            rows.Add(new LevelRow(level, step, total, ProgressionRules.StartingSkillPoints + level - 1));
        }

        return rows;
    }
}
