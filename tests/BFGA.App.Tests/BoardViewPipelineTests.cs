using System.Reflection;
using BFGA.App.ViewModels;
using BFGA.App.Views;
using BFGA.Canvas.Rendering;
using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using System.Numerics;
using BFGA.App.Services;

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

    [Fact]
    public void DeleteSelection_RemovesSelectedElementsAndClearsSelectionOverlay()
    {
        var mainViewModel = new MainViewModel();
        var board = new BoardState();
        var first = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(10, 10), Size = new Vector2(20, 20) };
        var second = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Ellipse, Position = new Vector2(40, 40), Size = new Vector2(10, 10) };
        board.Elements.Add(first);
        board.Elements.Add(second);
        typeof(MainViewModel).GetField("_board", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mainViewModel, board);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel);
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var toolController = (BoardToolController?)typeof(BoardView)
            .GetField("_toolController", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(boardView);

        Assert.NotNull(toolController);
        toolController!.Selection.SelectMany([first.Id, second.Id]);

        boardView.DeleteSelection();

        Assert.Empty(mainViewModel.Board.Elements);
        Assert.Empty(toolController.Selection.SelectedElementIds);
        Assert.Null(toolController.Selection.ActiveElementId);
    }

    [Fact]
    public void ApplyToolResult_PublishesOperationsThroughExistingPath()
    {
        var mainViewModel = new MainViewModel();
        var board = new BoardState();
        var first = new ShapeElement { Id = Guid.NewGuid(), Type = ShapeType.Rectangle, Position = new Vector2(10, 10), Size = new Vector2(20, 20) };
        board.Elements.Add(first);
        typeof(MainViewModel).GetField("_board", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mainViewModel, board);

        var host = new FakeHostSession();
        typeof(MainViewModel).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mainViewModel, host);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel);
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var result = new ToolResult(true, true, [new DeleteElementOperation(first.Id)]);
        InvokePrivate(boardView, "ApplyToolResult", result, "delete-selection", true);

        Assert.Single(host.BroadcastedOperations);
        Assert.IsType<DeleteElementOperation>(host.BroadcastedOperations[0]);
        Assert.Empty(mainViewModel.Board.Elements);
    }

    [Fact]
    public void SyncEraserPreview_EraserTool_UpdatesViewportPreview()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Eraser
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        InvokePrivate(boardView, "SyncEraserPreview", new Vector2(42f, 24f), true);

        var viewport = GetViewport(boardView);
        Assert.Equal(new EraserPreviewState(new Vector2(42f, 24f), 10f, true), viewport.EraserPreview);
    }

    [Fact]
    public void SyncEraserPreview_ToolChange_ClearsViewportPreview()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Eraser
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "SyncEraserPreview", new Vector2(42f, 24f), true);

        boardScreenViewModel.SelectedTool = BoardToolType.Select;
        InvokePrivate(boardView, "HandleBoardScreenPropertyChanged", boardScreenViewModel, new System.ComponentModel.PropertyChangedEventArgs(nameof(BoardScreenViewModel.SelectedTool)));

        var viewport = GetViewport(boardView);
        Assert.Null(viewport.EraserPreview);
    }

    [Fact]
    public void SyncEraserPreview_Inactive_ClearsViewportPreview()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Eraser
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "SyncEraserPreview", new Vector2(42f, 24f), true);
        InvokePrivate(boardView, "SyncEraserPreview", null, false);

        var viewport = GetViewport(boardView);
        Assert.Null(viewport.EraserPreview);
    }

    [Fact]
    public async Task ImportImageFromClipboardAsync_AddsCenteredImageAndSelectsIt()
    {
        var clipboard = new FakeClipboardService
        {
            ReadResult = new ClipboardImageData(CreatePngBytes(), "clipboard.png")
        };
        var mainViewModel = new MainViewModel(clipboardService: clipboard);
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel);
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var viewport = GetViewport(boardView);
        viewport.Width = 800;
        viewport.Height = 600;
        viewport.Pan = new Vector2(400, 300);
        viewport.Zoom = 1.0;

        await boardView.ImportImageFromClipboardAsync();

        var image = Assert.Single(mainViewModel.Board.Elements.OfType<ImageElement>());
        Assert.Equal(new Vector2(-0.5f, -0.5f), image.Position);
        Assert.Equal(BoardToolType.Select, boardScreenViewModel.SelectedTool);

        var toolController = GetToolController(boardView);
        Assert.Contains(image.Id, toolController.Selection.SelectedElementIds);
        Assert.Equal(image.Id, toolController.Selection.ActiveElementId);
    }

    [Fact]
    public async Task ImportImageFromClipboardAsync_WithoutImage_ExitsWithoutChanges()
    {
        var clipboard = new FakeClipboardService();
        var mainViewModel = new MainViewModel(clipboardService: clipboard);
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel);
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        await boardView.ImportImageFromClipboardAsync();

        Assert.Empty(mainViewModel.Board.Elements);
    }

    [Fact]
    public async Task TextTool_PromptReturnsText_AddsTextElementWithStyledDefaults()
    {
        var prompt = new FakeTextPromptService { Result = "hello" };
        var mainViewModel = new MainViewModel(textPromptService: prompt);
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Text,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepSkyBlue,
            Opacity = 0.5f
        };
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        await (Task)InvokePrivate(boardView, "PlaceTextFromPromptAsync", new Vector2(25f, 30f))!;

        var text = Assert.Single(mainViewModel.Board.Elements.OfType<TextElement>());
        Assert.Equal("hello", text.Text);
        Assert.Equal(new Vector2(25f, 30f), text.Position);
        Assert.Equal(new SkiaSharp.SKColor(0, 191, 255, 128), text.Color);
        Assert.Equal(24f, text.FontSize);
        Assert.Equal("Inter", text.FontFamily);
        Assert.Equal(BoardToolType.Select, boardScreenViewModel.SelectedTool);

        var toolController = GetToolController(boardView);
        Assert.Contains(text.Id, toolController.Selection.SelectedElementIds);
        Assert.Equal(text.Id, toolController.Selection.ActiveElementId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TextTool_PromptCancelledOrBlank_DoesNothing(string? promptResult)
    {
        var prompt = new FakeTextPromptService { Result = promptResult };
        var mainViewModel = new MainViewModel(textPromptService: prompt);
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Text
        };
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        await (Task)InvokePrivate(boardView, "PlaceTextFromPromptAsync", new Vector2(25f, 30f))!;

        Assert.Empty(mainViewModel.Board.Elements.OfType<TextElement>());
        Assert.Equal(BoardToolType.Text, boardScreenViewModel.SelectedTool);
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

    private static BFGA.Canvas.BoardViewport GetViewport(BoardView boardView)
    {
        var field = typeof(BoardView).GetField("viewport", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<BFGA.Canvas.BoardViewport>(field!.GetValue(boardView));
    }

    private static BoardToolController GetToolController(BoardView boardView)
    {
        var field = typeof(BoardView).GetField("_toolController", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<BoardToolController>(field!.GetValue(boardView));
    }

    private static byte[] CreatePngBytes()
    {
        using var bitmap = new SkiaSharp.SKBitmap(1, 1);
        bitmap.SetPixel(0, 0, SkiaSharp.SKColors.Red);

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed class FakeHostSession : BFGA.App.Networking.IGameHostSession
    {
        public List<BoardOperation> BroadcastedOperations { get; } = new();
        public BoardState BoardState { get; } = new();
        public bool IsRunning => true;
        public int Port => 7777;
        public bool CanUndo => false;
        public bool CanRedo => false;

        public event EventHandler<BFGA.Network.PeerJoinedEventArgs>? PeerJoined { add { } remove { } }
        public event EventHandler<BFGA.Network.PeerLeftEventArgs>? PeerLeft { add { } remove { } }

        public void Start(int port = 7777) { }

        public void ReplaceBoardState(BoardState snapshot)
        {
            BoardState.BoardName = snapshot.BoardName;
            BoardState.BoardId = snapshot.BoardId;
            BoardState.LastModified = snapshot.LastModified;
            BoardState.Elements = snapshot.Elements.ToList();
        }

        public bool TryApplyLocalOperation(BoardOperation operation)
        {
            if (operation is DeleteElementOperation delete)
            {
                BoardState.Elements.RemoveAll(e => e.Id == delete.ElementId);
                return true;
            }

            return false;
        }

        public void SyncAllClients() { }

        public void BroadcastOperation(BoardOperation operation, bool reliable = true)
            => BroadcastedOperations.Add(operation);

        public void BroadcastFullSync() { }

        public void PollEvents() { }

        public bool TryUndo() => false;
        public bool TryRedo() => false;

        public void Dispose() { }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public ClipboardImageData? ReadResult { get; set; }

        public Task<ClipboardImageData?> ReadImageAsync() => Task.FromResult(ReadResult);

        public Task WriteImageAsync(byte[] imageData, string fileName) => Task.CompletedTask;
    }

    private sealed class FakeTextPromptService : ITextPromptService
    {
        public string? Result { get; set; }

        public Task<string?> PromptAsync(string title, string prompt, string placeholder = "")
            => Task.FromResult(Result);
    }
}
