using System.Reflection;
using BFGA.App.ViewModels;
using BFGA.App.Views;
using BFGA.Canvas.Tools;
using System.Numerics;

namespace BFGA.App.Tests;

[Collection("BFGA_BOARD_DEBUG_LOG")]
public sealed class BoardViewPipelineTests
{
    [Fact]
    public void DisabledLogging_DoesNotInvokeMessageFactoryOnRuntimePath()
    {
        var sut = new MainViewModel();
        var boardView = new BoardView();
        AttachBoardScreen(boardView, new BoardScreenViewModel(sut));

        var invoked = false;

        boardView.GetType().GetMethod("LogPointerEvent", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string), typeof(Func<string>)])!
            .Invoke(boardView, ["pointer-pressed", new Func<string>(() =>
            {
                invoked = true;
                return "ignored";
            })]);

        Assert.False(invoked);
    }

    [Fact]
    public void MoveLogThrottling_ResetsPerGesture()
    {
        var boardView = new BoardView();

        InvokePrivate(boardView, "StartPointerGesture");
        Assert.True((bool)InvokePrivate(boardView, "ShouldLogMoveEvent")!);
        Assert.False((bool)InvokePrivate(boardView, "ShouldLogMoveEvent")!);

        InvokePrivate(boardView, "StartPointerGesture");
        Assert.True((bool)InvokePrivate(boardView, "ShouldLogMoveEvent")!);
        Assert.False((bool)InvokePrivate(boardView, "ShouldLogMoveEvent")!);
    }

    [Fact]
    public void PenDrag_ShouldKeepBoardChangedTrueAfterBoardViewSync()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Pen
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var toolController = (BoardToolController?)typeof(BoardView)
            .GetField("_toolController", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(boardView);

        Assert.NotNull(toolController);

        toolController!.HandlePointerDown(new Vector2(10f, 10f));

        InvokePrivateNoArgs(boardView, "SyncToolController");

        var moveResult = toolController.HandlePointerMove(new Vector2(16f, 10f));

        Assert.True(moveResult.BoardChanged);
    }

    private static void AttachBoardScreen(BoardView boardView, BoardScreenViewModel boardScreenViewModel)
    {
        var field = typeof(BoardView).GetField("_boardScreenViewModel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(boardView, boardScreenViewModel);
    }

    private static object? InvokePrivate(object instance, string methodName, params object[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method!.Invoke(instance, arguments);
    }

    private static object? InvokePrivateNoArgs(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);
        Assert.NotNull(method);
        return method!.Invoke(instance, []);
    }
}
