using System.Reflection;
using BFGA.App;
using BFGA.App.ViewModels;
using Avalonia.Input;

namespace BFGA.App.Tests;

public class MainWindowShortcutTests
{
    [Fact]
    public void OnKeyDown_IgnoresModifiedShortcuts()
    {
        var boardScreen = new BoardScreenViewModel(new MainViewModel());

        var handled = MainWindow.TryHandleToolShortcut(boardScreen, Key.V, KeyModifiers.Control);

        Assert.False(handled);
        Assert.Equal("Select", boardScreen.SelectedToolText);
    }
}
