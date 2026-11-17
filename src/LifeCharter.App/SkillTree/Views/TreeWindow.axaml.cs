using Avalonia.Controls;
using Avalonia.Threading;
using LifeCharter.App.Logging.Business;
using Microsoft.Extensions.Logging;

namespace LifeCharter.App.SkillTree.Views;

public partial class TreeWindow : Window
{
    private readonly ILogger<TreeWindow> logger = AppLogging.CreateLogger<TreeWindow>();
    private readonly DispatcherTimer statsTimer;
    private long lastFrames = -1;

    public TreeWindow()
    {
        InitializeComponent();

        // Developer overlay: updated only when the frame count changed, so the overlay itself
        // never causes frames while the tree is idle.
        statsTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateStats());
        Opened += (_, _) => statsTimer.Start();
        Closed += (_, _) => statsTimer.Stop();
    }

    private void UpdateStats()
    {
        var frames = Tree.FramesRendered;
        if (frames == lastFrames)
            return;

        var perSecond = lastFrames < 0 ? frames : frames - lastFrames;
        lastFrames = frames;
        Stats.Text = $"{perSecond} frames/s · {frames} total · {Tree.VisibleNodes}/{Tree.NodeCount} nodes visible · zoom {Tree.Zoom:F2}";
        logger.LogDebug("Tree rendered {Frames} frames in the last second ({Visible} nodes visible)", perSecond, Tree.VisibleNodes);
    }
}
