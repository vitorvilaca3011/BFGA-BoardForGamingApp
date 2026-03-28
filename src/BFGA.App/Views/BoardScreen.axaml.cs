using Avalonia.Controls;

namespace BFGA.App.Views;

public partial class BoardScreen : UserControl
{
    public BoardScreen()
    {
        InitializeComponent();
    }

    public BoardView BoardView => boardView;

    public BottomBar BottomBar => bottomBar;

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        bottomBar.BoardView = boardView;
    }
}
