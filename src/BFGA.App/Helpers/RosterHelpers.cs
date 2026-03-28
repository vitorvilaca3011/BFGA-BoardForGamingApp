namespace BFGA.App.Helpers;

public static class RosterHelpers
{
    public static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "??";
        }

        var trimmed = displayName.Trim();
        var length = Math.Min(2, trimmed.Length);
        return trimmed[..length].ToUpperInvariant();
    }
}
