using System.Reflection;
using BFGA.App.Views;
using BFGA.Canvas;

namespace BFGA.App.Tests;

public class BoardScreenLayoutTests
{
    [Fact]
    public void BoardScreen_UsesWhiteboardShellLayoutAndShortcuts()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "BoardScreen.axaml"));
        var mainWindowXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "MainWindow.axaml"));
        var toolbarXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "ToolBar.axaml"));
        var bottomBarXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "BottomBar.axaml"));
        var themeXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Styles", "WhiteboardTheme.axaml"));

        Assert.Contains("DockPanel", xaml);
        Assert.Contains("DockPanel.Dock=\"Left\"", xaml);
        Assert.Contains("DockPanel.Dock=\"Bottom\"", xaml);
        Assert.Contains("views:BoardView", xaml);
        Assert.Contains("Classes=\"whiteboard-shell\"", xaml);
        Assert.Contains("KeyDown=\"OnKeyDown\"", mainWindowXaml);
        Assert.Contains("PathIcon", toolbarXaml);
        Assert.Contains("Slider", bottomBarXaml);
        Assert.Contains("ZoomInCommand", bottomBarXaml);
        Assert.Contains("ZoomOutCommand", bottomBarXaml);
        Assert.Contains("ZoomLevel", bottomBarXaml);
        Assert.Contains("ToolBarBackground", themeXaml);
        Assert.Contains("ToolButtonSize", themeXaml);
        Assert.Contains("PanelCornerRadius", themeXaml);
    }

    [Fact]
    public void App_RegistersWhiteboardThemeAndToolIcons()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "App.axaml"));

        Assert.Contains("Styles/WhiteboardTheme.axaml", xaml);
        Assert.Contains("Assets/ToolIcons.axaml", xaml);
    }

    [Fact]
    public void ToolbarAndBottomBar_ExistAsDedicatedViews()
    {
        var toolbar = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "ToolBar.axaml"));
        var bottomBar = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "BottomBar.axaml"));

        Assert.Contains("whiteboard-toolbar", toolbar);
        Assert.Contains("whiteboard-bottom-bar", bottomBar);
        Assert.Contains("ToolIconSelect", toolbar);
        Assert.Contains("ToolIconHand", toolbar);
        Assert.Contains("ToolIconPen", toolbar);
        Assert.Contains("Slider", bottomBar);
        Assert.Contains("ZoomLabel", bottomBar);
        Assert.Contains("ZoomInCommand", bottomBar);
    }

    [Fact]
    public void WhiteboardTheme_UsesTypedBrushAndCornerRadiusResources()
    {
        var themeXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Styles", "WhiteboardTheme.axaml"));

        Assert.Contains("<SolidColorBrush x:Key=\"ToolBarBackground\"", themeXaml);
        Assert.Contains("<SolidColorBrush x:Key=\"ToolBarBorderBrush\"", themeXaml);
        Assert.Contains("<SolidColorBrush x:Key=\"ToolButtonBackground\"", themeXaml);
        Assert.Contains("<SolidColorBrush x:Key=\"ToolButtonForeground\"", themeXaml);
        Assert.Contains("<CornerRadius x:Key=\"PanelCornerRadius\"", themeXaml);
        Assert.DoesNotContain("<x:String x:Key=\"ToolBarBackground\"", themeXaml);
        Assert.DoesNotContain("<x:String x:Key=\"ToolBarBorderBrush\"", themeXaml);
        Assert.DoesNotContain("<x:String x:Key=\"ToolButtonBackground\"", themeXaml);
        Assert.DoesNotContain("<x:String x:Key=\"ToolButtonForeground\"", themeXaml);
        Assert.DoesNotContain("<x:String x:Key=\"PanelCornerRadius\"", themeXaml);
    }

    [Fact]
    public void BoardView_ExposesRuntimeZoomContract()
    {
        var boardView = new BoardView();

        Assert.NotNull(boardView.ZoomInCommand);
        Assert.NotNull(boardView.ZoomOutCommand);
        Assert.NotNull(boardView.ZoomResetCommand);
        Assert.Contains("100%", boardView.ZoomLabel);

        boardView.ZoomResetCommand.Execute(null);
        Assert.Contains("100%", boardView.ZoomLabel);
    }

    [Fact]
    public void BoardView_SyncsZoomLevelFromViewportChangesAndClampsIt()
    {
        var boardView = new BoardView();

        boardView.SetZoomLevel(10);
        Assert.InRange(boardView.ZoomLevel, 0.2, 3.0);

        var viewportField = typeof(BoardView).GetField("viewport", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(viewportField);

        var viewport = viewportField!.GetValue(boardView);
        Assert.NotNull(viewport);

        var zoomBorderProperty = viewport!.GetType().GetProperty("ZoomBorder");
        Assert.NotNull(zoomBorderProperty);

        var zoomBorder = zoomBorderProperty!.GetValue(viewport);
        Assert.NotNull(zoomBorder);

        zoomBorder!.GetType().GetMethod("Zoom", [typeof(double), typeof(double), typeof(double), typeof(bool)])!
            .Invoke(zoomBorder, [2.5d, 0d, 0d, true]);

        Assert.InRange(boardView.ZoomLevel, 2.4, 2.6);
        Assert.Contains("250%", boardView.ZoomLabel);
    }

    [Fact]
    public void BoardViewport_ConstrainsRealZoomRange()
    {
        var viewport = new BoardViewport();

        Assert.Equal(0.2, viewport.ZoomBorder.MinZoomX);
        Assert.Equal(3.0, viewport.ZoomBorder.MaxZoomX);
        Assert.Equal(0.2, viewport.ZoomBorder.MinZoomY);
        Assert.Equal(3.0, viewport.ZoomBorder.MaxZoomY);
    }
}
