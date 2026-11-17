using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LifeCharter.App.SkillTree.Business;
using LifeCharter.App.SkillTree.Models;

namespace LifeCharter.App.SkillTree.Controls;

/// <summary>
/// Draws the skill tree with pan, zoom, viewport culling and level of detail. It renders
/// only on input, data change or while a short animation runs, so an idle tree costs no frames.
/// </summary>
public sealed class SkillTreeCanvas : Control
{
    private const double CellSize = 256;
    private const double MinZoom = 0.08;
    private const double MaxZoom = 4;
    private const double DetailZoom = 0.4;
    private const double DragThreshold = 4;
    private static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(650);

    private static readonly IImmutableSolidColorBrush BackgroundBrush = new ImmutableSolidColorBrush(Color.Parse("#0E0F13"));
    private static readonly IImmutableSolidColorBrush LockedFill = new ImmutableSolidColorBrush(Color.Parse("#1C1F27"));
    private static readonly IImmutableSolidColorBrush LabelBrush = new ImmutableSolidColorBrush(Color.Parse("#C8CCD6"));
    private static readonly ImmutablePen EdgePen = new(new ImmutableSolidColorBrush(Color.Parse("#2A2E38")), 3);
    private static readonly ImmutablePen GoldPen = new(new ImmutableSolidColorBrush(Color.Parse("#E8C36A")), 3);
    private static readonly Typeface LabelTypeface = new(FontFamily.Default);

    private static readonly Dictionary<TreeAttribute, Color> AttributeColors = new()
    {
        [TreeAttribute.Health] = Color.Parse("#4CAF50"),
        [TreeAttribute.Stamina] = Color.Parse("#E5484D"),
        [TreeAttribute.Might] = Color.Parse("#3E8EF7"),
    };

    private static readonly Dictionary<TreeAttribute, IImmutableSolidColorBrush> AttributeBrushes =
        AttributeColors.ToDictionary(pair => pair.Key, pair => (IImmutableSolidColorBrush)new ImmutableSolidColorBrush(pair.Value));

    private static readonly Dictionary<TreeAttribute, ImmutablePen> AttributeOutlinePens =
        AttributeColors.ToDictionary(pair => pair.Key, pair => new ImmutablePen(new ImmutableSolidColorBrush(pair.Value, 0.55), 2));

    private static readonly Dictionary<TreeAttribute, ImmutablePen> AllocatedEdgePens =
        AttributeColors.ToDictionary(pair => pair.Key, pair => new ImmutablePen(new ImmutableSolidColorBrush(pair.Value, 0.8), 4));

    private readonly TreeLayout layout;
    private readonly Dictionary<GridCell, List<int>> grid = [];
    private readonly Dictionary<int, FormattedText> labels = [];
    private readonly List<NodePulse> pulses = [];
    private readonly Stopwatch clock = Stopwatch.StartNew();

    private double zoom = 0.3;
    private Vector offset;
    private bool centered;
    private Point? pressPosition;
    private Vector pressOffset;
    private bool dragging;
    private bool frameRequested;

