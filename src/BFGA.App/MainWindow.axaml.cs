using System;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using BFGA.App.Services;
using BFGA.App.ViewModels;

namespace BFGA.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            DataContext = new MainViewModel(new AvaloniaFileDialogService(this));
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StartPolling();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        CloseDataContext(DataContext);

        base.OnClosed(e);
    }

    public static void CloseDataContext(object? dataContext)
    {
        if (dataContext is MainViewModel viewModel)
        {
            viewModel.CloseAsync().GetAwaiter().GetResult();
            return;
        }

        (dataContext as IDisposable)?.Dispose();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel { CurrentScreen: BoardScreenViewModel boardScreen } vm)
        {
            return;
        }

        // Check Ctrl+Shift+Z FIRST (most specific), then Ctrl+Z, then Ctrl+Y
        if (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            if (vm.RedoCommand.CanExecute(null)) vm.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
        {
            if (vm.UndoCommand.CanExecute(null)) vm.UndoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
        {
            if (vm.RedoCommand.CanExecute(null)) vm.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (!TryHandleToolShortcut(boardScreen, e.Key, e.KeyModifiers))
        {
            return;
        }

        e.Handled = true;
    }

    public static bool TryHandleToolShortcut(BoardScreenViewModel boardScreen, Key key, KeyModifiers modifiers)
    {
        if (modifiers != KeyModifiers.None)
        {
            return false;
        }

        switch (key)
        {
            case Key.V:
                boardScreen.SelectToolCommand.Execute(null);
                return true;
            case Key.H:
                boardScreen.HandToolCommand.Execute(null);
                return true;
            case Key.P:
                boardScreen.PenToolCommand.Execute(null);
                return true;
            case Key.R:
                boardScreen.RectangleToolCommand.Execute(null);
                return true;
            case Key.E:
                boardScreen.EllipseToolCommand.Execute(null);
                return true;
            case Key.I:
                boardScreen.ImageToolCommand.Execute(null);
                return true;
            case Key.X:
                boardScreen.EraserToolCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }
}
