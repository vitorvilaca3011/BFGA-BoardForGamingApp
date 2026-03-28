using Avalonia.Media;
using BFGA.App.Converters;
using BFGA.App.Helpers;
using BFGA.App.ViewModels;
using BFGA.Network;
using BFGA.Network.Protocol;
using SkiaSharp;
using System.Reflection;
using Xunit;

namespace BFGA.App.Tests;

public class RosterOverlayTests
{
    [Fact]
    public void RosterOverlay_IsHidden_WhenRosterIsEmpty()
    {
        var mainViewModel = new MainViewModel();
        var sut = new BoardScreenViewModel(mainViewModel);

        Assert.False(sut.IsRosterVisible);
    }

    [Fact]
    public void RosterOverlay_IsVisible_WhenRosterHasPlayers()
    {
        var mainViewModel = new MainViewModel();
        var sut = new BoardScreenViewModel(mainViewModel);
        var changed = new List<string>();

        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        SetRoster(mainViewModel, new Dictionary<Guid, PlayerInfo>
        {
            { Guid.NewGuid(), new PlayerInfo("Jane Doe", SKColors.CornflowerBlue) }
        });

        Assert.Contains(nameof(BoardScreenViewModel.IsRosterVisible), changed);
        Assert.True(sut.IsRosterVisible);
    }

    [Fact]
    public void SKColorToBrushConverter_ConvertsCorrectly()
    {
        var sut = new SKColorToBrushConverter();
        var brush = Assert.IsType<SolidColorBrush>(sut.Convert(new SKColor(0x22, 0x33, 0x44, 0x11), typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(0x11, brush.Color.A);
        Assert.Equal(0x22, brush.Color.R);
        Assert.Equal(0x33, brush.Color.G);
        Assert.Equal(0x44, brush.Color.B);
    }

    [Theory]
    [InlineData("Jane", "JA")]
    [InlineData(" j", "J")]
    [InlineData("", "??")]
    [InlineData("  ", "??")]
    public void InitialsExtraction_TakesFirstTwoChars_Uppercase(string displayName, string expected)
    {
        Assert.Equal(expected, RosterHelpers.GetInitials(displayName));
    }

    [Fact]
    public void PlayerColors_MatchSpecPalette()
    {
        // Verify the 8-preset palette from the spec is used (not the old named colors)
        // Spec: #FF6B6B  #4ECDC4  #45B7D1  #96CEB4  #FFEAA7  #DDA0DD  #98D8C8  #F7DC6F
        var expectedPalette = new[]
        {
            new SKColor(0xFF, 0x6B, 0x6B),
            new SKColor(0x4E, 0xCD, 0xC4),
            new SKColor(0x45, 0xB7, 0xD1),
            new SKColor(0x96, 0xCE, 0xB4),
            new SKColor(0xFF, 0xEA, 0xA7),
            new SKColor(0xDD, 0xA0, 0xDD),
            new SKColor(0x98, 0xD8, 0xC8),
            new SKColor(0xF7, 0xDC, 0x6F),
        };

        // Access the private static field via reflection
        var field = typeof(BFGA.Network.GameHost)
            .GetField("PlayerColors", BindingFlags.NonPublic | BindingFlags.Static)!;
        var actual = (SKColor[])field.GetValue(null)!;

        Assert.Equal(expectedPalette.Length, actual.Length);
        for (int i = 0; i < expectedPalette.Length; i++)
        {
            Assert.Equal(expectedPalette[i], actual[i]);
        }
    }

    [Fact]
    public void BoardScreenViewModel_Dispose_UnsubscribesFromMainViewModel()
    {
        var mainViewModel = new MainViewModel();
        var sut = new BoardScreenViewModel(mainViewModel);
        var changed = new List<string>();

        sut.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        // Dispose — should detach the handler
        sut.Dispose();

        // Changing roster after dispose should NOT fire IsRosterVisible on sut
        SetRoster(mainViewModel, new Dictionary<Guid, PlayerInfo>
        {
            { Guid.NewGuid(), new PlayerInfo("Ghost", SKColors.Black) }
        });

        Assert.DoesNotContain(nameof(BoardScreenViewModel.IsRosterVisible), changed);
    }

    private static void SetRoster(MainViewModel mainViewModel, IReadOnlyDictionary<Guid, PlayerInfo> roster)
    {
        var setter = typeof(MainViewModel)
            .GetProperty(nameof(MainViewModel.Roster), BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(nonPublic: true)!;

        setter.Invoke(mainViewModel, [roster]);
    }
}
