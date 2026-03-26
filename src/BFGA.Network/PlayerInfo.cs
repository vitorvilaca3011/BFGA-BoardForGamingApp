using MessagePack;
using SkiaSharp;

namespace BFGA.Network;

/// <summary>
/// Information about a connected player.
/// </summary>
[MessagePackObject]
public class PlayerInfo
{
    /// <summary>
    /// The player's chosen display name.
    /// </summary>
    [Key(0)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The color assigned to this player for cursors and attribution.
    /// </summary>
    [Key(1)]
    public SKColor AssignedColor { get; set; }

    public PlayerInfo() { }

    public PlayerInfo(string displayName, SKColor assignedColor)
    {
        DisplayName = displayName;
        AssignedColor = assignedColor;
    }
}
