using BFGA.App.Services;
using System.Text;

namespace BFGA.App.Tests;

public class BoardDebugLoggerTests
{
    [Fact]
    public void EnabledLogger_CreatesPlainTextLogUnderDocumentsRoot()
    {
        var documentsRoot = CreateTempDirectory();

        try
        {
            string logPath;

            using (var logger = BoardDebugLogger.CreateIfEnabled(documentsRoot, enabled: true))
            {
                Assert.NotNull(logger);

                logPath = logger!.LogPath;
                logger.Write("board-event", "first line");
                logger.Write("board-event", "second line");
            }

            var logDirectory = Path.Combine(documentsRoot, "BFGA", "logs");
            var files = Directory.GetFiles(logDirectory, "*.log");

            Assert.Single(files);
            Assert.Equal(logPath, files[0]);

            var bytes = File.ReadAllBytes(files[0]);

            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

            var contents = Encoding.UTF8.GetString(bytes);

            Assert.Contains("[board-event] first line", contents);
            Assert.Contains("[board-event] second line", contents);
        }
        finally
        {
            Directory.Delete(documentsRoot, recursive: true);
        }
    }

    [Fact]
    public void EnabledLogger_PrunesOlderLogsAndKeepsNewestFive()
    {
        var documentsRoot = CreateTempDirectory();

        try
        {
            var logDirectory = Path.Combine(documentsRoot, "BFGA", "logs");
            Directory.CreateDirectory(logDirectory);

            for (var index = 1; index <= 6; index++)
            {
                var path = Path.Combine(logDirectory, $"old-{index}.log");
                File.WriteAllText(path, $"old {index}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-index));
            }

            using var logger = BoardDebugLogger.CreateIfEnabled(documentsRoot, enabled: true);

            Assert.NotNull(logger);
            Assert.StartsWith(Path.Combine(documentsRoot, "BFGA", "logs"), logger!.LogPath, StringComparison.OrdinalIgnoreCase);

            var files = Directory.GetFiles(logDirectory, "*.log");

            Assert.Equal(5, files.Length);
            Assert.DoesNotContain(files, file => Path.GetFileName(file).StartsWith("old-6", StringComparison.Ordinal));
            Assert.DoesNotContain(files, file => Path.GetFileName(file).StartsWith("old-5", StringComparison.Ordinal));
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("old-1", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(documentsRoot, recursive: true);
        }
    }

    [Fact]
    public void DisabledLogger_ReturnsNullAndDoesNotCreateDirectoriesOrFiles()
    {
        var documentsRoot = CreateTempDirectory();

        try
        {
            var logger = BoardDebugLogger.CreateIfEnabled(documentsRoot, enabled: false);

            Assert.Null(logger);

            Assert.False(Directory.Exists(Path.Combine(documentsRoot, "BFGA")));
        }
        finally
        {
            Directory.Delete(documentsRoot, recursive: true);
        }
    }

    [Fact]
    public void Dispose_CanBeCalledRepeatedly()
    {
        var documentsRoot = CreateTempDirectory();

        try
        {
            using var logger = BoardDebugLogger.CreateIfEnabled(documentsRoot, enabled: true);

            Assert.NotNull(logger);

            logger!.Dispose();
            logger.Dispose();
        }
        finally
        {
            Directory.Delete(documentsRoot, recursive: true);
        }
    }

    [Fact]
    public void Write_AfterDispose_DoesNotThrow()
    {
        var documentsRoot = CreateTempDirectory();

        try
        {
            var logger = BoardDebugLogger.CreateIfEnabled(documentsRoot, enabled: true);

            Assert.NotNull(logger);

            logger!.Dispose();

            var exception = Record.Exception(() => logger.Write("board-event", "ignored"));

            Assert.Null(exception);
        }
        finally
        {
            Directory.Delete(documentsRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateIfEnabled_IgnoresLockedOldLogsAndKeepsNewLogWritable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var documentsRoot = CreateTempDirectory();
        FileStream? lockedOldStream = null;

        try
        {
            var logDirectory = Path.Combine(documentsRoot, "BFGA", "logs");
            Directory.CreateDirectory(logDirectory);

            var lockedOldLog = CreateLockedFile(logDirectory, "locked-old.log", "locked old log");
            lockedOldStream = lockedOldLog.Stream;
            var staleOldLogPath = Path.Combine(logDirectory, "old-1.log");
            File.WriteAllText(staleOldLogPath, "old 1");

            File.SetLastWriteTimeUtc(lockedOldLog.Path, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(staleOldLogPath, DateTime.UtcNow.AddMinutes(-9));

            for (var index = 2; index <= 5; index++)
            {
                var path = Path.Combine(logDirectory, $"old-{index}.log");
                File.WriteAllText(path, $"old {index}");
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-10 + index));
            }

            using var logger = BoardDebugLogger.CreateIfEnabled(documentsRoot, enabled: true);

            Assert.NotNull(logger);

            logger!.Write("board-event", "new line");

            var files = Directory.GetFiles(logDirectory, "*.log");

            Assert.Contains(files, file => Path.GetFileName(file) == Path.GetFileName(lockedOldLog.Path));
            Assert.DoesNotContain(files, file => Path.GetFileName(file) == Path.GetFileName(staleOldLogPath));
            Assert.Contains(files, file => file == logger.LogPath);

            using var reader = new StreamReader(new FileStream(logger.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            var contents = reader.ReadToEnd();

            Assert.Contains("[board-event] new line", contents);
        }
        finally
        {
            lockedOldStream?.Dispose();
            Directory.Delete(documentsRoot, recursive: true);
        }
    }

    private static (string Path, FileStream Stream) CreateLockedFile(string directory, string fileName, string contents)
    {
        var path = Path.Combine(directory, fileName);
        var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var bytes = Encoding.UTF8.GetBytes(contents);
        stream.Write(bytes);
        stream.Flush();
        stream.Position = 0;
        return (path, stream);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bfga-debug-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
