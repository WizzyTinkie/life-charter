using Avalonia.Controls;

namespace LifeCharter.App.Widget.Views;

public partial class WidgetContent : UserControl
{
    public WidgetContent() => InitializeComponent();

    /// <summary>The title row; windows hosting the widget use it as a drag handle.</summary>
    public Control DragHandle => Header;
}
