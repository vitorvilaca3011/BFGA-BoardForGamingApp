using BFGA.App.Services;
using BFGA.App.Infrastructure;
using BFGA.App.Networking;
using BFGA.App.ViewModels;
using BFGA.App.Views;
using BFGA.Canvas.Rendering;
using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network;
using BFGA.Network.Protocol;
using System.IO;
using System.Reflection;

namespace BFGA.App.Tests;

[Collection("BFGA_BOARD_DEBUG_LOG")]
public class MainViewModelTests
{
    [Fact]
    public async Task CurrentScreen_IsReadOnlyAndDerivedFromConnectionState()
    {
        var sut = new MainViewModel();

        var property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.CurrentScreen))!;

        Assert.False(property.CanWrite);
        Assert.IsType<ConnectionScreenViewModel>(sut.CurrentScreen);

        sut.SelectedMode = ConnectionMode.Host;
        await sut.StartHostAsync();
        Assert.IsType<BoardScreenViewModel>(sut.CurrentScreen);

        await sut.StopHostAsync();
        Assert.IsType<ConnectionScreenViewModel>(sut.CurrentScreen);

        sut.SelectedMode = ConnectionMode.Join;
        Assert.IsType<ConnectionScreenViewModel>(sut.CurrentScreen);

        await sut.ConnectAsync();
        Assert.IsType<ConnectionScreenViewModel>(sut.CurrentScreen);
    }

    [Fact]
    public void ScreenViewModels_KeepReferenceToMainViewModel()
    {
        var sut = new MainViewModel();
        var connectionScreen = new ConnectionScreenViewModel(sut);
        var boardScreen = new BoardScreenViewModel(sut);

        Assert.Same(sut, connectionScreen.MainViewModel);
        Assert.Same(sut, boardScreen.MainViewModel);
    }

    [Fact]
    public void ConnectionAndBoardScreens_ForwardToExpectedViews()
    {
        var connectionXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "ConnectionScreen.axaml"));
        var boardXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "BoardScreen.axaml"));

        Assert.Contains("DataContext=\"{Binding MainViewModel}\"", connectionXaml);
        Assert.Contains("Board=\"{Binding MainViewModel.Board}\"", boardXaml);
        Assert.Contains("RemoteCursors=\"{Binding MainViewModel.RemoteCursors}\"", boardXaml);
        Assert.Contains("RemoteStrokePreviews=\"{Binding MainViewModel.RemoteStrokePreviews}\"", boardXaml);
    }

    [Fact]
    public void MainViewModel_ExposesConnectionScreenComputedWrappers()
    {
        var sut = new MainViewModel();

        Assert.True(sut.IsHostMode);
        Assert.False(sut.IsJoinMode);
        Assert.True(sut.IsDisconnected);
        Assert.Equal("START HOST", sut.PrimaryButtonText);
        Assert.Same(sut.StartHostCommand, sut.PrimaryActionCommand);
        Assert.True(sut.IsPrimaryButtonEnabled);

        sut.SelectedMode = ConnectionMode.Join;

        Assert.False(sut.IsHostMode);
        Assert.True(sut.IsJoinMode);
        Assert.Equal("CONNECT", sut.PrimaryButtonText);
        Assert.Same(sut.ConnectCommand, sut.PrimaryActionCommand);
        Assert.True(sut.IsPrimaryButtonEnabled);
    }

    [Fact]
    public async Task MainViewModel_PrimaryWrappersReflectConnectionStateChanges()
    {
        var sessions = new FakeGameSessionFactory { DelayConnectCompletion = true };
        var sut = new MainViewModel(sessionFactory: sessions);

        Assert.True(sut.IsDisconnected);
        Assert.Equal("START HOST", sut.PrimaryButtonText);

        sut.SelectedMode = ConnectionMode.Host;
        await sut.StartHostAsync();

        Assert.False(sut.IsDisconnected);
        Assert.Equal("STOP HOST", sut.PrimaryButtonText);
        Assert.Same(sut.StopHostCommand, sut.PrimaryActionCommand);

        await sut.StopHostAsync();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();

        Assert.Equal(ShellConnectionState.Joining, sut.ConnectionState);
        Assert.Equal("CONNECTING...", sut.PrimaryButtonText);
        Assert.Same(sut.ConnectCommand, sut.PrimaryActionCommand);
        Assert.False(sut.IsPrimaryButtonEnabled);

        sessions.LastCreatedClient!.RaiseConnected();

        Assert.Equal("DISCONNECT", sut.PrimaryButtonText);
        Assert.Same(sut.DisconnectCommand, sut.PrimaryActionCommand);
        Assert.True(sut.IsPrimaryButtonEnabled);
    }

    [Fact]
    public async Task BoardScreen_FollowsMainViewModelStateReplacements()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions);
        var screen = new BoardScreen { DataContext = new BoardScreenViewModel(sut) };

        var replacementBoard = new BoardState { BoardName = "Replacement" };
        replacementBoard.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Replacement text",
            FontSize = 18,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        });

        try
        {
            await BoardFileStore.SaveAsync(replacementBoard, filePath);

            await sut.StartHostAsync();
            await sut.LoadBoardAsync();

            Assert.Equal("Replacement", screen.BoardView.Board!.BoardName);

            sut.SelectedMode = ConnectionMode.Join;
            await sut.ConnectAsync();
            sessions.LastCreatedClient!.RaiseConnected();

            var remoteClientId = Guid.NewGuid();
            sessions.LastCreatedClient.RaiseOperationReceived(new CursorUpdateOperation(remoteClientId, new System.Numerics.Vector2(12, 34)));

            var strokeId = Guid.NewGuid();
            sessions.LastCreatedClient.RaiseOperationReceived(new DrawStrokePointOperation(strokeId, new System.Numerics.Vector2(1, 2)) { SenderId = remoteClientId });

            Assert.Contains(remoteClientId, screen.BoardView.RemoteCursors!.Keys);
            Assert.Contains(strokeId, screen.BoardView.RemoteStrokePreviews!.Keys);
        }
        finally
        {
            await sut.StopHostAsync();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void MainWindow_DataTemplatesBuildCorrectScreenControls()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "MainWindow.axaml"));

        Assert.Contains("DataTemplate DataType=\"vm:ConnectionScreenViewModel\"", xaml);
        Assert.Contains("DataTemplate DataType=\"vm:BoardScreenViewModel\"", xaml);
        Assert.Contains("ContentControl Content=\"{Binding CurrentScreen}\"", xaml);
        Assert.Contains("<Grid Margin=\"0\">", xaml);
    }

    [Fact]
    public void MainWindow_ShellNavigation_DoesNotUseTransitioningContentControlOrCrossFade()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "MainWindow.axaml"));

        Assert.Contains("ContentControl Content=\"{Binding CurrentScreen}\"", xaml);
        Assert.DoesNotContain("TransitioningContentControl Content=\"{Binding CurrentScreen}\"", xaml);
        Assert.DoesNotContain("TransitioningContentControl.PageTransition", xaml);
        Assert.DoesNotContain("<CrossFade", xaml);
    }

    [Fact]
    public void WhiteboardTheme_DefinesConnectionScreenStyleTokens()
    {
        var themeXaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Styles", "WhiteboardTheme.axaml"));

        Assert.Contains("connection-tab", themeXaml);
        Assert.Contains("connection-tab.active", themeXaml);
        Assert.Contains("input-label", themeXaml);
        Assert.Contains("connection-input", themeXaml);
        Assert.Contains("connection-primary-btn", themeXaml);
        Assert.Contains("connection-secondary-btn", themeXaml);
    }

    [Fact]
    public void Constructor_OptInCreatesBoardDebugLogUnderDocumentsRoot()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), $"bfga-mainvm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(documentsRoot);

        var previous = Environment.GetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG");
        Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", "1");

        try
        {
            var sut = new MainViewModel(documentsFolderProvider: () => documentsRoot);
            var screen = new BoardScreenViewModel(sut);

            screen.SelectedTool = BoardToolType.Pen;

            sut.Dispose();
            screen.Dispose();

            var logDirectory = Path.Combine(documentsRoot, "BFGA", "logs");
            var files = Directory.GetFiles(logDirectory, "*.log");

            Assert.Single(files);
            Assert.Contains("selected-tool", File.ReadAllText(files[0]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", previous);
        }
    }

    [Fact]
    public void Constructor_WhenDisabledDoesNotCreateBoardDebugLogFolder()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), $"bfga-mainvm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(documentsRoot);

        var previous = Environment.GetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG");
        Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", null);

        try
        {
            var providerInvoked = false;
            var sut = new MainViewModel(documentsFolderProvider: () =>
            {
                providerInvoked = true;
                return documentsRoot;
            });
            var screen = new BoardScreenViewModel(sut);

            screen.SelectedTool = BoardToolType.Pen;

            Assert.False(providerInvoked);
            Assert.False(Directory.Exists(Path.Combine(documentsRoot, "BFGA")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", previous);
        }
    }

    [Fact]
    public void Dispose_DisablesBoardDebugLogging()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), $"bfga-mainvm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(documentsRoot);

        var previous = Environment.GetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG");
        Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", "1");

        try
        {
            var sut = new MainViewModel(documentsFolderProvider: () => documentsRoot);
            var screen = new BoardScreenViewModel(sut);

            screen.SelectedTool = BoardToolType.Pen;
            var logFile = Directory.GetFiles(Path.Combine(documentsRoot, "BFGA", "logs"), "*.log").Single();
            var before = ReadAllTextShared(logFile);

            sut.Dispose();

            screen.SelectedTool = BoardToolType.Rectangle;

            var after = ReadAllTextShared(logFile);
            Assert.Equal(before, after);

            screen.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", previous);
        }
    }

    [Fact]
    public void Dispose_PreventsLazyBoardDebugFactoryFromRunning()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), $"bfga-mainvm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(documentsRoot);

        var previous = Environment.GetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG");
        Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", "1");

        try
        {
            var sut = new MainViewModel(documentsFolderProvider: () => documentsRoot);
            sut.Dispose();

            var invoked = false;
            typeof(MainViewModel)
                .GetMethod("LogBoardDebug", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(string), typeof(Func<string>)])!
                .Invoke(sut, ["after-dispose", new Func<string>(() =>
                {
                    invoked = true;
                    return "should-not-run";
                })]);

            Assert.False(invoked);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BFGA_BOARD_DEBUG_LOG", previous);
        }
    }

    private static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void ConnectionView_UsesCardWrapperAroundCenteredFormContent()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BFGA.App", "Views", "ConnectionView.axaml"));
        var normalized = new string(xaml.Where(character => !char.IsWhiteSpace(character)).ToArray());

        const string wrapperStart = "<BorderHorizontalAlignment=\"Center\"VerticalAlignment=\"Center\"Width=\"380\"Padding=\"28\"Background=\"{DynamicResourceBgSurface}\"BorderBrush=\"{DynamicResourceBorderDefault}\"BorderThickness=\"1\"CornerRadius=\"20\"><GridRowSpacing=\"32\">";
        const string primaryButton = "<ButtonClasses=\"connection-primary-btn\"";

        Assert.Contains(wrapperStart, normalized);

        var borderStart = normalized.IndexOf(wrapperStart, StringComparison.Ordinal);
        var primaryButtonStart = normalized.IndexOf(primaryButton, StringComparison.Ordinal);
        var borderEnd = normalized.IndexOf("</Border>", borderStart, StringComparison.Ordinal);

        Assert.True(borderStart >= 0);
        Assert.True(primaryButtonStart > borderStart);
        Assert.True(borderEnd > primaryButtonStart);
    }

    [Fact]
    public async Task ConnectAsync_StartsJoinAttemptWithoutBlockingForConnectionCompletion()
    {
        var sessions = new FakeGameSessionFactory { DelayConnectCompletion = true };
        var sut = new MainViewModel(sessionFactory: sessions);

        await sut.ConnectAsync();
        Assert.Equal(ShellConnectionState.Joining, sut.ConnectionState);
        Assert.NotNull(sessions.LastCreatedClient);
        Assert.False(sessions.LastCreatedClient!.PendingConnectTask.IsCompleted);

        sessions.LastCreatedClient!.RaiseConnected();

        Assert.Equal(ShellConnectionState.Connected, sut.ConnectionState);
    }

    [Fact]
    public void Constructor_InitializesShellDefaults()
    {
        var sut = new MainViewModel();

        Assert.Equal(ConnectionMode.Host, sut.SelectedMode);
        Assert.Equal(ShellConnectionState.Disconnected, sut.ConnectionState);
        Assert.IsType<ConnectionScreenViewModel>(sut.CurrentScreen);
        Assert.NotNull(sut.Board);
        Assert.False(string.IsNullOrWhiteSpace(sut.StatusText));
    }

    [Fact]
    public async Task StartHostAsync_CreatesHostAndUpdatesStatus()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);

        await sut.StartHostAsync();

        Assert.NotNull(sut.Host);
        Assert.True(sut.Host!.IsRunning);
        Assert.Equal(ShellConnectionState.Hosting, sut.ConnectionState);
        Assert.IsType<BoardScreenViewModel>(sut.CurrentScreen);
        Assert.Contains("Hosting", sut.StatusText);

        await sut.StopHostAsync();

        Assert.Null(sut.Host);
        Assert.Equal(ShellConnectionState.Disconnected, sut.ConnectionState);
        Assert.IsType<ConnectionScreenViewModel>(sut.CurrentScreen);
    }

    [Fact]
    public async Task ShellCommand_Exceptions_UpdateStatusViaCallback()
    {
        var sut = new MainViewModel();
        Func<bool> canExecute = () => true;

        var command = (AsyncRelayCommand)typeof(MainViewModel)
            .GetMethod("CreateShellCommand", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sut, new object[]
            {
                (Func<Task>)(() => throw new InvalidOperationException("boom")),
                canExecute,
                "Failed to run shell command: "
            })!;

        await command.ExecuteAsync();

        Assert.Equal("Failed to run shell command: boom", sut.StatusText);
    }

    [Fact]
    public async Task SaveAndLoadBoard_PersistsHostBoardState()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sut = new MainViewModel(dialog, new FakeGameSessionFactory());
        sut.Board.BoardName = "Shell Test";
        sut.Board.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Hello shell",
            FontSize = 20,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        });

        try
        {
            await sut.StartHostAsync();
            await sut.SaveBoardAsync();

            sut.Host!.BoardState.BoardName = "Changed";
            sut.Host.BoardState.Elements.Clear();
            await sut.LoadBoardAsync();

            Assert.Equal("Shell Test", sut.Host.BoardState.BoardName);
            Assert.Single(sut.Host.BoardState.Elements);
            Assert.Equal("Hello shell", Assert.IsType<TextElement>(sut.Host.BoardState.Elements[0]).Text);
        }
        finally
        {
            await sut.StopHostAsync();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ClientSave_RequestFullSyncsBeforePromptingForPath()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions);

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();

        var saveTask = sut.SaveBoardAsync();
        Assert.Equal(1, sessions.LastCreatedClient.RequestFullSyncCalls);

        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(Guid.NewGuid(), new BoardState(), new Dictionary<Guid, PlayerInfo>()));
        await saveTask;

        Assert.Single(dialog.SaveRequests);
    }

    [Fact]
    public async Task ClientSave_DoesNotPromptUntilFullSyncArrives()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions);

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();

        var saveTask = sut.SaveBoardAsync();
        await Task.Delay(25);

        Assert.Equal(1, sessions.LastCreatedClient.RequestFullSyncCalls);
        Assert.Empty(dialog.SaveRequests);

        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(Guid.NewGuid(), new BoardState(), new Dictionary<Guid, PlayerInfo>()));
        await saveTask;

        Assert.Single(dialog.SaveRequests);
    }

    [Fact]
    public async Task ClientSave_FailsCleanlyWhenFullSyncNeverArrives()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions, fullSyncTimeout: TimeSpan.Zero);

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();

        await sut.SaveBoardAsync();

        Assert.Empty(dialog.SaveRequests);
        Assert.Contains("Failed to save board", sut.StatusText);
    }

    [Fact]
    public async Task ClientSave_WritesPostSyncBoardContents()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions);

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();

        sut.Board.BoardName = "Stale Board";
        sut.Board.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Stale text",
            FontSize = 12,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        });

        var saveTask = sut.SaveBoardAsync();
        Assert.Equal(1, sessions.LastCreatedClient.RequestFullSyncCalls);

        var refreshedBoard = new BoardState { BoardName = "Fresh Board" };
        refreshedBoard.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Fresh text",
            FontSize = 18,
            Color = SkiaSharp.SKColors.Blue,
            FontFamily = "Arial"
        });

        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(Guid.NewGuid(), refreshedBoard, new Dictionary<Guid, PlayerInfo>()));
        await saveTask;

        var savedBoard = await BoardFileStore.LoadAsync(filePath);
        Assert.Equal("Fresh Board", savedBoard.BoardName);
        Assert.Single(savedBoard.Elements);
        Assert.Equal("Fresh text", Assert.IsType<TextElement>(savedBoard.Elements[0]).Text);
    }

    [Fact]
    public async Task HostSave_DoesNotRequestFullSync()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions);

        await sut.StartHostAsync();
        await sut.SaveBoardAsync();

        Assert.Equal(0, sessions.LastCreatedClient?.RequestFullSyncCalls ?? 0);
        Assert.Single(dialog.SaveRequests);
    }

    [Fact]
    public async Task HostAutosave_WritesToDocumentsFolderWithoutPrompting()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions, documentsFolderProvider: () => documentsRoot);

        sut.Board.BoardName = "My Host Board";
        await sut.StartHostAsync();

        await sut.CloseAsync();

        var expectedPath = Path.Combine(documentsRoot, "BFGA", "My Host Board.bfga");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task HostAutosave_FailuresAreCapturedInStatusText()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sut = new MainViewModel(sessionFactory: new FakeGameSessionFactory(), documentsFolderProvider: () => documentsRoot + "<invalid>");

        await sut.StartHostAsync();
        await sut.CloseAsync();

        Assert.Contains("Failed to autosave board", sut.StatusText);
    }

    [Fact]
    public async Task StartAndStopHost_TogglesAutosaveTimerAndFinalSaveOnClose()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions, documentsFolderProvider: () => documentsRoot);

        await sut.StartHostAsync();
        Assert.True(sut.IsAutosaveTimerEnabled);

        await sut.StopHostAsync();
        Assert.False(sut.IsAutosaveTimerEnabled);
    }

    [Fact]
    public async Task MainWindowClose_PerformsFinalHostAutosaveBeforeDisposal()
    {
        var documentsRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var vm = new MainViewModel(sessionFactory: new FakeGameSessionFactory(), documentsFolderProvider: () => documentsRoot);
        vm.Board.BoardName = "Window Close Board";
        await vm.StartHostAsync();

        MainWindow.CloseDataContext(vm);

        var expectedPath = Path.Combine(documentsRoot, "BFGA", "Window Close Board.bfga");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task DisconnectCleansUpPendingFullSyncWait()
    {
        var dialog = new FakeFileDialogService(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga"));
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions, fullSyncTimeout: TimeSpan.FromSeconds(5));

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();

        var saveTask = sut.SaveBoardAsync();
        await Task.Delay(25);

        await sut.DisconnectAsync();
        await saveTask;

        Assert.Empty(dialog.SaveRequests);
        Assert.Contains("Failed to save board", sut.StatusText);
    }

    [Fact]
    public async Task LoadWhileHosting_ReplacesShellBoardWithoutSharingHostReference()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sut = new MainViewModel(dialog, new FakeGameSessionFactory());
        sut.Board.BoardName = "Loaded Board";
        sut.Board.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Loaded text",
            FontSize = 18,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        });

        try
        {
            await BoardFileStore.SaveAsync(sut.Board, filePath);

            await sut.StartHostAsync();
            var hostBoard = sut.Host!.BoardState;

            hostBoard.BoardName = "Mutated host";
            hostBoard.Elements.Clear();

            await sut.LoadBoardAsync();

            Assert.Equal("Loaded Board", sut.Board.BoardName);
            Assert.Equal("Loaded Board", sut.Host.BoardState.BoardName);
            Assert.NotSame(sut.Board, sut.Host.BoardState);
            Assert.Single(sut.Board.Elements);
            Assert.Single(sut.Host.BoardState.Elements);
        }
        finally
        {
            await sut.StopHostAsync();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void ClientMode_DisablesHostOnlyLoadSave()
    {
        var sut = new MainViewModel();

        sut.SelectedMode = ConnectionMode.Join;

        Assert.False(sut.CanLoadBoard);
        Assert.False(sut.CanSaveBoard);
    }

    [Fact]
    public async Task FullSyncResponse_UpdatesShellBoardForJoinedClient()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var board = new BoardState
        {
            BoardName = "Remote Board"
        };
        board.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Synced text",
            FontSize = 16,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        });
        var localClientId = Guid.NewGuid();
        var remoteClientId = Guid.NewGuid();
        var sync = new FullSyncResponseOperation(localClientId, board, new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) },
            { remoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) }
        });

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseOperationReceived(sync);

        Assert.Equal("Remote Board", sut.Board.BoardName);
        Assert.Single(sut.Board.Elements);
        Assert.Equal(2, sut.Roster.Count);
        Assert.IsType<BoardScreenViewModel>(sut.CurrentScreen);
    }

    [Fact]
    public async Task InboundPresenceAndPreviewOperations_UpdateShellState()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var localClientId = Guid.NewGuid();
        var remoteClientId = Guid.NewGuid();
        var sync = new FullSyncResponseOperation(localClientId, new BoardState { BoardName = "Remote" }, new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) },
            { remoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) }
        });

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseOperationReceived(sync);

        sessions.LastCreatedClient.RaiseOperationReceived(new PeerJoinedOperation(Guid.NewGuid(), "New Remote", SkiaSharp.SKColors.Green));
        Assert.Equal(3, sut.Roster.Count);

        var cursorPoint = new System.Numerics.Vector2(10, 15);
        sessions.LastCreatedClient.RaiseOperationReceived(new CursorUpdateOperation(remoteClientId, cursorPoint));
        Assert.Single(sut.RemoteCursors);

        var strokeId = Guid.NewGuid();
        sessions.LastCreatedClient.RaiseOperationReceived(new DrawStrokePointOperation(strokeId, new System.Numerics.Vector2(1, 2)) { SenderId = remoteClientId });
        sessions.LastCreatedClient.RaiseOperationReceived(new DrawStrokePointOperation(strokeId, new System.Numerics.Vector2(3, 4)) { SenderId = remoteClientId });
        Assert.Single(sut.RemoteStrokePreviews);

        sessions.LastCreatedClient.RaiseOperationReceived(new CancelStrokeOperation(strokeId) { SenderId = remoteClientId });
        Assert.Empty(sut.RemoteStrokePreviews);

        var strokeElement = new StrokeElement
        {
            Id = strokeId,
            Points = new List<System.Numerics.Vector2> { new(0, 0), new(5, 5) },
            Thickness = 2f
        };
        sessions.LastCreatedClient.RaiseOperationReceived(new AddElementOperation(strokeElement) { SenderId = remoteClientId });
        Assert.Empty(sut.RemoteStrokePreviews);

        sessions.LastCreatedClient.RaiseOperationReceived(new PeerLeftOperation(remoteClientId));
        Assert.DoesNotContain(remoteClientId, sut.Roster.Keys);
        Assert.DoesNotContain(remoteClientId, sut.RemoteCursors.Keys);
    }

    [Fact]
    public async Task RosterSync_ReconcilesPlaceholderOverlayMetadata()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var remoteClientId = Guid.NewGuid();
        var localClientId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();

        sessions.LastCreatedClient!.RaiseOperationReceived(new CursorUpdateOperation(remoteClientId, new System.Numerics.Vector2(10, 15)));
        sessions.LastCreatedClient.RaiseOperationReceived(new DrawStrokePointOperation(Guid.NewGuid(), new System.Numerics.Vector2(1, 2)) { SenderId = remoteClientId });

        Assert.StartsWith("Player ", sut.RemoteCursors[remoteClientId].DisplayName);
        Assert.Equal(SkiaSharp.SKColors.White, sut.RemoteCursors[remoteClientId].AssignedColor);

        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(localClientId, new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { remoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) },
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) }
        }));

        Assert.Equal("Remote", sut.RemoteCursors[remoteClientId].DisplayName);
        Assert.Equal(SkiaSharp.SKColors.Red, sut.RemoteCursors[remoteClientId].AssignedColor);
        Assert.Equal("Remote", sut.RemoteStrokePreviews.Values.Single().DisplayName);
        Assert.Equal(SkiaSharp.SKColors.Red, sut.RemoteStrokePreviews.Values.Single().AssignedColor);
        Assert.DoesNotContain(localClientId, sut.RemoteCursors.Keys);
        Assert.DoesNotContain(sut.RemoteStrokePreviews.Values, preview => preview.ClientId == localClientId);
    }

    [Fact]
    public async Task FullSync_RemovesLocalPlaceholderPresenceFromRemoteState()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var localClientId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();

        sessions.LastCreatedClient!.RaiseOperationReceived(new CursorUpdateOperation(localClientId, new System.Numerics.Vector2(5, 6)));

        Assert.Single(sut.RemoteCursors);

        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(localClientId, new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) }
        }));

        Assert.Empty(sut.RemoteCursors);
        Assert.Empty(sut.RemoteStrokePreviews);
    }

    [Fact]
    public async Task LaserPointerOperation_RemotePeer_UsesAssignedRosterColor()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var localClientId = Guid.NewGuid();
        var remoteClientId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();
        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(localClientId, new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) },
            { remoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) }
        }));

        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(remoteClientId, new System.Numerics.Vector2(10, 15), true));

        var remoteLaser = Assert.Contains(remoteClientId, sut.RemoteLasers);
        Assert.Equal(SkiaSharp.SKColors.Red, remoteLaser.Color);
        Assert.True(remoteLaser.IsActive);
        Assert.Equal(1, remoteLaser.Trail.Count);
    }

    [Fact]
    public async Task LaserPointerOperation_InactiveUpdate_PreservesTrailForFade()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var localClientId = Guid.NewGuid();
        var remoteClientId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();
        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(localClientId, new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) },
            { remoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) }
        }));

        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(remoteClientId, new System.Numerics.Vector2(10, 15), true));
        var remoteLaser = Assert.Contains(remoteClientId, sut.RemoteLasers);
        var trailCountBeforeRelease = remoteLaser.Trail.Count;

        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(remoteClientId, new System.Numerics.Vector2(20, 25), false));

        remoteLaser = Assert.Contains(remoteClientId, sut.RemoteLasers);
        Assert.False(remoteLaser.IsActive);
        Assert.Equal(trailCountBeforeRelease, remoteLaser.Trail.Count);
    }

    [Fact]
    public async Task FullSync_ReconcilesRemoteLaserColorAndDropsUnknownPeers()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var localClientId = Guid.NewGuid();
        var knownRemoteClientId = Guid.NewGuid();
        var unknownRemoteClientId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();

        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(knownRemoteClientId, new System.Numerics.Vector2(10, 15), true));
        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(unknownRemoteClientId, new System.Numerics.Vector2(20, 25), true));

        Assert.Equal(SkiaSharp.SKColors.White, sut.RemoteLasers[knownRemoteClientId].Color);
        Assert.Equal(SkiaSharp.SKColors.White, sut.RemoteLasers[unknownRemoteClientId].Color);

        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(localClientId, new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) },
            { knownRemoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) }
        }));

        var remoteLaser = Assert.Contains(knownRemoteClientId, sut.RemoteLasers);
        Assert.Equal(SkiaSharp.SKColors.Red, remoteLaser.Color);
        Assert.DoesNotContain(unknownRemoteClientId, sut.RemoteLasers.Keys);
    }

    [Fact]
    public async Task PeerLeft_RemovesRemoteLaserState()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var localClientId = Guid.NewGuid();
        var remoteClientId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();
        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(localClientId, new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) },
            { remoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) }
        }));

        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(remoteClientId, new System.Numerics.Vector2(10, 15), true));
        Assert.Contains(remoteClientId, sut.RemoteLasers.Keys);

        sessions.LastCreatedClient.RaiseOperationReceived(new PeerLeftOperation(remoteClientId));

        Assert.DoesNotContain(remoteClientId, sut.RemoteLasers.Keys);
    }

    [Fact]
    public async Task LaserPointerOperation_MultiplePeers_KeepIndependentLaserState()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var localClientId = Guid.NewGuid();
        var firstRemoteClientId = Guid.NewGuid();
        var secondRemoteClientId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();
        sessions.LastCreatedClient.RaiseOperationReceived(new FullSyncResponseOperation(localClientId, new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { localClientId, new PlayerInfo("Me", SkiaSharp.SKColors.Blue) },
            { firstRemoteClientId, new PlayerInfo("Remote 1", SkiaSharp.SKColors.Red) },
            { secondRemoteClientId, new PlayerInfo("Remote 2", SkiaSharp.SKColors.Green) }
        }));

        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(firstRemoteClientId, new System.Numerics.Vector2(10, 15), true));
        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(secondRemoteClientId, new System.Numerics.Vector2(20, 25), true));
        sessions.LastCreatedClient.RaiseOperationReceived(new LaserPointerOperation(firstRemoteClientId, new System.Numerics.Vector2(30, 35), false));

        var firstRemoteLaser = Assert.Contains(firstRemoteClientId, sut.RemoteLasers);
        var secondRemoteLaser = Assert.Contains(secondRemoteClientId, sut.RemoteLasers);

        Assert.Equal(SkiaSharp.SKColors.Red, firstRemoteLaser.Color);
        Assert.Equal(SkiaSharp.SKColors.Green, secondRemoteLaser.Color);
        Assert.False(firstRemoteLaser.IsActive);
        Assert.True(secondRemoteLaser.IsActive);
        Assert.Equal(1, firstRemoteLaser.Trail.Count);
        Assert.Equal(1, secondRemoteLaser.Trail.Count);
        Assert.Equal(new System.Numerics.Vector2(10, 15), firstRemoteLaser.Trail.GetPoints()[0].Position);
        Assert.Equal(new System.Numerics.Vector2(20, 25), secondRemoteLaser.Trail.GetPoints()[0].Position);
    }

    [Fact]
    public async Task StrokePreview_AppendsWithoutCloningExistingPointList()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var remoteClientId = Guid.NewGuid();
        var strokeId = Guid.NewGuid();

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();

        sessions.LastCreatedClient!.RaiseOperationReceived(new FullSyncResponseOperation(Guid.NewGuid(), new BoardState(), new Dictionary<Guid, PlayerInfo>
        {
            { remoteClientId, new PlayerInfo("Remote", SkiaSharp.SKColors.Red) }
        }));

        sessions.LastCreatedClient.RaiseOperationReceived(new DrawStrokePointOperation(strokeId, new System.Numerics.Vector2(1, 2)) { SenderId = remoteClientId });
        var firstPoints = sut.RemoteStrokePreviews[strokeId].Points;

        sessions.LastCreatedClient.RaiseOperationReceived(new DrawStrokePointOperation(strokeId, new System.Numerics.Vector2(3, 4)) { SenderId = remoteClientId });
        var secondPoints = sut.RemoteStrokePreviews[strokeId].Points;

        Assert.Same(firstPoints, secondPoints);
        Assert.Equal(2, secondPoints.Count);
    }

    [Fact]
    public void BoardViewport_PropagatesRemoteOverlayStateToCanvas()
    {
        var viewport = new BFGA.Canvas.BoardViewport();
        var remoteCursors = new Dictionary<Guid, RemoteCursorState>
        {
            { Guid.NewGuid(), new RemoteCursorState(Guid.NewGuid(), "Remote", SkiaSharp.SKColors.Red, new System.Numerics.Vector2(1, 2)) }
        };
        var remotePreviews = new Dictionary<Guid, RemoteStrokePreviewState>
        {
            { Guid.NewGuid(), new RemoteStrokePreviewState(Guid.NewGuid(), Guid.NewGuid(), "Remote", SkiaSharp.SKColors.Red, new List<System.Numerics.Vector2> { new(0, 0) }) }
        };

        viewport.RemoteCursors = remoteCursors;
        viewport.RemoteStrokePreviews = remotePreviews;

        Assert.Same(remoteCursors, viewport.Canvas.RemoteCursors);
        Assert.Same(remotePreviews, viewport.Canvas.RemoteStrokePreviews);
    }

    [Fact]
    public void BoardView_WiresSelectedToolIntoRuntimeController()
    {
        var sut = new MainViewModel();
        var boardView = new BoardView
        {
            DataContext = new BoardScreenViewModel(sut)
        };

        var controllerField = typeof(BoardView).GetField("_toolController", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(controllerField);

        var controller = controllerField!.GetValue(boardView);
        Assert.NotNull(controller);

        sut.Board.BoardName = "Runtime";
        boardView.DataContext = new BoardScreenViewModel(sut);

        var boardScreen = (BoardScreenViewModel)boardView.DataContext!;
        boardScreen.RectangleToolCommand.Execute(null);

        controller = controllerField.GetValue(boardView);
        Assert.NotNull(controller);

        var currentTool = (BoardToolType)controller!.GetType().GetProperty(nameof(BoardToolController.CurrentTool))!.GetValue(controller)!;

        Assert.Equal(BoardToolType.Rectangle, currentTool);
    }

    [Fact]
    public async Task BoardView_UsesHostBoardStateForRuntimeControllerWhenHosting()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);
        var boardView = new BoardView
        {
            DataContext = new BoardScreenViewModel(sut)
        };

        await sut.StartHostAsync();

        typeof(BoardView)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "SyncToolController" && method.GetParameters().Length == 1)
            .Invoke(boardView, [true]);

        var controllerField = typeof(BoardView).GetField("_toolController", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var controller = controllerField.GetValue(boardView)!;

        var boardProperty = controller.GetType().GetProperty(nameof(BoardToolController.Board))!;
        var controllerBoard = (BoardState)boardProperty.GetValue(controller)!;

        Assert.Same(sut.Board, controllerBoard);
        Assert.NotSame(sut.Host!.BoardState, controllerBoard);
    }

    [Fact]
    public async Task DispatchLocalBoardOperation_UpdatesHostBoardAndBroadcasts()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);

        await sut.StartHostAsync();

        var element = new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Local",
            FontSize = 18,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        };

        await sut.DispatchLocalBoardOperation(new AddElementOperation(element));

        Assert.Single(sut.Board.Elements);
        Assert.Single(sut.Host!.BoardState.Elements);
        Assert.NotSame(sut.Board.Elements[0], sut.Host.BoardState.Elements[0]);
        Assert.Single(sessions.LastCreatedHost!.BroadcastedOperations);
        Assert.IsType<AddElementOperation>(sessions.LastCreatedHost.BroadcastedOperations[0]);
    }

    [Fact]
    public async Task PublishLocalBoardOperation_InHostMode_UpdatesAuthoritativeStateAndBroadcasts()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);

        await sut.StartHostAsync();

        var element = new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Local",
            FontSize = 18,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        };

        sessions.LastCreatedHost!.ReplaceBoardState(new BoardState());
        var operation = new AddElementOperation(element);

        await sut.PublishLocalBoardOperation(operation);

        Assert.Single(sut.Host!.BoardState.Elements);
        Assert.Single(sut.Board.Elements);
        Assert.NotSame(sut.Host.BoardState, sut.Board);
        Assert.Single(sessions.LastCreatedHost.BroadcastedOperations);
    }

    [Fact]
    public async Task LoadWhileHosting_KeepsAuthoritativeHostOperationsWorking()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions);

        var loadedBoard = new BoardState { BoardName = "Loaded" };
        var element = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new System.Numerics.Vector2(10, 10),
            Size = new System.Numerics.Vector2(20, 20),
            Type = ShapeType.Rectangle
        };
        loadedBoard.Elements.Add(element);

        try
        {
            await BoardFileStore.SaveAsync(loadedBoard, filePath);
            await sut.StartHostAsync();
            await sut.LoadBoardAsync();

            await sut.PublishLocalBoardOperation(new DeleteElementOperation(element.Id));

            Assert.Empty(sut.Host!.BoardState.Elements);
            Assert.Empty(sut.Board.Elements);
            Assert.Single(sessions.LastCreatedHost!.BroadcastedOperations);
        }
        finally
        {
            await sut.StopHostAsync();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task LoadWhileHosting_ResyncsAlreadyConnectedClients()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
        var dialog = new FakeFileDialogService(filePath);
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(dialog, sessions);

        var loadedBoard = new BoardState { BoardName = "Loaded" };
        loadedBoard.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Loaded text",
            FontSize = 16,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        });

        try
        {
            await BoardFileStore.SaveAsync(loadedBoard, filePath);
            await sut.StartHostAsync();

            await sut.LoadBoardAsync();

            Assert.Single(sessions.LastCreatedHost!.FullSyncBroadcasts);
            Assert.Equal("Loaded", sut.Host!.BoardState.BoardName);
            Assert.Equal("Loaded", sut.Board.BoardName);
        }
        finally
        {
            await sut.StopHostAsync();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task HostLocalCommit_UsesHostApplyPathAndBroadcasts()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);

        await sut.StartHostAsync();

        var element = new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Host local",
            FontSize = 16,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        };

        await sut.DispatchLocalBoardOperation(new AddElementOperation(element));

        Assert.Single(sut.Host!.BoardState.Elements);
        Assert.Single(sut.Board.Elements);
        Assert.NotSame(sut.Host.BoardState, sut.Board);
        Assert.Single(sessions.LastCreatedHost!.BroadcastedOperations);
    }

    [Fact]
    public void BoardToolController_PreservesSelectionAcrossBoardReplacementWhenElementStillExists()
    {
        var board = new BoardState();
        var element = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new System.Numerics.Vector2(10, 10),
            Size = new System.Numerics.Vector2(20, 20),
            Type = ShapeType.Rectangle
        };
        board.Elements.Add(element);

        var controller = new BoardToolController(board);
        controller.Selection.Select(element.Id);

        var replacement = new BoardState();
        replacement.Elements.Add(new ShapeElement
        {
            Id = element.Id,
            Position = element.Position,
            Size = element.Size,
            Rotation = element.Rotation,
            ZIndex = element.ZIndex,
            OwnerId = element.OwnerId,
            IsLocked = element.IsLocked,
            Type = element.Type,
            StrokeColor = element.StrokeColor,
            FillColor = element.FillColor,
            StrokeWidth = element.StrokeWidth
        });
        controller.SetBoard(replacement);

        Assert.Single(controller.Selection.SelectedElementIds);
        Assert.Contains(element.Id, controller.Selection.SelectedElementIds);
    }

    [Fact]
    public async Task DispatchLocalBoardOperation_UsesClientSendPathWhenJoined()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();

        var element = new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new System.Numerics.Vector2(5, 5),
            Size = new System.Numerics.Vector2(20, 20),
            Type = ShapeType.Ellipse
        };

        await sut.DispatchLocalBoardOperation(new AddElementOperation(element));

        Assert.Single(sut.Board.Elements);
        Assert.Single(sessions.LastCreatedClient.SentOperations);
        Assert.IsType<AddElementOperation>(sessions.LastCreatedClient.SentOperations[0]);
    }

    [Fact]
    public async Task ClientDisconnected_ClearsClientAndRestoresModeSwitching()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions);

        sut.SelectedMode = ConnectionMode.Join;
        await sut.ConnectAsync();
        sessions.LastCreatedClient!.RaiseConnected();
        sessions.LastCreatedClient.RaiseDisconnected();

        Assert.Null(sut.Client);
        Assert.Equal(ShellConnectionState.Disconnected, sut.ConnectionState);
        Assert.True(sut.CanSwitchMode);
        Assert.IsType<ConnectionScreenViewModel>(sut.CurrentScreen);
    }

    [Fact]
    public async Task AsyncCommandFailuresUpdateStatusInsteadOfThrowing()
    {
        var sut = new MainViewModel(new ThrowingFileDialogService());

        await sut.LoadBoardAsync();

        Assert.Contains("Failed to load board", sut.StatusText);
    }

    [Fact]
    public async Task PollNetwork_TimeoutsJoinAttemptWithoutRealSocketDependency()
    {
        var sessions = new FakeGameSessionFactory();
        var sut = new MainViewModel(sessionFactory: sessions, joinTimeout: TimeSpan.Zero);

        sut.SelectedMode = ConnectionMode.Join;
        sut.HostAddress = "127.0.0.1";
        sut.HostPort = 59999;

        await sut.ConnectAsync();

        sut.PollNetwork();

        Assert.Null(sut.Client);
        Assert.Equal(ShellConnectionState.Disconnected, sut.ConnectionState);
        Assert.Contains("Failed to connect to 127.0.0.1:59999", sut.StatusText);
    }

    [Fact]
    public async Task HostAndShellBoardCopies_DoNotShareElementInstances()
    {
        var sut = new MainViewModel(sessionFactory: new FakeGameSessionFactory());
        var element = new TextElement
        {
            Id = Guid.NewGuid(),
            Text = "Original",
            FontSize = 12,
            Color = SkiaSharp.SKColors.Black,
            FontFamily = "Arial"
        };
        sut.Board.Elements.Add(element);

        await sut.StartHostAsync();

        try
        {
            Assert.NotSame(sut.Board.Elements[0], sut.Host!.BoardState.Elements[0]);

            ((TextElement)sut.Host.BoardState.Elements[0]).Text = "Mutated host";

            Assert.Equal("Original", ((TextElement)sut.Board.Elements[0]).Text);
        }
        finally
        {
            await sut.StopHostAsync();
        }
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        private readonly string _filePath;
        public List<string> SaveRequests { get; } = new();

        public FakeFileDialogService(string filePath)
        {
            _filePath = filePath;
        }

        public Task<string?> OpenBoardPathAsync() => Task.FromResult<string?>(_filePath);

        public Task<string?> SaveBoardPathAsync(string suggestedFileName)
        {
            SaveRequests.Add(suggestedFileName);
            return Task.FromResult<string?>(_filePath);
        }

        public Task<string?> OpenImagePathAsync() => Task.FromResult<string?>(null);
    }

    private sealed class ThrowingFileDialogService : IFileDialogService
    {
        public Task<string?> OpenBoardPathAsync() => throw new InvalidOperationException("dialog failure");

        public Task<string?> SaveBoardPathAsync(string suggestedFileName) => throw new InvalidOperationException("dialog failure");

        public Task<string?> OpenImagePathAsync() => throw new InvalidOperationException("dialog failure");
    }

    private sealed class FakeGameSessionFactory : IGameSessionFactory
    {
        public FakeGameHostSession? LastCreatedHost { get; private set; }
        public FakeGameClientSession? LastCreatedClient { get; private set; }
        public bool DelayConnectCompletion { get; set; }

        public IGameHostSession CreateHost()
        {
            LastCreatedHost = new FakeGameHostSession();
            return LastCreatedHost;
        }

        public IGameClientSession CreateClient(string displayName)
        {
            LastCreatedClient = new FakeGameClientSession(displayName, DelayConnectCompletion);
            return LastCreatedClient;
        }
    }

    private sealed class FakeGameHostSession : IGameHostSession
    {
        public List<BoardOperation> BroadcastedOperations { get; } = new();
        public List<BoardState> FullSyncBroadcasts { get; } = new();

        public event EventHandler<PeerJoinedEventArgs>? PeerJoined
        {
            add { }
            remove { }
        }

        public event EventHandler<PeerLeftEventArgs>? PeerLeft
        {
            add { }
            remove { }
        }

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }
        public BoardState BoardState { get; } = new();

        public void Start(int port = 7777)
        {
            IsRunning = true;
            Port = port;
        }

        public void PollEvents()
        {
        }

        public void BroadcastOperation(BoardOperation operation, bool reliable = true)
        {
            BroadcastedOperations.Add(operation);
        }

        public void SyncAllClients()
        {
        }

        public void BroadcastFullSync()
        {
            FullSyncBroadcasts.Add(BoardState);
        }

        public void ReplaceBoardState(BoardState snapshot)
        {
            BoardState.BoardName = snapshot.BoardName;
            BoardState.BoardId = snapshot.BoardId;
            BoardState.LastModified = snapshot.LastModified;
            BoardState.Elements = snapshot.Elements.ToList();
        }

        public bool TryApplyLocalOperation(BoardOperation operation)
        {
            switch (operation)
            {
                case AddElementOperation add:
                    BoardState.Elements.Add(add.Element);
                    break;
                case DeleteElementOperation delete:
                    BoardState.Elements.RemoveAll(e => e.Id == delete.ElementId);
                    break;
                case MoveElementOperation move:
                    {
                        var element = BoardState.Elements.FirstOrDefault(e => e.Id == move.ElementId);
                        if (element is null) return false;
                        element.Position = move.Position;
                        element.Size = move.Size;
                        element.Rotation = move.Rotation;
                        break;
                    }
                default:
                    return false;
            }

            return true;
        }

        public bool CanUndo => false;
        public bool CanRedo => false;
        public bool TryUndo() => false;
        public bool TryRedo() => false;

        public void Dispose()
        {
            IsRunning = false;
        }
    }

    private sealed class FakeGameClientSession(string displayName, bool delayConnectCompletion) : IGameClientSession
    {
        public List<BoardOperation> SentOperations { get; } = new();
        public int RequestFullSyncCalls { get; private set; }
        private readonly bool _delayConnectCompletion = delayConnectCompletion;
        private TaskCompletionSource<bool>? _connectCompletionSource;

        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler<ClientOperationReceivedEventArgs>? OperationReceived;

        public string DisplayName { get; } = displayName;
        public bool IsConnected { get; private set; }

        public void ConnectAsync(string hostAddress, int port = 7777)
        {
            if (_delayConnectCompletion)
            {
                _connectCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                return;
            }
        }

        public Task PendingConnectTask => _connectCompletionSource?.Task ?? Task.CompletedTask;

        public void RequestFullSync()
        {
            RequestFullSyncCalls++;
        }

        public void SendOperation(BoardOperation operation, bool reliable = true)
        {
            SentOperations.Add(operation);
        }

        public void PollEvents()
        {
        }

        public void Dispose()
        {
            IsConnected = false;
        }

        public void RaiseConnected()
        {
            IsConnected = true;
            Connected?.Invoke(this, EventArgs.Empty);
            _connectCompletionSource?.TrySetResult(true);
            _connectCompletionSource = null;
        }

        public void RaiseDisconnected()
        {
            IsConnected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseOperationReceived(BoardOperation operation)
        {
            OperationReceived?.Invoke(this, new ClientOperationReceivedEventArgs(operation));
        }
    }
}
