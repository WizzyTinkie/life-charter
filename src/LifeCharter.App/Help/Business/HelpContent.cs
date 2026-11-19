using LifeCharter.App.Help.Models;
using LifeCharter.Core.Progression.Business;
using LifeCharter.Core.Retention.Business;
using LifeCharter.Core.Siege.Business;
using LifeCharter.Core.Siege.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.Help.Business;

/// <summary>
/// The help text. Every number comes from the rule classes (<see cref="ProgressionRules"/>,
/// <see cref="LevelCurve"/>, <see cref="RetentionRules"/>, <see cref="SiegeRules"/>,
/// <see cref="DifficultyPresets"/>), so the help stays right when the rules are tuned.
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
                "Mastered (filled with colour): all goals complete. Its neighbours become available, and the retention phase begins.",
                "Fortified (silver rim): about halfway to permanent; takes half damage. Capacity nodes call it Seasoned.",
                "Engraved (gold rim with an inner ring): learned for good. Never attacked again.",
                "Corrupted (dim, dashed purple rim): its integrity reached 0. A short cleanse restores it.",
                "An amber ring around a mastered node shows its remaining integrity; a red dot means it is under attack today and not yet defended.",
                "The gold node in the centre is you — the root, always mastered.",
            ]),
        new("Skill points",
            "Skill points decide what you commit to. Think of them as training slots: you earn more by levelling up, and you get a point back whenever you master a node.",
            [
                $"You start with {ProgressionRules.StartingSkillPoints} points and get +1 for every new level.",
                "Allocating a node costs 1 point and puts it into Training.",
                $"A node can be allocated when it is Available, has at least one goal, you have a point left, and fewer than {ProgressionRules.WorkInProgressLimit} nodes of the same attribute are in Training.",
                $"The limit of {ProgressionRules.WorkInProgressLimit} nodes in Training per attribute keeps you from taking on too much at once.",
                "Refund gives the point back while a node is still in Training — no shame in changing your mind.",
                "Mastering a node gives its point back, so finishing something always lets you pick the next thing.",
                $"Unspent points = {ProgressionRules.StartingSkillPoints} + (level − 1) − nodes in training.",
            ]),
        new("XP and levels",
            "XP comes from logging what you did and from mastering nodes. XP goes to the attribute of the node.",
            [
                $"Each log entry gives {ProgressionRules.EntryXp} XP.",
                $"Only the first {ProgressionRules.DailyEntryCapPerGoal} entries per goal and day earn XP, so spamming one easy habit does not pay. Extra entries still count towards the goal.",
                $"Mastering a node gives a bonus: Small +{ProgressionRules.DefaultXpReward(NodeKind.Small)}, Notable +{ProgressionRules.DefaultXpReward(NodeKind.Notable)}, Keystone +{ProgressionRules.DefaultXpReward(NodeKind.Keystone)} XP.",
                "The XP needed for the next level grows with each level (100 × level^1.5); see the table below.",
            ]),
        new("The daily siege",
            "Each day, mastered skills that you would actually start to forget or lose are attacked. Defending one means doing the practice that prevents the loss.",
            [
                "Attacks are chosen in the morning, most dangerous first, and shown on the widget and in the tree (red dot). They do not change during the day.",
                "Defend a Memory node with a recall: try to recall it first (blank page, explain it aloud), then rate Again, Hard, Good or Easy. Re-reading does not count.",
                "Defend any other node by doing it: tick it on the widget, or log it in the tree.",
                $"An attack left undefended at midnight deals about {SiegeRules.BaseDamage:0} damage, more the longer the skill has been neglected (up to ×{RetentionRules.MaxSeverity:0}). Fortified nodes take half. Integrity runs from {SiegeRules.MaxIntegrity} to 0.",
                "Any practice heals a node back to full integrity, attacked or not.",
                "At 0 integrity a node is corrupted (on Casual it only fades). Practise it a few times to cleanse it; relearning is faster than learning.",
                "Only scheduled attacks deal damage. Skills beyond your daily budget, on rest days or on vacation simply wait.",
                $"Wards: you start with {SiegeRules.StartingWards} and earn one for every {SiegeRules.DaysPerWard} fully defended days. Spend one on the widget to shield a whole day (illness, travel). A shielded day does not break your streak.",
                "Vacation (Siege settings) pauses all attacks for a few days. Due recalls come back afterwards, spread within your budget.",
                "Nodes in training are not attacked: their goal keeps them going.",
            ]),
        new("When is a skill learned for good?",
            "Each node has a retention model that matches what kind of skill it is. The model decides when it is attacked and when it becomes Engraved. The side panel shows the progress and an estimated date.",
            [
                $"Memory (knowledge): spaced repetition (FSRS) predicts recall. Engraved at a memory stability of {RetentionRules.EngravedStabilityDays:0} days, after at least {RetentionRules.EngravedMinimumReviews} successful recalls spread over {RetentionRules.EngravedMinimumSpanDays / 30} months. Fortified at {RetentionRules.FortifiedStabilityDays:0} days.",
                $"Habit (lifestyle): becomes automatic with repetitions. Engraved after {RetentionRules.HabitPlateauRepetitions} repetitions (simple habits half, complex ones double) plus an automaticity self-check averaging {RetentionRules.SelfCheckPass:0} of 5. Each missed attack costs one repetition.",
                $"Capacity (fitness): needs a weekly maintenance dose of about two thirds of your training sessions. No damage for the first {RetentionRules.CapacityGraceDays} days without a session. Never engraved, because fitness never is; Seasoned after {RetentionRules.SeasonedDays / 30} months with the dose in {RetentionRules.SeasonedAdherence:P0} of weeks.",
                $"Motor skill (technique): fades slowly. Engraved after {RetentionRules.MotorEngravedSessions} practice sessions at least {RetentionRules.MotorSessionSpacingDays} days apart, spread over {RetentionRules.MotorEngravedSpanDays / 7} weeks after mastery.",
                "Milestone (one-off achievement): engraved the moment it is mastered.",
                "Change a node's retention model and complexity in Edit tree mode.",
            ]),
        new("Difficulty",
            "Siege settings (tray menu or the Siege button) choose how hard the siege hits. Every knob can also be changed on its own.",
            [
                .. DifficultyPresets.All.Select(preset =>
                    $"{preset.Difficulty}: damage ×{preset.DamageMultiplier:0.##}, up to {preset.AttacksPerDay} attacks a day, rest days {RestDays(preset.RestDays)}, ward cap {preset.WardCap}, " +
                    (preset.ZeroIntegrity switch
                    {
                        ZeroIntegrityEffect.Nothing => "nothing happens at 0 integrity.",
                        ZeroIntegrityEffect.Dim => $"a node at 0 fades (keeps its XP) until {preset.CleanseSessions} practice sessions.",
                        _ => $"a node at 0 is corrupted and its mastery XP withheld until {preset.CleanseSessions} practice sessions.",
                    })),
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
                "Recalls of Memory nodes count as practice and earn XP, but not towards goals.",
                "Made a mistake? Press Undo next to the entry under Recent entries in the side panel, or next to \"Last:\" on the widget.",
                "Undo does not erase anything: it adds a small entry that cancels the original. That keeps the history honest and makes syncing between your machines safe.",
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
                "Drag the widget by its title row to put it anywhere; LifeCharter remembers the spot. Tray menu → Widget position snaps it back to a corner.",
                "Peek shows the widget on top of everything: tray menu → Peek widget, or run lifecharter peek. Esc or a click elsewhere hides it.",
                "Clicking the tray icon opens the tree. Closing the tree keeps LifeCharter running in the tray; use Quit in the tray menu to exit.",
                "Tick Start on login in the tray menu to have LifeCharter start with your desktop.",
                "Commands: lifecharter tree, lifecharter peek, lifecharter help, lifecharter quit. Bind them to keyboard shortcuts in your desktop settings.",
            ]),
        new("Coming next",
            "Planned in docs/PLAN.md:",
            [
                "Quick entry: type \"run 5\" or a sentence in the widget.",
                "Notifications with the morning threat report, and bosses: weekly self-tests per attribute.",
                "Sync between your machines through Syncthing, and an AI coach for goal planning.",
            ]),
    ];

    public static IReadOnlyList<LevelRow> Levels { get; } = BuildLevels();

    private static string RestDays(Weekdays days) => days == Weekdays.None ? "none" : days.ToString().ToLowerInvariant();

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
