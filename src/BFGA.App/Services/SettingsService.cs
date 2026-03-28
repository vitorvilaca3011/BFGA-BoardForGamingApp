using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BFGA.App.Services;

public sealed class SettingsService : IDisposable
{
    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BFGA");

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

    private CancellationTokenSource? _debounceCts;

    public float GridOpacity { get; set; } = 0.1f;
    public string Language { get; set; } = "English";
    public string DefaultImageFolder { get; set; } = string.Empty;
    public bool AutosaveEnabled { get; set; } = true;
    public int AutosaveIntervalSeconds { get; set; } = 60;

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return;
            }

            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data is null)
            {
                return;
            }

            GridOpacity = Math.Clamp(data.GridOpacity, 0f, 0.3f);
            Language = data.Language ?? "English";
            DefaultImageFolder = data.DefaultImageFolder ?? string.Empty;
            AutosaveEnabled = data.AutosaveEnabled;
            AutosaveIntervalSeconds = data.AutosaveIntervalSeconds > 0 ? data.AutosaveIntervalSeconds : 60;
        }
        catch
        {
            // Corrupt settings — use defaults
        }
    }

    public void SaveDebounced()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                SaveImmediate();
            }
            catch (OperationCanceledException)
            {
                // Debounced — new save pending
            }
        }, token);
    }

    public void SaveImmediate()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var data = new SettingsData
            {
                GridOpacity = GridOpacity,
                Language = Language,
                DefaultImageFolder = DefaultImageFolder,
                AutosaveEnabled = AutosaveEnabled,
                AutosaveIntervalSeconds = AutosaveIntervalSeconds
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Non-critical — settings just won't persist
        }
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }

    private sealed class SettingsData
    {
        public float GridOpacity { get; set; } = 0.1f;
        public string? Language { get; set; } = "English";
        public string? DefaultImageFolder { get; set; } = string.Empty;
        public bool AutosaveEnabled { get; set; } = true;
        public int AutosaveIntervalSeconds { get; set; } = 60;
    }
}
