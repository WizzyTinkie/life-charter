using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LifeCharter.App.Logging.Business;
using LifeCharter.App.SkillTree.Models;
using LifeCharter.App.SkillTree.ViewModels;
using LifeCharter.Core.SkillTree.Business;
using LifeCharter.Core.SkillTree.Models;
using Microsoft.Extensions.Logging;

namespace LifeCharter.App.SkillTree.Views;

public partial class TreeWindow : Window
{
    private const double AutoConnectDistance = 300;

    private readonly ILogger<TreeWindow> logger = AppLogging.CreateLogger<TreeWindow>();
    private readonly DispatcherTimer statsTimer;
    private readonly TreeWindowViewModel? viewModel;
    private long lastFrames = -1;

    // Used by the IDE previewer.
    public TreeWindow()
    {
        InitializeComponent();

        // Developer overlay: updated only when the frame count changed, so it never causes
        // frames while the tree is idle.
        statsTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateStats());
        Opened += (_, _) => statsTimer.Start();
        Closed += (_, _) => statsTimer.Stop();
    }

    public TreeWindow(TreeWindowViewModel viewModel) : this()
    {
        this.viewModel = viewModel;
        DataContext = viewModel;

        var session = viewModel.Session;
        Tree.SetChart(session.Chart, session.Progression);
        session.Changed += OnSessionChanged;
        Closed += (_, _) =>
        {
            session.Changed -= OnSessionChanged;
            viewModel.Dispose();
        };

        Tree.NodeSelected += (_, e) => viewModel.SelectedNodeId = e.NodeId;
        Tree.NodeMoved += (_, e) => _ = RunAsync(() => session.MoveNodeAsync(e.NodeId!.Value, e.World.X, e.World.Y));
        Tree.ConnectRequested += (_, e) => _ = RunAsync(() => session.ToggleEdgeAsync(e.NodeId!.Value, e.OtherNodeId!.Value));
        Tree.CreateRequested += (_, e) => _ = RunAsync(() => CreateNodeAsync(e));
    }

    private void OnSessionChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (viewModel is not null)
                Tree.SetChart(viewModel.Session.Chart, viewModel.Session.Progression);
        });

    private async Task CreateNodeAsync(TreeCanvasEventArgs e)
    {
        var session = viewModel!.Session;
        var connectTo = e.NodeId ?? NearestNode(e.World);
        var node = await session.CreateNodeAsync(AttributeAt(e.World), e.World.X, e.World.Y, connectTo);
        Tree.SelectNode(node.Id);
    }

    private Guid? NearestNode(Point world) =>
        viewModel!.Session.Chart.Nodes
            .Select(node => new { node.Id, Distance = Math.Sqrt(Math.Pow(node.X - world.X, 2) + Math.Pow(node.Y - world.Y, 2)) })
            .Where(candidate => candidate.Distance <= AutoConnectDistance)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => (Guid?)candidate.Id)
            .FirstOrDefault();

    /// <summary>The attribute whose sector contains the point.</summary>
    private static LifeAttribute AttributeAt(Point world)
    {
        var degrees = Math.Atan2(world.Y, world.X) * 180 / Math.PI;
        return Enum.GetValues<LifeAttribute>()
            .OrderBy(attribute => AngularDistance(degrees, StarterTemplate.SectorCenterDegrees(attribute)))
            .First();
    }

    private static double AngularDistance(double a, double b)
    {
        var difference = Math.Abs(a - b) % 360;
        return difference > 180 ? 360 - difference : difference;
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tree action failed");
        }
    }

    private void UpdateStats()
    {
        var frames = Tree.FramesRendered;
        if (frames == lastFrames)
            return;

        var perSecond = lastFrames < 0 ? frames : frames - lastFrames;
        lastFrames = frames;
        Stats.Text = $"{perSecond} frames/s · {Tree.VisibleNodes}/{Tree.NodeCount} nodes · zoom {Tree.Zoom:F2}";
    }
}
