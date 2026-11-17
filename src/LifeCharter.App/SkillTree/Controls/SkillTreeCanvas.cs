using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using LifeCharter.App.SkillTree.Models;
using LifeCharter.Core.Common.Models;
using LifeCharter.Core.Progression.Models;
using LifeCharter.Core.SkillTree.Models;

namespace LifeCharter.App.SkillTree.Controls;

/// <summary>
/// Draws the skill tree with pan, zoom, viewport culling and level of detail. Renders only on
/// input, data change or while a short animation runs, so an idle tree costs no frames.
/// In edit mode: drag a node to move it, Shift-drag between nodes to connect or disconnect
/// them, double-click empty space to create a node.
/// </summary>
public sealed class SkillTreeCanvas : Control
{
    private const double CellSize = 256;
    private const double MinZoom = 0.08;
    private const double MaxZoom = 4;
    private const double DetailZoom = 0.4;
    private const double LabelZoom = 0.55;
    private const double DragThreshold = 4;

    /// <summary>Space between a node's border and the start of its connecting line.</summary>
    private const double EdgeGap = 3;
    private static readonly TimeSpan PulseDuration = TimeSpan.FromMilliseconds(650);

    private static readonly IImmutableSolidColorBrush BackgroundBrush = new ImmutableSolidColorBrush(Color.Parse("#0E0F13"));
    private static readonly IImmutableSolidColorBrush LockedFill = new ImmutableSolidColorBrush(Color.Parse("#1C1F27"));
    private static readonly IImmutableSolidColorBrush GoldBrush = new ImmutableSolidColorBrush(Color.Parse("#E8C36A"));
    private static readonly IImmutableSolidColorBrush LabelBrush = new ImmutableSolidColorBrush(Color.Parse("#C8CCD6"));
    private static readonly IImmutableSolidColorBrush DimLabelBrush = new ImmutableSolidColorBrush(Color.Parse("#6B7080"));
    private static readonly ImmutablePen EdgePen = new(new ImmutableSolidColorBrush(Color.Parse("#2A2E38")), 3);
    private static readonly ImmutablePen RequiresPen = new(new ImmutableSolidColorBrush(Color.Parse("#4A4F5C")), 2, new ImmutableDashStyle([4, 3], 0));
    private static readonly ImmutablePen LockedOutline = new(new ImmutableSolidColorBrush(Color.Parse("#2E323D")), 2);
    private static readonly ImmutablePen GoldPen = new(GoldBrush, 3);
    private static readonly ImmutablePen ProgressPen = new(GoldBrush, 4, lineCap: PenLineCap.Round);
    private static readonly ImmutablePen SelectionPen = new(new ImmutableSolidColorBrush(Colors.White, 0.85), 2);
    private static readonly ImmutablePen ConnectPen = new(new ImmutableSolidColorBrush(Colors.White, 0.6), 2, new ImmutableDashStyle([6, 4], 0));
    private static readonly Typeface LabelTypeface = new(FontFamily.Default);

    private static readonly Dictionary<LifeAttribute, Color> AttributeColors = new()
    {
        [LifeAttribute.Health] = Color.Parse("#4CAF50"),
        [LifeAttribute.Stamina] = Color.Parse("#E5484D"),
        [LifeAttribute.Might] = Color.Parse("#3E8EF7"),
    };

    private static readonly Dictionary<LifeAttribute, IImmutableSolidColorBrush> MasteredFills = Brushes(1);
    private static readonly Dictionary<LifeAttribute, IImmutableSolidColorBrush> TrainingFills = Brushes(0.3);
    private static readonly Dictionary<LifeAttribute, ImmutablePen> AvailablePens = Pens(1, 3);
    private static readonly Dictionary<LifeAttribute, ImmutablePen> MasteredEdgePens = Pens(0.85, 4);
    private static readonly Dictionary<LifeAttribute, ImmutablePen> FrontierEdgePens = Pens(0.35, 3);

