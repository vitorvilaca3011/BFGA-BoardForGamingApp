using Avalonia.Controls;

namespace BFGA.App.Views;

public partial class ConnectionScreen : UserControl
{
    public ConnectionScreen()
    {
        InitializeComponent();
    }

    public ConnectionView ConnectionView => connectionView;
}
