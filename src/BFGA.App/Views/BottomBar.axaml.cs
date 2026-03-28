using Avalonia;
using Avalonia.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BFGA.App.Views;

public partial class BottomBar : UserControl, INotifyPropertyChanged
{
    public BoardView? BoardView
    {
        get => _boardView;
        set
        {
            _boardView = value;
            BoardViewChanged?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged();
        }
    }

    public event EventHandler? BoardViewChanged;
    public new event PropertyChangedEventHandler? PropertyChanged;

    private BoardView? _boardView;

    public BottomBar()
    {
        InitializeComponent();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
