using System;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using BFGA.App.Services;
using BFGA.App.ViewModels;

namespace BFGA.App;

public partial class MainWindow : Window
{
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            DataContext = new MainViewModel(new AvaloniaFileDialogService(this));
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StartPolling();
                Closing += OnWindowClosing;
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (!_isClosing && DataContext is IDisposable disposable && DataContext is not MainViewModel)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isClosing)
            return;

        e.Cancel = true;
        _isClosing = true;

        try
        {
            Task cleanupTask = Task.CompletedTask;
            if (DataContext is MainViewModel viewModel)
                cleanupTask = viewModel.CloseAsync();

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
            var winner = await Task.WhenAny(cleanupTask, timeoutTask);

            if (winner == cleanupTask)
                await cleanupTask;
        }
        catch (Exception)
        {
        }

        Close();
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

    private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsSettingsOpen = !vm.IsSettingsOpen;
    }

    private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
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
            case Key.A:
                boardScreen.ArrowToolCommand.Execute(null);
                return true;
            case Key.L:
                boardScreen.LineToolCommand.Execute(null);
                return true;
            case Key.T:
                boardScreen.TextToolCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }
}
