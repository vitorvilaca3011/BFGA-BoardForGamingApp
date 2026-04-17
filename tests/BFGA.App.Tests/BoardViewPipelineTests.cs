using System.Reflection;
using Avalonia;
using Avalonia.Input;
using BFGA.App.Services;
using BFGA.App.ViewModels;
using BFGA.App.Views;
using BFGA.Canvas.Rendering;
using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
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
    public void TextTool_PlaceText_CreatesElementWithStyledDefaults()
    {
        var mainViewModel = new MainViewModel();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Text,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepSkyBlue,
            Opacity = 0.5f,
            FontSize = 32f,
            FontFamily = "Arial"
        };
        var boardView = new BoardView();
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var toolController = GetToolController(boardView);
        var color = new SkiaSharp.SKColor(0, 191, 255, 128);
        var textElement = toolController.PlaceText("hello", new Vector2(25f, 30f), color, 32f, "Arial");

        var text = Assert.Single(mainViewModel.Board.Elements.OfType<TextElement>());
        Assert.Equal("hello", text.Text);
        Assert.Equal(new Vector2(25f, 30f), text.Position);
        Assert.Equal(color, text.Color);
        Assert.Equal(32f, text.FontSize);
        Assert.Equal("Arial", text.FontFamily);
    }

    [Fact]
    public void TextTool_PlaceText_DoesNotAddOnEmptyString()
    {
        var mainViewModel = new MainViewModel();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Text
        };
        var boardView = new BoardView();
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        Assert.Empty(mainViewModel.Board.Elements.OfType<TextElement>());
    }

    [Fact]
    public void StartInlineTextEditing_PositionsEditorAtClickPoint()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            FontSize = 24f
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var viewport = GetViewport(boardView);
        viewport.Width = 800;
        viewport.Height = 600;
        viewport.Zoom = 1.0;

        InvokePrivate(boardView, "StartInlineTextEditing", new Vector2(50f, 60f));

        var editor = GetInlineTextEditor(boardView);
        Assert.Equal(50d, Avalonia.Controls.Canvas.GetLeft(editor));
        Assert.Equal(60d, Avalonia.Controls.Canvas.GetTop(editor));
        Assert.True(editor.IsVisible);
    }

    [Fact]
    public void TryStartInlineTextEditExistingText_SelectsTextElementAndEnablesEditing()
    {
        var mainViewModel = new MainViewModel();
        var board = new BoardState();
        var text = new TextElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(40, 50),
            Text = "hello",
            FontSize = 24f,
            FontFamily = "Inter",
            Color = SkiaSharp.SKColors.White
        };
        board.Elements.Add(text);
        typeof(MainViewModel).GetField("_board", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mainViewModel, board);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel);
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var handled = (bool)(InvokePrivate(boardView, "TryStartInlineTextEditExistingText", new Vector2(42f, 52f)) ?? false);

        Assert.True(handled);
        Assert.True(boardScreenViewModel.IsEditingText);
        Assert.Equal(text.Id, boardScreenViewModel.EditingTextElementId);

        var toolController = GetToolController(boardView);
        Assert.Equal(text.Id, toolController.Selection.ActiveElementId);
        Assert.Contains(text.Id, toolController.Selection.SelectedElementIds);
    }

    [Fact]
    public void BoardToolController_SelectTool_AllowsTextMoveAfterSelection()
    {
        var board = new BoardState();
        var text = new TextElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Text = "move me",
            FontSize = 24f,
            FontFamily = "Inter",
            Color = SkiaSharp.SKColors.White
        };
        board.Elements.Add(text);

        var controller = new BoardToolController(board);
        controller.SetTool(BoardToolType.Select);
        controller.Selection.Select(text.Id);

        var bounds = BFGA.Canvas.Rendering.ElementBoundsHelper.GetBounds(text);
        var result = controller.HandlePointerDown(new Vector2(bounds.Left + 1, bounds.Top + 1));
        Assert.True(result.Handled);

        var moveResult = controller.HandlePointerMove(new Vector2(bounds.Left + 11, bounds.Top + 15));
        Assert.True(moveResult.BoardChanged);

        controller.HandlePointerUp(new Vector2(bounds.Left + 11, bounds.Top + 15));
        Assert.Equal(new Vector2(20, 24), text.Position);
    }

    [Fact]
    public void SyncSelectionOverlay_TextSelection_ExposesPropertyPanelState()
    {
        var mainViewModel = new MainViewModel();
        var board = new BoardState();
        var text = new TextElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(10, 10),
            Text = "panel",
            FontSize = 24f,
            FontFamily = "Inter",
            Color = SkiaSharp.SKColors.White
        };
        board.Elements.Add(text);
        typeof(MainViewModel).GetField("_board", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mainViewModel, board);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.Select
        };
        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var toolController = GetToolController(boardView);
        toolController.Selection.Select(text.Id);

        InvokePrivateNoArgs(boardView, "SyncSelectionOverlay");

        Assert.True(boardScreenViewModel.HasSelectedTextSelection);
        Assert.True(boardScreenViewModel.IsPropertyPanelVisible);
    }

    [Fact]
    public void LaserPointer_BeginLocalLaser_SetsActiveOverlayWithoutBoardMutation()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        var initialCount = mainViewModel.Board.Elements.Count;

        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(12f, 34f), new Point(20, 40), 100L);

        Assert.NotNull(boardView.LocalLaser);
        Assert.True(boardView.LocalLaser!.IsActive);
        Assert.Equal(new Vector2(12f, 34f), boardView.LocalLaser.HeadPosition);
        Assert.Equal(100L, boardView.LocalLaser.LastUpdateMs);
        Assert.Null(boardView.LocalPing);
        Assert.Equal(initialCount, mainViewModel.Board.Elements.Count);
    }

    [Fact]
    public void LaserPointer_BeginLocalLaser_PublishesActiveOperation()
    {
        var mainViewModel = new MainViewModel();
        var client = new FakeClientSession();
        AttachClient(mainViewModel, client);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(12f, 34f), new Point(20, 40), 100L);

        var operation = Assert.Single(client.SentOperations);
        var laser = Assert.IsType<LaserPointerOperation>(operation);
        Assert.Equal(Guid.Empty, laser.SenderId);
        Assert.Equal(new Vector2(12f, 34f), laser.Position);
        Assert.True(laser.IsActive);
    }

    [Fact]
    public void LaserPointer_BeginLocalLaser_UsesLaserPresenceColorInsteadOfSelectedStrokeColor()
    {
        var mainViewModel = new MainViewModel
        {
            LaserPresenceColor = SkiaSharp.SKColors.Lime
        };
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");

        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(12f, 34f), new Point(20, 40), 100L);

        Assert.NotNull(boardView.LocalLaser);
        Assert.Equal(mainViewModel.LaserPresenceColor, boardView.LocalLaser!.Color);
        Assert.NotEqual(boardScreenViewModel.SelectedStrokeColor, boardView.LocalLaser.Color);
    }

    [Fact]
    public void LaserPointer_UpdateLocalLaser_ChangedPointPublishesActiveOperation()
    {
        var mainViewModel = new MainViewModel();
        var client = new FakeClientSession();
        AttachClient(mainViewModel, client);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(10f, 10f), new Point(10, 10), 100L);

        InvokePrivate(boardView, "UpdateLocalLaser", new Vector2(10f, 10f), 120L);
        InvokePrivate(boardView, "UpdateLocalLaser", new Vector2(16f, 22f), 150L);

        Assert.Equal(2, client.SentOperations.Count);

        var update = Assert.IsType<LaserPointerOperation>(client.SentOperations[1]);
        Assert.Equal(new Vector2(16f, 22f), update.Position);
        Assert.True(update.IsActive);
    }

    [Fact]
    public void LaserPointer_CompleteLocalLaser_PublishesInactiveOperation()
    {
        var mainViewModel = new MainViewModel();
        var client = new FakeClientSession();
        AttachClient(mainViewModel, client);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(10f, 10f), new Point(10, 10), 100L);

        InvokePrivate(boardView, "CompleteLocalLaser", new Vector2(12f, 13f), new Point(12, 13), 250L);

        Assert.Equal(2, client.SentOperations.Count);

        var complete = Assert.IsType<LaserPointerOperation>(client.SentOperations[1]);
        Assert.Equal(new Vector2(12f, 13f), complete.Position);
        Assert.False(complete.IsActive);
    }

    [Fact]
    public void LaserPointer_CancelLocalLaser_PublishesInactiveOperation()
    {
        var mainViewModel = new MainViewModel();
        var client = new FakeClientSession();
        AttachClient(mainViewModel, client);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(4f, 6f), new Point(4, 6), 100L);
        InvokePrivate(boardView, "UpdateLocalLaser", new Vector2(8f, 9f), 120L);

        InvokePrivateNoArgs(boardView, "CancelLocalLaser");

        Assert.Equal(3, client.SentOperations.Count);

        var cancel = Assert.IsType<LaserPointerOperation>(client.SentOperations[2]);
        Assert.Equal(new Vector2(8f, 9f), cancel.Position);
        Assert.False(cancel.IsActive);
    }

    [Fact]
    public void LaserPointer_ToolSwitchAway_PublishesInactiveOperationAndPreservesTrail()
    {
        var mainViewModel = new MainViewModel();
        var client = new FakeClientSession();
        AttachClient(mainViewModel, client);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(4f, 6f), new Point(4, 6), 100L);
        InvokePrivate(boardView, "UpdateLocalLaser", new Vector2(8f, 9f), 120L);

        var trailCount = boardView.LocalLaser!.Trail.Count;

        boardScreenViewModel.SelectedTool = BoardToolType.Select;
        InvokePrivate(boardView, "HandleBoardScreenPropertyChanged", boardScreenViewModel, new System.ComponentModel.PropertyChangedEventArgs(nameof(BoardScreenViewModel.SelectedTool)));

        Assert.NotNull(boardView.LocalLaser);
        Assert.False(boardView.LocalLaser!.IsActive);
        Assert.Equal(trailCount, boardView.LocalLaser.Trail.Count);
        Assert.Equal(3, client.SentOperations.Count);
        var cancel = Assert.IsType<LaserPointerOperation>(client.SentOperations[^1]);
        Assert.Equal(new Vector2(8f, 9f), cancel.Position);
        Assert.False(cancel.IsActive);
    }

    [Fact]
    public void LaserPointer_QuickTap_KeepsLocalPingAndPublishesLaserLifecycleOnly()
    {
        var mainViewModel = new MainViewModel();
        var client = new FakeClientSession();
        AttachClient(mainViewModel, client);

        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        var initialCount = mainViewModel.Board.Elements.Count;

        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(10f, 10f), new Point(10, 10), 100L);
        InvokePrivate(boardView, "CompleteLocalLaser", new Vector2(12f, 13f), new Point(12, 13), 250L);

        Assert.NotNull(boardView.LocalPing);
        Assert.Equal(initialCount, mainViewModel.Board.Elements.Count);
        Assert.Equal(2, client.SentOperations.Count);
        Assert.All(client.SentOperations, operation => Assert.IsType<LaserPointerOperation>(operation));
        Assert.True(((LaserPointerOperation)client.SentOperations[0]).IsActive);
        Assert.False(((LaserPointerOperation)client.SentOperations[1]).IsActive);
    }

    [Fact]
    public void LaserPointer_QuickTapPing_UsesLaserPresenceColor()
    {
        var mainViewModel = new MainViewModel
        {
            LaserPresenceColor = SkiaSharp.SKColors.Cyan
        };
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(10f, 10f), new Point(10, 10), 100L);

        InvokePrivate(boardView, "CompleteLocalLaser", new Vector2(12f, 13f), new Point(12, 13), 250L);

        Assert.NotNull(boardView.LocalPing);
        Assert.Equal(mainViewModel.LaserPresenceColor, boardView.LocalPing!.Color);
        Assert.NotEqual(boardScreenViewModel.SelectedStrokeColor, boardView.LocalPing.Color);
    }

    [Fact]
    public void LaserPointer_UpdateLocalLaser_AppendsTrailAndHeadPosition()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(10f, 10f), new Point(10, 10), 100L);

        InvokePrivate(boardView, "UpdateLocalLaser", new Vector2(16f, 22f), 150L);

        Assert.NotNull(boardView.LocalLaser);
        Assert.Equal(new Vector2(16f, 22f), boardView.LocalLaser!.HeadPosition);
        Assert.Equal(150L, boardView.LocalLaser.LastUpdateMs);
        var trailPoints = boardView.LocalLaser.Trail.GetPoints().ToArray();
        Assert.True(trailPoints.Length >= 2);
        Assert.Equal(new Vector2(10f, 10f), trailPoints[0].Position);
        Assert.Equal(new Vector2(16f, 22f), trailPoints[^1].Position);
    }

    [Fact]
    public void LaserPointer_CompleteLocalLaser_QuickTapCreatesPing()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(10f, 10f), new Point(10, 10), 100L);

        InvokePrivate(boardView, "CompleteLocalLaser", new Vector2(12f, 13f), new Point(12, 13), 250L);

        Assert.NotNull(boardView.LocalLaser);
        Assert.False(boardView.LocalLaser!.IsActive);
        Assert.Equal(250L, boardView.LocalLaser.LastUpdateMs);
        Assert.NotNull(boardView.LocalPing);
        Assert.Equal(new Vector2(12f, 13f), boardView.LocalPing!.Position);
        Assert.Equal(250L, boardView.LocalPing.StartedAtMs);
    }

    [Fact]
    public void LaserPointer_CancelLocalLaser_MarksOverlayInactive()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(4f, 6f), new Point(4, 6), 100L);
        InvokePrivate(boardView, "UpdateLocalLaser", new Vector2(8f, 9f), 120L);

        var trailCount = boardView.LocalLaser!.Trail.Count;

        InvokePrivateNoArgs(boardView, "CancelLocalLaser");

        Assert.NotNull(boardView.LocalLaser);
        Assert.False(boardView.LocalLaser!.IsActive);
        Assert.Equal(trailCount, boardView.LocalLaser.Trail.Count);
        Assert.Null(boardView.LocalPing);
    }

    [Fact]
    public void LaserPointer_BeginLocalLaser_CreatesFreshOverlayPerGesture()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(2f, 3f), new Point(2, 3), 100L);

        var firstGestureLaser = boardView.LocalLaser;
        Assert.NotNull(firstGestureLaser);

        InvokePrivate(boardView, "CompleteLocalLaser", new Vector2(4f, 5f), new Point(4, 5), 300L);
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(8f, 13f), new Point(8, 13), 400L);

        Assert.NotNull(boardView.LocalLaser);
        Assert.NotSame(firstGestureLaser, boardView.LocalLaser);
        Assert.True(boardView.LocalLaser!.IsActive);
    }

    [Fact]
    public void LaserPointer_CompleteAfterCancel_DoesNotCreatePing()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer,
            SelectedStrokeColor = SkiaSharp.SKColors.DeepPink
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(10f, 10f), new Point(10, 10), 100L);
        InvokePrivateNoArgs(boardView, "CancelLocalLaser");
        InvokePrivate(boardView, "CompleteLocalLaser", new Vector2(12f, 12f), new Point(12, 12), 150L);

        Assert.NotNull(boardView.LocalLaser);
        Assert.False(boardView.LocalLaser!.IsActive);
        Assert.Null(boardView.LocalPing);
    }

    [Fact]
    public void LaserPointer_ToolSwitchAway_CancelsActiveEmission()
    {
        var mainViewModel = new MainViewModel();
        var boardView = new BoardView();
        var boardScreenViewModel = new BoardScreenViewModel(mainViewModel)
        {
            SelectedTool = BoardToolType.LaserPointer
        };

        AttachBoardScreen(boardView, boardScreenViewModel);
        InvokePrivateNoArgs(boardView, "SyncToolController");
        InvokePrivate(boardView, "BeginLocalLaser", new Vector2(18f, 24f), new Point(18, 24), 100L);

        boardScreenViewModel.SelectedTool = BoardToolType.Select;
        InvokePrivate(boardView, "HandleBoardScreenPropertyChanged", boardScreenViewModel, new System.ComponentModel.PropertyChangedEventArgs(nameof(BoardScreenViewModel.SelectedTool)));

        Assert.NotNull(boardView.LocalLaser);
        Assert.False(boardView.LocalLaser!.IsActive);
    }

    [Fact]
    public void BoardCursorFactory_LaserPointer_UsesCrossCursor()
    {
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Infrastructure", "BoardCursorFactory.cs");
        var source = File.ReadAllText(Path.GetFullPath(sourcePath));

        Assert.Contains("BoardToolType.LaserPointer => new Cursor(StandardCursorType.Cross)", source);
    }

    private static void AttachBoardScreen(BoardView boardView, BoardScreenViewModel boardScreenViewModel)
    {
        var field = typeof(BoardView).GetField("_boardScreenViewModel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(boardView, boardScreenViewModel);
    }

    private static void AttachClient(MainViewModel mainViewModel, FakeClientSession client)
    {
        var field = typeof(MainViewModel).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(mainViewModel, client);
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

    private static Avalonia.Controls.TextBox GetInlineTextEditor(BoardView boardView)
    {
        var field = typeof(BoardView).GetField("InlineTextEditor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<Avalonia.Controls.TextBox>(field!.GetValue(boardView));
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
        public event EventHandler<BFGA.Network.OperationReceivedEventArgs>? OperationReceived { add { } remove { } }

        public void Start(int port = 7777) { }

        public void ReplaceBoardState(BoardState snapshot)
        {
            BoardState.BoardName = snapshot.BoardName;
            BoardState.BoardId = snapshot.BoardId;
            BoardState.LastModified = snapshot.LastModified;
            BoardState.Elements = snapshot.Elements.ToList();
        }

        public void SetHostPresence(string displayName, SkiaSharp.SKColor assignedColor) { }

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

    private sealed class FakeClientSession : BFGA.App.Networking.IGameClientSession
    {
        public List<BoardOperation> SentOperations { get; } = new();
        public string DisplayName => "Client";
        public bool IsConnected => true;

        public event EventHandler? Connected { add { } remove { } }
        public event EventHandler? Disconnected { add { } remove { } }
        public event EventHandler<BFGA.Network.ClientOperationReceivedEventArgs>? OperationReceived { add { } remove { } }

        public void ConnectAsync(string hostAddress, int port = 7777)
        {
        }

        public void RequestFullSync()
        {
        }

        public void SendOperation(BoardOperation operation, bool reliable = true)
            => SentOperations.Add(operation);

        public void PollEvents()
        {
        }

        public void Dispose()
        {
        }
    }
}