    public SkillTreeCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        layout = TreeLayoutGenerator.Generate(1000);
        foreach (var node in layout.Nodes)
            CellsOf(node.Position).Add(node.Index);
    }

    public int NodeCount => layout.Nodes.Count;

    /// <summary>How many times <see cref="Render"/> ran; stays flat while the tree is idle.</summary>
    public long FramesRendered { get; private set; }

    public int VisibleNodes { get; private set; }

    public double Zoom => zoom;

    public override void Render(DrawingContext context)
    {
        FramesRendered++;

        if (!centered && Bounds.Width > 0)
        {
            offset = new Vector(Bounds.Width / 2, Bounds.Height / 2);
            centered = true;
        }

        context.FillRectangle(BackgroundBrush, new Rect(Bounds.Size));
        var world = VisibleWorld();
        var detailed = zoom >= DetailZoom;

        using (context.PushTransform(Matrix.CreateScale(zoom, zoom) * Matrix.CreateTranslation(offset)))
        {
            foreach (var edge in layout.Edges)
            {
                var from = layout.Nodes[edge.From];
                var to = layout.Nodes[edge.To];
                if (!world.Intersects(new Rect(from.Position, to.Position).Normalize()))
                    continue;

                var pen = from.Allocated && to.Allocated ? AllocatedEdgePens[to.Attribute] : EdgePen;
                context.DrawLine(pen, from.Position, to.Position);
            }

            var visible = 0;
            foreach (var index in NodesIn(world))
            {
                var node = layout.Nodes[index];
                visible++;
                var fill = node.Allocated ? AttributeBrushes[node.Attribute] : LockedFill;

                if (!detailed)
                {
                    context.DrawEllipse(fill, null, node.Position, node.Radius, node.Radius);
                    continue;
                }

                var outline = node.Kind is NodeKind.Notable or NodeKind.Keystone or NodeKind.Root
                    ? GoldPen
                    : AttributeOutlinePens[node.Attribute];
                context.DrawEllipse(fill, outline, node.Position, node.Radius, node.Radius);

                if (ShowsLabel(node))
                    context.DrawText(Label(node), node.Position + new Vector(node.Radius + 6, -9));
            }

            VisibleNodes = visible;
            DrawPulses(context);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        pressPosition = e.GetPosition(this);
        pressOffset = offset;
        dragging = false;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pressPosition is not { } start)
            return;

        var delta = e.GetPosition(this) - start;
        if (!dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) < DragThreshold)
            return;

        dragging = true;
        offset = pressOffset + delta;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pressPosition is not null && !dragging && HitTest(e.GetPosition(this)) is { } node && node.Kind != NodeKind.Root)
        {
            node.Allocated = !node.Allocated;
            pulses.Add(new NodePulse(node.Index, clock.Elapsed));
            InvalidateVisual();
        }

        pressPosition = null;
        dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var anchor = (Vector)e.GetPosition(this);
        var newZoom = Math.Clamp(zoom * Math.Pow(1.2, e.Delta.Y), MinZoom, MaxZoom);
        offset = anchor - (anchor - offset) * (newZoom / zoom);
        zoom = newZoom;
        InvalidateVisual();
        e.Handled = true;
    }

    private void DrawPulses(DrawingContext context)
    {
        if (pulses.Count == 0)
            return;

        var now = clock.Elapsed;
        pulses.RemoveAll(pulse => now - pulse.StartedAt > PulseDuration);

        foreach (var pulse in pulses)
        {
            var node = layout.Nodes[pulse.NodeIndex];
            var progress = (now - pulse.StartedAt) / PulseDuration;
            var radius = node.Radius + 34 * progress;
            var pen = new ImmutablePen(new ImmutableSolidColorBrush(AttributeColors[node.Attribute], 1 - progress), 3);
            context.DrawEllipse(null, pen, node.Position, radius, radius);
        }

        if (pulses.Count > 0)
            RequestAnimationFrame();
    }

    private void RequestAnimationFrame()
    {
        if (frameRequested || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        frameRequested = true;
        topLevel.RequestAnimationFrame(_ =>
        {
            frameRequested = false;
            InvalidateVisual();
        });
    }

    private bool ShowsLabel(TreeNode node) =>
        node.Kind == NodeKind.Small ? zoom >= 1.8 : zoom >= 0.9;

    private FormattedText Label(TreeNode node)
    {
        if (!labels.TryGetValue(node.Index, out var text))
        {
            text = new FormattedText(node.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelTypeface, 13, LabelBrush);
            labels[node.Index] = text;
        }

        return text;
    }

    private Rect VisibleWorld()
    {
        var topLeft = (Point)(-offset / zoom);
        return new Rect(topLeft, Bounds.Size / zoom).Inflate(40);
    }

    private TreeNode? HitTest(Point screen)
    {
        var world = (Point)((screen - offset) / zoom);
        TreeNode? best = null;
        var bestDistance = double.MaxValue;

        foreach (var index in NodesIn(new Rect(world, world).Inflate(40)))
        {
            var node = layout.Nodes[index];
            var delta = node.Position - world;
            var distance = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            if (distance <= node.Radius + 6 && distance < bestDistance)
            {
                best = node;
                bestDistance = distance;
            }
        }

        return best;
    }

    private IEnumerable<int> NodesIn(Rect world)
    {
        var minX = (int)Math.Floor(world.Left / CellSize);
        var maxX = (int)Math.Floor(world.Right / CellSize);
        var minY = (int)Math.Floor(world.Top / CellSize);
        var maxY = (int)Math.Floor(world.Bottom / CellSize);

        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        {
            if (!grid.TryGetValue(new GridCell(x, y), out var cell))
                continue;

            foreach (var index in cell)
                yield return index;
        }
    }

    private List<int> CellsOf(Point position)
    {
        var key = new GridCell((int)Math.Floor(position.X / CellSize), (int)Math.Floor(position.Y / CellSize));
        if (!grid.TryGetValue(key, out var cell))
        {
            cell = [];
            grid[key] = cell;
        }

        return cell;
    }
}
