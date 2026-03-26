using BFGA.Core.Models;
using MessagePack;

namespace BFGA.Core;

public static class BoardFileStore
{
    public static async Task SaveAsync(BoardState board, string filePath, CancellationToken cancellationToken = default)
    {
        if (board == null)
            throw new ArgumentNullException(nameof(board));
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
            directory = Directory.GetCurrentDirectory();
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $"{Path.GetRandomFileName()}.tmp");
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await MessagePackSerializer.SerializeAsync(stream, board, MessagePackSetup.Options, cancellationToken);
            }

            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, destinationBackupFileName: null);
            else
                File.Move(tempPath, filePath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public static async Task<BoardState> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Board file not found at '{filePath}'.", filePath);

        try
        {
            await using var stream = File.OpenRead(filePath);
            var board = await MessagePackSerializer.DeserializeAsync<BoardState>(stream, MessagePackSetup.Options, cancellationToken);
            return board ?? throw new InvalidOperationException("Loaded board state is null.");
        }
        catch (Exception ex) when (ex is not FileNotFoundException and not ArgumentException and not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to load board from '{filePath}': {ex.Message}", ex);
        }
    }
}
