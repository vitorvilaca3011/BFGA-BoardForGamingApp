using System.Reflection;
using System.Threading.Tasks;
using BFGA.App;
using BFGA.App.ViewModels;
using Avalonia.Controls;
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

    [Theory]
    [InlineData(Key.Delete, KeyModifiers.None, true)]
    [InlineData(Key.Delete, KeyModifiers.Control, false)]
    [InlineData(Key.Back, KeyModifiers.None, false)]
    public void TryHandleDeleteShortcut_ReturnsExpectedResult(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.TryHandleDeleteShortcut(key, modifiers));
    }

    [Fact]
    public void ShouldSuppressBoardShortcuts_ReturnsTrueForTextInput()
    {
        Assert.True(MainWindow.ShouldSuppressBoardShortcuts(new TextBox()));
    }

    [Theory]
    [InlineData(Key.V, KeyModifiers.Control, true)]
    [InlineData(Key.V, KeyModifiers.None, false)]
    [InlineData(Key.V, KeyModifiers.Shift, false)]
    [InlineData(Key.P, KeyModifiers.Control, false)]
    public void TryHandlePasteShortcut_ReturnsExpectedResult(Key key, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.TryHandlePasteShortcut(key, modifiers));
    }

    [Fact]
    public async Task TryHandleBoardShortcutAsync_InvokesDeleteCallback()
    {
        var invoked = false;
        var pasteInvoked = false;

        var handled = await MainWindow.TryHandleBoardShortcutAsync(
            Key.Delete,
            KeyModifiers.None,
            focusedElement: null,
            deleteSelection: () => invoked = true,
            pasteImage: () =>
            {
                pasteInvoked = true;
                return Task.CompletedTask;
            });

        Assert.True(handled);
        Assert.True(invoked);
        Assert.False(pasteInvoked);
    }

    [Fact]
    public async Task TryHandleBoardShortcutAsync_DoesNotInvokeDeleteWhenTextBoxFocused()
    {
        var invoked = false;
        var pasteInvoked = false;

        var handled = await MainWindow.TryHandleBoardShortcutAsync(
            Key.Delete,
            KeyModifiers.None,
            focusedElement: new TextBox(),
            deleteSelection: () => invoked = true,
            pasteImage: () =>
            {
                pasteInvoked = true;
                return Task.CompletedTask;
            });

        Assert.False(handled);
        Assert.False(invoked);
        Assert.False(pasteInvoked);
    }

    [Fact]
    public async Task TryHandleBoardShortcutAsync_InvokesPasteCallback()
    {
        var deleteInvoked = false;
        var pasteInvoked = false;

        var handled = await MainWindow.TryHandleBoardShortcutAsync(
            Key.V,
            KeyModifiers.Control,
            focusedElement: null,
            deleteSelection: () => deleteInvoked = true,
            pasteImage: () =>
            {
                pasteInvoked = true;
                return Task.CompletedTask;
            });

        Assert.True(handled);
        Assert.False(deleteInvoked);
        Assert.True(pasteInvoked);
    }
}
