using BFGA.App.ViewModels;
using BFGA.App.Views;
using BFGA.Canvas.Tools;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SkiaSharp;
using System.Reflection;

namespace BFGA.App.Tests;

public class PropertyPanelTests
{
    [Theory]
    [InlineData(BoardToolType.Pen, true)]
    [InlineData(BoardToolType.Rectangle, true)]
    [InlineData(BoardToolType.Ellipse, true)]
    [InlineData(BoardToolType.Select, false)]
    [InlineData(BoardToolType.Hand, false)]
    [InlineData(BoardToolType.Image, false)]
    [InlineData(BoardToolType.Eraser, false)]
    public void IsPropertyPanelVisible_MatchesToolSelection(BoardToolType toolType, bool expected)
    {
        var sut = CreateSut();

        sut.SelectedTool = toolType;

        Assert.Equal(expected, sut.IsPropertyPanelVisible);
    }

    [Fact]
    public void DefaultPropertyValues_MatchTaskFiveContract()
    {
        var sut = CreateSut();

        Assert.Equal(SKColors.White, sut.SelectedStrokeColor);
        Assert.Equal(2f, sut.StrokeWidth);
        Assert.Equal(1f, sut.Opacity);
        Assert.Equal(SKColors.Transparent, sut.SelectedFillColor);
    }

    [Fact]
    public void ShowFillSection_MatchesToolSelection()
    {
        var sut = CreateSut();

        sut.SelectedTool = BoardToolType.Pen;
        Assert.False(sut.ShowFillSection);

        sut.SelectedTool = BoardToolType.Rectangle;
        Assert.True(sut.ShowFillSection);

        sut.SelectedTool = BoardToolType.Ellipse;
        Assert.True(sut.ShowFillSection);
    }

    [Fact]
    public void TextSelection_OpensPropertyPanelEvenInSelectTool()
    {
        var sut = CreateSut();

        Assert.False(sut.IsPropertyPanelVisible);

        sut.HasSelectedTextSelection = true;

        Assert.True(sut.IsPropertyPanelVisible);
        Assert.True(sut.IsTextSelectionActive);
    }

    [Fact]
    public void Opacity_ClampsToValidRange()
    {
        var sut = CreateSut();

        sut.Opacity = 1.5f;
        Assert.Equal(1f, sut.Opacity);

        sut.Opacity = -0.25f;
        Assert.Equal(0f, sut.Opacity);
    }

    [Fact]
    public void StrokeWidth_DefaultIs2_AndRangeIsOneToTwenty()
    {
        var sut = CreateSut();

        // Default
        Assert.Equal(2f, sut.StrokeWidth);

        // Accepts min
        sut.StrokeWidth = 1f;
        Assert.Equal(1f, sut.StrokeWidth);

        // Accepts max
        sut.StrokeWidth = 20f;
        Assert.Equal(20f, sut.StrokeWidth);
    }

    [Fact]
    public void Opacity_DefaultIs1_AndRangeIsZeroToOne()
    {
        var sut = CreateSut();

        Assert.Equal(1f, sut.Opacity);

        sut.Opacity = 0f;
        Assert.Equal(0f, sut.Opacity);

        sut.Opacity = 1f;
        Assert.Equal(1f, sut.Opacity);
    }

    [Fact]
    public void SelectedStrokeColor_DefaultIsWhite_AndCanBeChanged()
    {
        var sut = CreateSut();

        Assert.Equal(SKColors.White, sut.SelectedStrokeColor);

        sut.SelectedStrokeColor = new SKColor(0xFF, 0x3B, 0x30);
        Assert.Equal(new SKColor(0xFF, 0x3B, 0x30), sut.SelectedStrokeColor);
    }

    [Fact]
    public void SelectedFillColor_DefaultIsTransparent_AndCanBeChanged()
    {
        var sut = CreateSut();

        Assert.Equal(SKColors.Transparent, sut.SelectedFillColor);

        sut.SelectedFillColor = SKColors.White;
        Assert.Equal(SKColors.White, sut.SelectedFillColor);
    }

    [Fact]
    public void IsPropertyPanelVisible_RaisesPropertyChanged_WhenToolChanges()
    {
        var sut = CreateSut();
        var notifications = new System.Collections.Generic.List<string?>();
        sut.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        sut.SelectedTool = BoardToolType.Pen;

        Assert.Contains(nameof(sut.IsPropertyPanelVisible), notifications);
    }

    [Fact]
    public void ShowFillSection_RaisesPropertyChanged_WhenToolChanges()
    {
        var sut = CreateSut();
        var notifications = new System.Collections.Generic.List<string?>();
        sut.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        sut.SelectedTool = BoardToolType.Rectangle;

        Assert.Contains(nameof(sut.ShowFillSection), notifications);
    }

    [Fact]
    public void AllPropertyPanelProperties_RaisePropertyChanged_WhenChanged()
    {
        // Regression guard: ensures BoardView's SyncToolController subscription fires
        // for all property panel VM properties, not just SelectedTool
        var sut = CreateSut();
        var notifications = new System.Collections.Generic.HashSet<string?>();
        sut.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        sut.SelectedStrokeColor = new SKColor(0xFF, 0x3B, 0x30);
        sut.SelectedFillColor = SKColors.White;
        sut.StrokeWidth = 5f;
        sut.Opacity = 0.5f;

        Assert.Contains(nameof(sut.SelectedStrokeColor), notifications);
        Assert.Contains(nameof(sut.SelectedFillColor), notifications);
        Assert.Contains(nameof(sut.StrokeWidth), notifications);
        Assert.Contains(nameof(sut.Opacity), notifications);
    }

    [Fact]
    public void SettingsPanel_ContainsLaserColorSectionAndHelperCopy()
    {
        var settingsPanelXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "SettingsPanel.axaml"));

        Assert.Contains("LASER COLOR", settingsPanelXaml);
        Assert.Contains("Used for roster, cursor, and laser", settingsPanelXaml);
        Assert.Contains("SKColorToBrushConverter", settingsPanelXaml);
    }

    [Fact]
    public void SettingsPanel_ContainsExpectedLaserColorSwatches()
    {
        var settingsPanelXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "SettingsPanel.axaml"));
        var expectedSwatches = new[]
        {
            "#000000", "#FFFFFF", "#FF3B30", "#FF9500",
            "#FFCC00", "#34C759", "#007AFF", "#AF52DE",
            "#5856D6", "#FF2D55", "#A2845E", "#8E8E93",
            "#636366", "#48484A", "#2C2C2E", "#1C1C1E"
        };

        Assert.Equal(16, CountOccurrences(settingsPanelXaml, "Classes=\"color-swatch\""));

        foreach (var swatch in expectedSwatches)
        {
            Assert.Contains($"Tag=\"{swatch}\"", settingsPanelXaml);
            Assert.Contains($"Background=\"{swatch}\"", settingsPanelXaml);
        }
    }

    [Fact]
    public void SettingsPanelSwatchClick_UpdatesLaserPresenceColor()
    {
        using var mainViewModel = new MainViewModel();
        using var boardScreenViewModel = new BoardScreenViewModel(mainViewModel);
        var panel = new SettingsPanel
        {
            DataContext = boardScreenViewModel
        };
        var button = new Button
        {
            Tag = "#34C759"
        };

        typeof(SettingsPanel)
            .GetMethod("OnLaserColorSwatchClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(panel, new object?[] { button, new RoutedEventArgs() });

        Assert.Equal(SKColor.Parse("#34C759"), boardScreenViewModel.LaserPresenceColor);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static BoardScreenViewModel CreateSut() => new(new MainViewModel());
}