    private readonly Dictionary<Guid, CanvasNode> nodes = [];
    private readonly List<CanvasEdge> edges = [];
    private readonly Dictionary<GridCell, List<Guid>> grid = [];
    private readonly Dictionary<Guid, FormattedText> labels = [];
    private readonly Dictionary<Guid, Geometry> progressArcs = [];
    private readonly List<NodePulse> pulses = [];
    private readonly Stopwatch clock = Stopwatch.StartNew();

    private double zoom = 0.9;
    private Vector offset;
    private bool centered;
    private Point? pressPosition;
    private Vector pressOffset;
    private Guid? pressedNode;
    private Point pressedNodeStart;
    private bool connecting;
    private Point connectEnd;
    private bool dragging;
    private bool frameRequested;

    public static readonly StyledProperty<bool> IsEditingProperty =
        AvaloniaProperty.Register<SkillTreeCanvas, bool>(nameof(IsEditing));

    public SkillTreeCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public bool IsEditing
    {
        get => GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    public Guid? SelectedNodeId { get; private set; }

    public int NodeCount => nodes.Count;

    /// <summary>How many times <see cref="Render"/> ran; stays flat while the tree is idle.</summary>
    public long FramesRendered { get; private set; }

    public int VisibleNodes { get; private set; }

    public double Zoom => zoom;

    public event EventHandler<TreeCanvasEventArgs>? NodeSelected;

    public event EventHandler<TreeCanvasEventArgs>? NodeMoved;

    public event EventHandler<TreeCanvasEventArgs>? ConnectRequested;

    public event EventHandler<TreeCanvasEventArgs>? CreateRequested;

    /// <summary>Replaces what is drawn. Nodes whose state changed get a short pulse.</summary>
    public void SetChart(LifeChartData chart, ProgressionResult progression)
    {
        var previousStates = nodes.Values.ToDictionary(node => node.Id, node => node.State);
        nodes.Clear();
        edges.Clear();
        grid.Clear();
        labels.Clear();
        progressArcs.Clear();

        foreach (var node in chart.Nodes)
        {
            if (!progression.Nodes.TryGetValue(node.Id, out var progress))
                continue;

            var canvasNode = new CanvasNode(node.Id, new Point(node.X, node.Y), node.Kind, node.Attribute, node.Title, progress.State, progress.Fraction);
            nodes[node.Id] = canvasNode;
            CellOf(canvasNode.Position).Add(node.Id);

            if (previousStates.TryGetValue(node.Id, out var before) && before != progress.State)
                pulses.Add(new NodePulse(node.Id, clock.Elapsed));
        }

        foreach (var edge in chart.Edges)
        {
            if (nodes.ContainsKey(edge.FromNodeId) && nodes.ContainsKey(edge.ToNodeId))
                edges.Add(new CanvasEdge(edge.FromNodeId, edge.ToNodeId, edge.Kind));
        }

        if (SelectedNodeId is { } selected && !nodes.ContainsKey(selected))
            Select(null);

        InvalidateVisual();
    }

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
            foreach (var edge in edges)
                DrawEdge(context, edge, world);

            var visible = 0;
            foreach (var id in NodesIn(world))
            {
                visible++;
                DrawNode(context, nodes[id], detailed);
            }

            VisibleNodes = visible;

            if (connecting && pressedNode is { } from && TrimmedLine(nodes[from], connectEnd, 0) is { } line)
                context.DrawLine(ConnectPen, line.Start, line.End);

            DrawPulses(context);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var screen = e.GetPosition(this);
        pressPosition = screen;
        pressOffset = offset;
        dragging = false;
        pressedNode = HitTest(screen)?.Id;
        connecting = IsEditing && pressedNode is not null && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (pressedNode is { } id)
            pressedNodeStart = nodes[id].Position;

        if (IsEditing && pressedNode is null && e.ClickCount == 2)
        {
            CreateRequested?.Invoke(this, new TreeCanvasEventArgs(SelectedNodeId, null, ToWorld(screen)));
            pressPosition = null;
            return;
        }

        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (pressPosition is not { } start)
            return;

        var screen = e.GetPosition(this);
        var delta = screen - start;
        if (!dragging && Math.Abs(delta.X) + Math.Abs(delta.Y) < DragThreshold)
            return;

        dragging = true;
        if (connecting)
            connectEnd = ToWorld(screen);
        else if (IsEditing && pressedNode is { } id)
            MoveNode(id, pressedNodeStart + delta / zoom);
        else
            offset = pressOffset + delta;

        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (pressPosition is null)
            return;

        var screen = e.GetPosition(this);
        if (!dragging)
        {
            Select(pressedNode);
        }
        else if (connecting && pressedNode is { } from && HitTest(screen) is { } target && target.Id != from)
        {
            ConnectRequested?.Invoke(this, new TreeCanvasEventArgs(from, target.Id, ToWorld(screen)));
        }
        else if (IsEditing && !connecting && pressedNode is { } moved)
        {
            NodeMoved?.Invoke(this, new TreeCanvasEventArgs(moved, null, nodes[moved].Position));
        }

        pressPosition = null;
        pressedNode = null;
        connecting = false;
        dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
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

    public void SelectNode(Guid? nodeId) => Select(nodeId);

    private void Select(Guid? nodeId)
    {
        SelectedNodeId = nodeId;
        NodeSelected?.Invoke(this, new TreeCanvasEventArgs(nodeId, null, default));
        InvalidateVisual();
    }

    private void DrawEdge(DrawingContext context, CanvasEdge edge, Rect world)
    {
        var from = nodes[edge.From];
        var to = nodes[edge.To];
        if (!world.Intersects(new Rect(from.Position, to.Position).Normalize()))
            return;

        // Lines run from border to border, never through a node.
        if (TrimmedLine(from, to.Position, to.Radius) is not { } line)
            return;

        if (edge.Kind == EdgeKind.Requires)
        {
            context.DrawLine(RequiresPen, line.Start, line.End);
            return;
        }

        var fromMastered = from.State == NodeState.Mastered;
        var toMastered = to.State == NodeState.Mastered;
        var colorSource = to.Kind == NodeKind.Root ? from : to;
        var pen = fromMastered && toMastered
            ? MasteredEdgePens[colorSource.Attribute]
            : fromMastered || toMastered ? FrontierEdgePens[colorSource.Attribute] : EdgePen;
        context.DrawLine(pen, line.Start, line.End);
    }

    /// <summary>
    /// The segment from <paramref name="from"/>'s border towards <paramref name="target"/>,
    /// stopping <paramref name="targetRadius"/> short of it. Null when the circles overlap.
    /// </summary>
    private static EdgeLine? TrimmedLine(CanvasNode from, Point target, double targetRadius)
    {
        var delta = target - from.Position;
        var length = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        var startOffset = from.Radius + EdgeGap;
        var endOffset = targetRadius > 0 ? targetRadius + EdgeGap : 0;
        if (length <= startOffset + endOffset)
            return null;

        var direction = delta / length;
        return new EdgeLine(from.Position + direction * startOffset, target - direction * endOffset);
    }

    private void DrawNode(DrawingContext context, CanvasNode node, bool detailed)
    {
        var (fill, outline) = node.Kind == NodeKind.Root
            ? ((IBrush)GoldBrush, (IPen?)null)
            : node.State switch
            {
                NodeState.Mastered => (MasteredFills[node.Attribute], node.Kind is NodeKind.Notable or NodeKind.Keystone ? GoldPen : null),
                NodeState.Training => (TrainingFills[node.Attribute], AvailablePens[node.Attribute]),
                NodeState.Available => (LockedFill, AvailablePens[node.Attribute]),
                _ => ((IBrush)LockedFill, (IPen?)LockedOutline),
            };

        // The training tint is translucent; an opaque base keeps the background out of the node.
        if (node.State == NodeState.Training)
            context.DrawEllipse(LockedFill, null, node.Position, node.Radius, node.Radius);

        context.DrawEllipse(fill, detailed ? outline : null, node.Position, node.Radius, node.Radius);

        if (!detailed)
            return;

        if (node.State == NodeState.Training && node.Fraction > 0)
            context.DrawGeometry(null, ProgressPen, ProgressArc(node));

        if (node.Id == SelectedNodeId)
            context.DrawEllipse(null, SelectionPen, node.Position, node.Radius + 9, node.Radius + 9);

        if (zoom >= LabelZoom)
        {
            var label = Label(node);
            context.DrawText(label, node.Position + new Vector(-label.Width / 2, node.Radius + 6));
        }
    }

    private Geometry ProgressArc(CanvasNode node)
    {
        if (progressArcs.TryGetValue(node.Id, out var cached))
            return cached;

        var radius = node.Radius + 5;
        var sweep = Math.Min(node.Fraction, 0.999) * 2 * Math.PI;
        var start = new Point(node.Position.X, node.Position.Y - radius);
        var end = new Point(node.Position.X + radius * Math.Sin(sweep), node.Position.Y - radius * Math.Cos(sweep));

        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(start, isFilled: false);
            figure.ArcTo(end, new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
            figure.EndFigure(isClosed: false);
        }

        progressArcs[node.Id] = geometry;
        return geometry;
    }

    private void DrawPulses(DrawingContext context)
    {
        if (pulses.Count == 0)
            return;

        var now = clock.Elapsed;
        pulses.RemoveAll(pulse => now - pulse.StartedAt > PulseDuration || !nodes.ContainsKey(pulse.NodeId));

        foreach (var pulse in pulses)
        {
            var node = nodes[pulse.NodeId];
            var progress = (now - pulse.StartedAt) / PulseDuration;
            var radius = node.Radius + 40 * progress;
            var color = node.Kind == NodeKind.Root ? Color.Parse("#E8C36A") : AttributeColors[node.Attribute];
            context.DrawEllipse(null, new ImmutablePen(new ImmutableSolidColorBrush(color, 1 - progress), 3), node.Position, radius, radius);
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

    private void MoveNode(Guid id, Point position)
    {
        var node = nodes[id];
        grid[KeyOf(node.Position)].Remove(id);
        node.Position = position;
        CellOf(position).Add(id);
        progressArcs.Remove(id);
    }

    private FormattedText Label(CanvasNode node)
    {
        if (!labels.TryGetValue(node.Id, out var text))
        {
            var brush = node.State == NodeState.Locked ? DimLabelBrush : LabelBrush;
            text = new FormattedText(node.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelTypeface, 13, brush);
            labels[node.Id] = text;
        }

        return text;
    }

    private Point ToWorld(Point screen) => (Point)((screen - offset) / zoom);

    private Rect VisibleWorld() => new Rect(ToWorld(default), Bounds.Size / zoom).Inflate(60);

    private CanvasNode? HitTest(Point screen)
    {
        var world = ToWorld(screen);
        CanvasNode? best = null;
        var bestDistance = double.MaxValue;

        foreach (var id in NodesIn(new Rect(world, world).Inflate(60)))
        {
            var node = nodes[id];
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

    private IEnumerable<Guid> NodesIn(Rect world)
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

            foreach (var id in cell)
                yield return id;
        }
    }

    private static GridCell KeyOf(Point position) =>
        new((int)Math.Floor(position.X / CellSize), (int)Math.Floor(position.Y / CellSize));

    private List<Guid> CellOf(Point position)
    {
        var key = KeyOf(position);
        if (!grid.TryGetValue(key, out var cell))
        {
            cell = [];
            grid[key] = cell;
        }

        return cell;
    }

    private readonly record struct EdgeLine(Point Start, Point End);

    private static Dictionary<LifeAttribute, IImmutableSolidColorBrush> Brushes(double opacity) =>
        AttributeColors.ToDictionary(pair => pair.Key, pair => (IImmutableSolidColorBrush)new ImmutableSolidColorBrush(pair.Value, opacity));

    private static Dictionary<LifeAttribute, ImmutablePen> Pens(double opacity, double thickness) =>
        AttributeColors.ToDictionary(pair => pair.Key, pair => new ImmutablePen(new ImmutableSolidColorBrush(pair.Value, opacity), thickness));
}
