using BFGA.App.ViewModels;
using BFGA.Canvas.Tools;

namespace BFGA.App.Tests;

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
}
