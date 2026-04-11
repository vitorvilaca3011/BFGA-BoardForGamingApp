using System.Reflection;
using Avalonia.Media;
using BFGA.App.Views;
using BFGA.Canvas;
using BFGA.Canvas.Rendering;
using SkiaSharp;

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

        Assert.Contains("x:DataType=\"vm:BoardScreenViewModel\"", xaml);
        Assert.Contains("Classes=\"whiteboard-shell\"", xaml);
        Assert.Contains("Board=\"{Binding MainViewModel.Board}\"", xaml);
        Assert.Contains("RemoteCursors=\"{Binding MainViewModel.RemoteCursors}\"", xaml);
        Assert.Contains("RemoteStrokePreviews=\"{Binding MainViewModel.RemoteStrokePreviews}\"", xaml);
        Assert.Contains("Grid", xaml);
        Assert.DoesNotContain("DockPanel", xaml);
        Assert.Contains("views:BottomBar x:Name=\"bottomBar\"", xaml);
        Assert.Contains("views:PropertyPanel", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
        Assert.Contains("VerticalAlignment=\"Bottom\"", xaml);
        Assert.Contains("Margin=\"0,0,0,12\"", xaml);
        Assert.Contains("VerticalAlignment=\"Center\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
        Assert.Contains("Border.property-panel", themeXaml);
        Assert.Contains("Button.color-swatch", themeXaml);
        Assert.Contains("Button.transparent-swatch", themeXaml);

        Assert.Contains("xmlns:vm=\"clr-namespace:BFGA.App.ViewModels\"", toolbarXaml);
        Assert.Contains("x:DataType=\"vm:BoardScreenViewModel\"", toolbarXaml);
        Assert.Contains("Classes=\"whiteboard-toolbar\"", toolbarXaml);
        Assert.Equal(4, CountOccurrences(toolbarXaml, "Background=\"{DynamicResource BorderDefault}\""));
        AssertSequence(
            toolbarXaml,
            "ToolIconSelect",
            "ToolIconHand",
            "ToolIconRectangle",
            "ToolIconEllipse",
            "ToolIconArrow",
            "ToolIconLine",
            "BorderDefault",
            "ToolIconEraser",
            "ToolIconPen",
            "BorderDefault",
            "ToolIconText",
            "ToolIconImage",
            "BorderDefault",
            "ToolIconLaser");
        Assert.Contains("Classes.active=\"{Binding IsSelectToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsHandToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsPenToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsRectangleToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsEllipseToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsArrowToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsLineToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsTextToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsImageToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsEraserToolActive}\"", toolbarXaml);
        Assert.Contains("Classes.active=\"{Binding IsLaserPointerToolActive}\"", toolbarXaml);
        Assert.Contains("views:BoardView", xaml);
        Assert.Contains("KeyDown=\"OnKeyDown\"", mainWindowXaml);
        Assert.DoesNotContain("PathIcon", toolbarXaml);
        Assert.Contains("<Path", toolbarXaml);
        Assert.DoesNotContain("PathIcon", bottomBarXaml);
        Assert.Contains("<Path", bottomBarXaml);
        Assert.DoesNotContain("PathIcon Data=\"{StaticResource IconMinimize}\"", mainWindowXaml);
        Assert.DoesNotContain("PathIcon Data=\"{StaticResource IconClose}\"", mainWindowXaml);
        Assert.Contains("Classes=\"stroke-icon\"", toolbarXaml);
        Assert.Contains("Classes=\"stroke-icon\"", bottomBarXaml);
        Assert.Contains("Classes=\"title-bar-glyph settings-glyph\"", mainWindowXaml);
        Assert.Contains("Classes=\"title-bar-glyph window-control-glyph\"", mainWindowXaml);
        Assert.Contains("Text=\"&#xE713;\"", mainWindowXaml);
        Assert.Contains("Text=\"&#xE921;\"", mainWindowXaml);
        Assert.Contains("Text=\"&#xE8BB;\"", mainWindowXaml);
        Assert.DoesNotContain("M12.22 2h-.44", mainWindowXaml);
        Assert.DoesNotContain("Width=\"14\" Height=\"14\"", mainWindowXaml);
        Assert.Contains("Path.stroke-icon", themeXaml);
        Assert.Contains("TextBlock.title-bar-glyph", themeXaml);
        Assert.Contains("TextBlock.settings-glyph", themeXaml);
        Assert.Contains("TextBlock.window-control-glyph", themeXaml);
        Assert.Contains("HorizontalAlignment\" Value=\"Center\"", themeXaml);
        Assert.Contains("VerticalAlignment\" Value=\"Center\"", themeXaml);
        Assert.Contains("Foreground\" Value=\"{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}\"", themeXaml);
        Assert.Contains("Slider", bottomBarXaml);
        Assert.Contains("ZoomInCommand", bottomBarXaml);
        Assert.Contains("ZoomOutCommand", bottomBarXaml);
        Assert.Contains("ZoomLevel", bottomBarXaml);
        var propertyPanelXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "PropertyPanel.axaml"));
        Assert.Contains("IsVisible=\"{Binding IsPropertyPanelVisible}\"", propertyPanelXaml);
        Assert.Contains("Classes=\"color-swatch\"", propertyPanelXaml);
        Assert.Contains("Classes=\"color-swatch transparent-swatch\"", propertyPanelXaml);
        Assert.Contains("Tag=\"#00000000\"", propertyPanelXaml);
        Assert.Contains("BgBase", themeXaml);
        Assert.Contains("ToolButtonSize", themeXaml);
        Assert.Contains("PanelCornerRadius", themeXaml);
    }

    [Fact]
    public void App_RegistersWhiteboardThemeAndToolIcons()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "App.axaml"));
        var csproj = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "BFGA.App.csproj"));
        var typographyXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Styles", "Typography.axaml"));

        Assert.Contains("Styles/WhiteboardTheme.axaml", xaml);
        Assert.Contains("Assets/ToolIcons.axaml", xaml);
        Assert.Contains("Styles/Colors.axaml", xaml);
        Assert.Contains("Styles/Typography.axaml", xaml);
        Assert.Contains("AvaloniaResource Include=\"Assets\\Fonts\\*.ttf\"", csproj);
        Assert.Contains("InterFont", typographyXaml);
        Assert.Contains("InterExtraLightFont", typographyXaml);
        Assert.Contains("InterLightFont", typographyXaml);
        Assert.Contains("InterMediumFont", typographyXaml);
        Assert.Contains("MonoFont", typographyXaml);
        Assert.Contains("avares://BFGA.App/Assets/Fonts#Inter", typographyXaml);
        Assert.Contains("avares://BFGA.App/Assets/Fonts#Inter ExtraLight", typographyXaml);
        Assert.Contains("avares://BFGA.App/Assets/Fonts#Inter Light", typographyXaml);
        Assert.Contains("avares://BFGA.App/Assets/Fonts#Inter Medium", typographyXaml);
        Assert.Contains("avares://BFGA.App/Assets/Fonts#JetBrains Mono", typographyXaml);
    }

    [Fact]
    public void App_RegistersTypographyResourcesAtRuntime()
    {
        var app = new BFGA.App.App();
        app.Initialize();

        AssertRuntimeFontFamily(app, "InterFont", "Inter");
        AssertRuntimeFontFamily(app, "InterExtraLightFont", "Inter ExtraLight");
        AssertRuntimeFontFamily(app, "InterLightFont", "Inter Light");
        AssertRuntimeFontFamily(app, "InterMediumFont", "Inter Medium");
        AssertRuntimeFontFamily(app, "MonoFont", "JetBrains Mono");
    }

    [Fact]
    public void App_RegistersSharedThemeBrushesWithMatchingColors()
    {
        var app = new BFGA.App.App();
        app.Initialize();

        AssertThemeBrush(app, "BgBase", ThemeColors.BgBase);
        AssertThemeBrush(app, "BgSurface", ThemeColors.BgSurface);
        AssertThemeBrush(app, "BgElevated", ThemeColors.BgElevated);
        AssertThemeBrush(app, "TextPrimary", ThemeColors.TextPrimary);
        AssertThemeBrush(app, "TextSecondary", ThemeColors.TextSecondary);
        AssertThemeBrush(app, "TextTertiary", new SKColor(0x66, 0x66, 0x66));
        AssertThemeBrush(app, "BorderDefault", ThemeColors.BorderDefault);
    }

    [Fact]
    public void ToolbarAndBottomBar_ExistAsDedicatedViews()
    {
        var toolbar = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "ToolBar.axaml"));
        var bottomBar = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "BottomBar.axaml"));

        Assert.Contains("xmlns:vm=\"clr-namespace:BFGA.App.ViewModels\"", toolbar);
        Assert.Contains("x:DataType=\"vm:BoardScreenViewModel\"", toolbar);
        Assert.Contains("whiteboard-toolbar", toolbar);
        Assert.Contains("whiteboard-bottom-bar", bottomBar);
        Assert.Contains("ToolIconSelect", toolbar);
        Assert.Contains("ToolIconHand", toolbar);
        Assert.Contains("ToolIconPen", toolbar);
        Assert.Contains("ToolIconArrow", toolbar);
        Assert.Contains("ToolIconLine", toolbar);
        Assert.Contains("ToolIconText", toolbar);
        Assert.Contains("ToolIconLaser", toolbar);
        Assert.Contains("Slider", bottomBar);
        Assert.Contains("ZoomLabel", bottomBar);
        Assert.Contains("ZoomInCommand", bottomBar);
        var toolIcons = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Assets", "ToolIcons.axaml"));
        Assert.Contains("IconSettings", toolIcons);
    }

    [Fact]
    public void WhiteboardTheme_UsesCentralizedThemeTokensAndCornerRadiusResources()
    {
        var themeXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Styles", "WhiteboardTheme.axaml"));

        Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolBarBackground\"", themeXaml);
        Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolBarBorderBrush\"", themeXaml);
        Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolButtonBackground\"", themeXaml);
        Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolButtonForeground\"", themeXaml);
        Assert.Contains("Button.whiteboard-tool-button", themeXaml);
        Assert.Contains("Button.whiteboard-tool-button:pointerover", themeXaml);
        Assert.Contains("Button.whiteboard-tool-button.active", themeXaml);
        Assert.Contains("Background\" Value=\"Transparent\"", themeXaml);
        Assert.Contains("Foreground\" Value=\"{DynamicResource TextSecondary}\"", themeXaml);
        Assert.Contains("Background\" Value=\"{DynamicResource BgOverlay}\"", themeXaml);
        Assert.Contains("Foreground\" Value=\"{DynamicResource TextPrimary}\"", themeXaml);
        Assert.Contains("Background\" Value=\"{DynamicResource BorderSubtle}\"", themeXaml);
        Assert.Contains("BorderBrush\" Value=\"{DynamicResource AccentWhite}\"", themeXaml);
        Assert.Contains("BorderThickness\" Value=\"2,0,0,0\"", themeXaml);
        Assert.Contains("DynamicResource BgBase", themeXaml);
        Assert.Contains("DynamicResource BgSurface", themeXaml);
        Assert.Contains("DynamicResource TextPrimary", themeXaml);
        Assert.Contains("DynamicResource BorderDefault", themeXaml);
        Assert.Contains("<CornerRadius x:Key=\"PanelCornerRadius\"", themeXaml);
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

        var viewport = (BoardViewport)viewportField!.GetValue(boardView)!;
        Assert.NotNull(viewport);

        viewport.SetZoom(2.5, 0, 0);

        Assert.InRange(boardView.ZoomLevel, 2.4, 2.6);
        Assert.Contains("250%", boardView.ZoomLabel);
    }

    [Fact]
    public void BoardViewport_ConstrainsRealZoomRange()
    {
        Assert.Equal(0.2, BoardViewport.MinZoom, 0.001);
        Assert.Equal(3.0, BoardViewport.MaxZoom, 0.001);
    }

    private static void AssertThemeBrush(BFGA.App.App app, string key, SKColor expectedColor)
    {
        var found = app.TryGetResource(key, null, out var resource);

        Assert.True(found, $"Expected application resource '{key}' to exist.");
        var brush = Assert.IsType<SolidColorBrush>(resource);
        Assert.Equal(expectedColor.Red, brush.Color.R);
        Assert.Equal(expectedColor.Green, brush.Color.G);
        Assert.Equal(expectedColor.Blue, brush.Color.B);
        Assert.Equal(expectedColor.Alpha, brush.Color.A);
    }

    private static void AssertRuntimeFontFamily(BFGA.App.App app, string key, string expected)
    {
        var found = app.TryGetResource(key, null, out var resource);

        Assert.True(found, $"Expected application resource '{key}' to exist.");
        var fontFamily = Assert.IsType<FontFamily>(resource);
        Assert.Equal(expected, fontFamily.Name);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void AssertSequence(string text, params string[] parts)
    {
        var lastIndex = -1;

        foreach (var part in parts)
        {
            var index = text.IndexOf(part, lastIndex + 1, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Expected to find '{part}' after index {lastIndex}.");
            Assert.True(index > lastIndex, $"Expected '{part}' to appear after previous marker.");
            lastIndex = index;
        }
    }
}
