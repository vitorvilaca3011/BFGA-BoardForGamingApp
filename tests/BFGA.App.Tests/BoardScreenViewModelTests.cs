using BFGA.App.ViewModels;
using BFGA.Canvas.Tools;
using BFGA.App.Services;
using System.IO;
using SkiaSharp;

namespace BFGA.App.Tests;

[Collection("BFGA_BOARD_DEBUG_LOG")]
public class BoardScreenViewModelTests
{
    [Fact]
    public void SelectedTool_UpdatesComputedActiveStates()
    {
        var sut = new BoardScreenViewModel(new MainViewModel());

        Assert.True(sut.IsSelectToolActive);
        Assert.False(sut.IsHandToolActive);
        Assert.False(sut.IsPenToolActive);
        Assert.False(sut.IsRectangleToolActive);
        Assert.False(sut.IsEllipseToolActive);
        Assert.False(sut.IsImageToolActive);
        Assert.False(sut.IsEraserToolActive);
        Assert.False(sut.IsArrowToolActive);
        Assert.False(sut.IsLineToolActive);
        Assert.False(sut.IsTextToolActive);
        Assert.False(sut.IsLaserPointerToolActive);

        sut.SelectedTool = BoardToolType.Ellipse;

        Assert.False(sut.IsSelectToolActive);
        Assert.False(sut.IsHandToolActive);
        Assert.False(sut.IsPenToolActive);
        Assert.False(sut.IsRectangleToolActive);
        Assert.True(sut.IsEllipseToolActive);
        Assert.False(sut.IsImageToolActive);
        Assert.False(sut.IsEraserToolActive);
        Assert.False(sut.IsArrowToolActive);
        Assert.False(sut.IsLineToolActive);
        Assert.False(sut.IsTextToolActive);
        Assert.False(sut.IsLaserPointerToolActive);
    }

    [Fact]
    public void SelectedTool_RaisesNotificationsForAllActiveFlags()
    {
        var sut = new BoardScreenViewModel(new MainViewModel());
        var changed = new List<string>();

        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        sut.SelectedTool = BoardToolType.Image;

        Assert.Contains(nameof(BoardScreenViewModel.SelectedTool), changed);
        Assert.Contains(nameof(BoardScreenViewModel.SelectedToolText), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsSelectToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsHandToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsPenToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsRectangleToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsEllipseToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsImageToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsEraserToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsArrowToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsLineToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsTextToolActive), changed);
        Assert.Contains(nameof(BoardScreenViewModel.IsLaserPointerToolActive), changed);
    }

    [Fact]
    public void SelectedTool_SuccessfulChangeLogsOnce()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), $"bfga-board-screen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(documentsRoot);

        var previous = Environment.GetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG");
        Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", "1");

        try
        {
            var sut = new MainViewModel(documentsFolderProvider: () => documentsRoot);
            var screen = new BoardScreenViewModel(sut);

            screen.SelectedTool = BoardToolType.Rectangle;

            sut.Dispose();

            var logFile = Directory.GetFiles(Path.Combine(documentsRoot, "BFGA", "logs"), "*.log").Single();
            var contents = ReadAllTextShared(logFile);

            Assert.Equal(1, contents.Split("[selected-tool]", StringSplitOptions.RemoveEmptyEntries).Length - 1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", previous);
        }
    }

    private static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void BoardScreenViewModel_LaserPresenceColor_ProxiesMainViewModel()
    {
        using var mainViewModel = new MainViewModel();
        using var sut = new BoardScreenViewModel(mainViewModel);
        var changed = new List<string>();

        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        mainViewModel.LaserPresenceColor = SKColor.Parse("#34C759");

        Assert.Equal(SKColor.Parse("#34C759"), sut.LaserPresenceColor);
        Assert.Contains(nameof(BoardScreenViewModel.LaserPresenceColor), changed);

        sut.LaserPresenceColor = SKColor.Parse("#FF9500");

        Assert.Equal(SKColor.Parse("#FF9500"), mainViewModel.LaserPresenceColor);
    }

    [Fact]
    public void BoardScreenViewModel_LaserPresenceColor_DoesNotChangeSelectedStrokeColor()
    {
        using var mainViewModel = new MainViewModel();
        using var sut = new BoardScreenViewModel(mainViewModel);
        var strokeColor = SKColor.Parse("#007AFF");

        sut.SelectedStrokeColor = strokeColor;
        sut.LaserPresenceColor = SKColor.Parse("#FF2D55");

        Assert.Equal(strokeColor, sut.SelectedStrokeColor);
        Assert.Equal(SKColor.Parse("#FF2D55"), mainViewModel.LaserPresenceColor);
    }
}
