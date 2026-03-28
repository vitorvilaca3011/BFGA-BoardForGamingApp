using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using BFGA.App.Infrastructure;
using BFGA.App.Networking;
using BFGA.App.Services;
using BFGA.Canvas.Rendering;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network;
using BFGA.Network.Protocol;
using MessagePack;

namespace BFGA.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IFileDialogService? _fileDialogService;
    private readonly IGameSessionFactory _sessionFactory;
    private readonly Func<string> _documentsFolderProvider;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly AsyncRelayCommand _startHostCommand;
    private readonly AsyncRelayCommand _stopHostCommand;
    private readonly AsyncRelayCommand _connectCommand;
    private readonly AsyncRelayCommand _disconnectCommand;
    private readonly AsyncRelayCommand _loadBoardCommand;
    private readonly AsyncRelayCommand _saveBoardCommand;
    private readonly AsyncRelayCommand _setHostModeCommand;
    private readonly AsyncRelayCommand _setJoinModeCommand;
    private readonly AsyncRelayCommand _undoCommand;
    private readonly AsyncRelayCommand _redoCommand;
    private readonly ConnectionScreenViewModel _connectionScreen;
    private readonly BoardScreenViewModel _boardScreen;

    private ConnectionMode _selectedMode = ConnectionMode.Host;
    private ShellConnectionState _connectionState = ShellConnectionState.Disconnected;
    private string _displayName = string.IsNullOrWhiteSpace(Environment.UserName) ? "Player" : Environment.UserName;
    private string _hostAddress = "127.0.0.1";
    private int _hostPort = 7777;
    private BoardState _board = new();
    private IReadOnlyDictionary<Guid, PlayerInfo> _roster = new Dictionary<Guid, PlayerInfo>();
    private IReadOnlyDictionary<Guid, RemoteCursorState> _remoteCursors = new Dictionary<Guid, RemoteCursorState>();
    private IReadOnlyDictionary<Guid, RemoteStrokePreviewState> _remoteStrokePreviews = new Dictionary<Guid, RemoteStrokePreviewState>();
    private string _statusText = "Ready. Choose Host or Join.";
    private IGameHostSession? _host;
    private IGameClientSession? _client;
    private bool _isPolling;
    private bool _isAutosaving;
    private int _undoShadowCount;
    private int _redoShadowCount;
    private bool _isSettingsOpen;
    private float _gridOpacity = 0.1f;
    private DateTime? _joinStartedAt;
    private readonly TimeSpan _joinTimeout;
    private readonly TimeSpan _fullSyncTimeout;
    private Guid? _localClientId;
    private TaskCompletionSource<bool>? _pendingFullSyncRequest;

    public MainViewModel(
        IFileDialogService? fileDialogService = null,
        IGameSessionFactory? sessionFactory = null,
        TimeSpan? joinTimeout = null,
        TimeSpan? fullSyncTimeout = null,
        Func<string>? documentsFolderProvider = null)
    {
        _fileDialogService = fileDialogService;
        _sessionFactory = sessionFactory ?? new NetworkGameSessionFactory();
        _joinTimeout = joinTimeout ?? TimeSpan.FromSeconds(5);
        _fullSyncTimeout = fullSyncTimeout ?? TimeSpan.FromSeconds(5);
        _documentsFolderProvider = documentsFolderProvider ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        _startHostCommand = CreateShellCommand(StartHostAsync, CanStartHost, "Failed to start host: ");
        _stopHostCommand = CreateShellCommand(StopHostAsync, CanStopHost, "Failed to stop host: ");
        _connectCommand = CreateShellCommand(ConnectAsync, CanConnect, "Failed to connect: ");
        _disconnectCommand = CreateShellCommand(DisconnectAsync, CanDisconnect, "Failed to disconnect: ");
        _loadBoardCommand = CreateShellCommand(LoadBoardAsync, CanLoadBoardCommand, "Failed to load board: ");
        _saveBoardCommand = CreateShellCommand(SaveBoardAsync, CanSaveBoardCommand, "Failed to save board: ");
        _undoCommand = CreateShellCommand(UndoAsync, () => CanUndo, "Failed to undo: ");
        _redoCommand = CreateShellCommand(RedoAsync, () => CanRedo, "Failed to redo: ");
        _setHostModeCommand = new AsyncRelayCommand(() =>
        {
            SelectedMode = ConnectionMode.Host;
            return Task.CompletedTask;
        }, () => CanSwitchMode, ex => HandleShellError($"Failed to set host mode: {ex.Message}"));
        _setJoinModeCommand = new AsyncRelayCommand(() =>
        {
            SelectedMode = ConnectionMode.Join;
            return Task.CompletedTask;
        }, () => CanSwitchMode, ex => HandleShellError($"Failed to set join mode: {ex.Message}"));
        _connectionScreen = new ConnectionScreenViewModel(this);
        _boardScreen = new BoardScreenViewModel(this);

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _pollTimer.Tick += (_, _) => PollNetwork();

        _autosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _autosaveTimer.Tick += async (_, _) => await HostAutosaveAsync().ConfigureAwait(false);
    }

    public ConnectionMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!CanSwitchMode)
            {
                return;
            }

            if (!SetProperty(ref _selectedMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsHostMode));
            OnPropertyChanged(nameof(IsJoinMode));
            OnPropertyChanged(nameof(IsHostModeSelected));
            OnPropertyChanged(nameof(IsJoinModeSelected));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(PrimaryActionCommand));
            OnPropertyChanged(nameof(IsPrimaryButtonEnabled));
            RaiseCommandStates();
        }
    }

    public bool IsHostMode => SelectedMode == ConnectionMode.Host;

    public bool IsJoinMode => SelectedMode == ConnectionMode.Join;

    public bool IsHostModeSelected
    {
        get => IsHostMode;
        set
        {
            if (value)
            {
                SelectedMode = ConnectionMode.Host;
            }
        }
    }

    public bool IsJoinModeSelected
    {
        get => IsJoinMode;
        set
        {
            if (value)
            {
                SelectedMode = ConnectionMode.Join;
            }
        }
    }

    public ShellConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (!SetProperty(ref _connectionState, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsDisconnected));
            OnPropertyChanged(nameof(CanLoadBoard));
            OnPropertyChanged(nameof(CanSaveBoard));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(PrimaryActionCommand));
            OnPropertyChanged(nameof(IsPrimaryButtonEnabled));
            OnPropertyChanged(nameof(CurrentScreen));
            OnPropertyChanged(nameof(IsBoardScreen));
            RaiseCommandStates();
            UpdateStatusText();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value))
            {
                return;
            }

            RaiseCommandStates();
        }
    }

    public string HostAddress
    {
        get => _hostAddress;
        set => SetProperty(ref _hostAddress, value);
    }

    public int HostPort
    {
        get => _hostPort;
        set
        {
            if (SetProperty(ref _hostPort, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public BoardState Board
    {
        get => _board;
        private set => SetProperty(ref _board, value);
    }

    public IReadOnlyDictionary<Guid, PlayerInfo> Roster
    {
        get => _roster;
        private set => SetProperty(ref _roster, value);
    }

    public IReadOnlyDictionary<Guid, RemoteCursorState> RemoteCursors
    {
        get => _remoteCursors;
        private set => SetProperty(ref _remoteCursors, value);
    }

    public IReadOnlyDictionary<Guid, RemoteStrokePreviewState> RemoteStrokePreviews
    {
        get => _remoteStrokePreviews;
        private set => SetProperty(ref _remoteStrokePreviews, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public object CurrentScreen
    {
        get => ConnectionState is ShellConnectionState.Hosting or ShellConnectionState.Connected
            ? _boardScreen
            : _connectionScreen;
    }

    public bool IsDisconnected => ConnectionState == ShellConnectionState.Disconnected;

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public float GridOpacity
    {
        get => _gridOpacity;
        set => SetProperty(ref _gridOpacity, Math.Clamp(value, 0f, 0.3f));
    }

    public bool IsBoardScreen => CurrentScreen is BoardScreenViewModel;

    public string PrimaryButtonText => (SelectedMode, ConnectionState) switch
    {
        (ConnectionMode.Host, ShellConnectionState.Hosting) => "STOP HOST",
        (ConnectionMode.Host, ShellConnectionState.Disconnected) => "START HOST",
        (ConnectionMode.Join, ShellConnectionState.Joining) => "CONNECTING...",
        (ConnectionMode.Join, ShellConnectionState.Connected) => "DISCONNECT",
        (ConnectionMode.Join, ShellConnectionState.Disconnected) => "CONNECT",
        _ => "CONNECT",
    };

    public ICommand PrimaryActionCommand => (SelectedMode, ConnectionState) switch
    {
        (ConnectionMode.Host, ShellConnectionState.Hosting) => StopHostCommand,
        (ConnectionMode.Host, ShellConnectionState.Disconnected) => StartHostCommand,
        (ConnectionMode.Join, ShellConnectionState.Connected) => DisconnectCommand,
        _ => ConnectCommand,
    };

    public bool IsPrimaryButtonEnabled => ConnectionState != ShellConnectionState.Joining;

    public IGameHostSession? Host
    {
        get => _host;
        private set
        {
            if (_host == value)
            {
                return;
            }

            if (_host is not null)
            {
                _host.PeerJoined -= OnPeerJoined;
                _host.PeerLeft -= OnPeerLeft;
            }

            _host = value;

            if (_host is not null)
            {
                _host.PeerJoined += OnPeerJoined;
                _host.PeerLeft += OnPeerLeft;
            }

            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public IGameClientSession? Client
    {
        get => _client;
        private set
        {
            if (_client == value)
            {
                return;
            }

            if (_client is not null)
            {
                _client.Connected -= OnClientConnected;
                _client.Disconnected -= OnClientDisconnected;
                _client.OperationReceived -= OnClientOperationReceived;
            }

            _client = value;

            if (_client is not null)
            {
                _client.Connected += OnClientConnected;
                _client.Disconnected += OnClientDisconnected;
                _client.OperationReceived += OnClientOperationReceived;
            }

            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public bool CanSwitchMode => ConnectionState == ShellConnectionState.Disconnected && Host is null && Client is null;
    public bool CanLoadBoard => Host is not null || (ConnectionState == ShellConnectionState.Disconnected && SelectedMode == ConnectionMode.Host);
    public bool CanSaveBoard => Host is not null || Client is not null || (ConnectionState == ShellConnectionState.Disconnected && SelectedMode == ConnectionMode.Host);

    public ICommand StartHostCommand => _startHostCommand;
    public ICommand StopHostCommand => _stopHostCommand;
    public ICommand ConnectCommand => _connectCommand;
    public ICommand DisconnectCommand => _disconnectCommand;
    public ICommand LoadBoardCommand => _loadBoardCommand;
    public ICommand SaveBoardCommand => _saveBoardCommand;
    public ICommand SetHostModeCommand => _setHostModeCommand;
    public ICommand SetJoinModeCommand => _setJoinModeCommand;
    public AsyncRelayCommand UndoCommand => _undoCommand;
    public AsyncRelayCommand RedoCommand => _redoCommand;

    public bool CanUndo => Host is not null ? Host.CanUndo : _undoShadowCount > 0;
    public bool CanRedo => Host is not null ? Host.CanRedo : _redoShadowCount > 0;

    public bool IsAutosaveTimerEnabled => _autosaveTimer.IsEnabled;

    public string GetHostAutosavePathForTests() => GetHostAutosavePath(Host?.BoardState ?? Board);

    public void StartPolling()
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        _pollTimer.Start();
    }

    public void StopPolling()
    {
        if (!_isPolling)
        {
            return;
        }

        _isPolling = false;
        _pollTimer.Stop();
    }

    public void PollNetwork()
    {
        Host?.PollEvents();
        Client?.PollEvents();

        if (ConnectionState == ShellConnectionState.Joining && Client is not null)
        {
            if (Client.IsConnected)
            {
                _joinStartedAt = null;
            }
            else if (_joinStartedAt is not null && DateTime.UtcNow - _joinStartedAt >= _joinTimeout)
            {
                AbortJoinAttempt($"Failed to connect to {HostAddress}:{HostPort} within {_joinTimeout.TotalSeconds:0.#} seconds.");
            }
        }

        if (Host is null && Client is null && _isPolling)
        {
            StopPolling();
        }

        // In host mode, notify undo/redo state changes after each poll tick
        // (other players' operations can change what the host can undo)
        if (Host is not null)
        {
            NotifyUndoRedoChanged();
        }
    }

    public async Task StartHostAsync()
    {
        IGameHostSession? host = null;

        try
        {
            await DisconnectAsync().ConfigureAwait(true);

            host = _sessionFactory.CreateHost();
            host.ReplaceBoardState(Board);
            host.Start(HostPort);

            Host = host;
            SyncBoardFromHost();
            ConnectionState = ShellConnectionState.Hosting;
            StatusText = $"Hosting on port {host.Port}.";
            StartAutosave();
            StartPolling();
        }
        catch (Exception ex)
        {
            host?.Dispose();
            HandleShellError($"Failed to start host: {ex.Message}");
        }
    }

    public Task StopHostAsync()
    {
        StopAutosave();
        Host?.Dispose();
        Host = null;
        ResetCollaboratorState();

        if (ConnectionState == ShellConnectionState.Hosting)
        {
            ConnectionState = ShellConnectionState.Disconnected;
        }

        UpdateStatusText();
        return Task.CompletedTask;
    }

    public async Task ConnectAsync()
    {
        IGameClientSession? client = null;

        try
        {
            await StopHostAsync().ConfigureAwait(true);
            await DisconnectAsync().ConfigureAwait(true);

            client = _sessionFactory.CreateClient(DisplayName);
            Client = client;
            _joinStartedAt = DateTime.UtcNow;

            ConnectionState = ShellConnectionState.Joining;
            StatusText = $"Connecting to {HostAddress}:{HostPort} as {DisplayName}.";
            StartPolling();

            client.ConnectAsync(HostAddress, HostPort);
        }
        catch (Exception ex)
        {
            client?.Dispose();
            if (ReferenceEquals(Client, client))
            {
                Client = null;
            }
            HandleShellError($"Failed to connect: {ex.Message}");
        }
    }

    public Task DisconnectAsync()
    {
        ClearJoinAttempt();
        FailPendingFullSyncRequest(new InvalidOperationException("Disconnected before full sync arrived."));
        Client?.Dispose();
        Client = null;
        ResetCollaboratorState();

        if (ConnectionState is ShellConnectionState.Joining or ShellConnectionState.Connected)
        {
            ConnectionState = ShellConnectionState.Disconnected;
        }

        UpdateStatusText();
        return Task.CompletedTask;
    }

    public async Task CloseAsync()
    {
        try
        {
            await HostAutosaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            HandleShellError($"Failed to close board: {ex.Message}");
        }

        Dispose();
    }

    public async Task LoadBoardAsync()
    {
        if (!CanLoadBoard || _fileDialogService is null)
        {
            return;
        }

        try
        {
            var filePath = await _fileDialogService.OpenBoardPathAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var loadedBoard = await BoardFileStore.LoadAsync(filePath).ConfigureAwait(true);
            if (Host is not null)
            {
                Host.ReplaceBoardState(loadedBoard);
                Host.BroadcastFullSync();
                SyncBoardFromHost();
            }
            else
            {
                Board = loadedBoard;
            }

            StatusText = $"Loaded board from {Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            HandleShellError($"Failed to load board: {ex.Message}");
        }
    }

    public async Task SaveBoardAsync()
    {
        if (!CanSaveBoard || _fileDialogService is null)
        {
            return;
        }

        try
        {
            if (Client is not null)
            {
                await RequestFullSyncAndWaitAsync().ConfigureAwait(true);
            }

            var targetBoard = Host?.BoardState ?? Board;
            var suggestedFileName = string.IsNullOrWhiteSpace(targetBoard.BoardName)
                ? "board.bfga"
                : $"{SanitizeFileName(targetBoard.BoardName)}.bfga";

            var filePath = await _fileDialogService.SaveBoardPathAsync(suggestedFileName).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            await BoardFileStore.SaveAsync(targetBoard, filePath).ConfigureAwait(true);
            StatusText = $"Saved board to {Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            HandleShellError($"Failed to save board: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _boardScreen.Dispose();
        StopPolling();
        StopAutosave();
        Host?.Dispose();
        Client?.Dispose();
        Host = null;
        Client = null;
        ResetCollaboratorState();
    }

    private bool CanStartHost() => ConnectionState == ShellConnectionState.Disconnected && SelectedMode == ConnectionMode.Host;
    private bool CanStopHost() => Host is not null;
    private bool CanConnect() => ConnectionState == ShellConnectionState.Disconnected && SelectedMode == ConnectionMode.Join;
    private bool CanDisconnect() => Client is not null || ConnectionState is ShellConnectionState.Joining or ShellConnectionState.Connected;
    private bool CanLoadBoardCommand() => CanLoadBoard;
    private bool CanSaveBoardCommand() => CanSaveBoard;

    private void OnPeerJoined(object? sender, PeerJoinedEventArgs e)
    {
        UpsertRosterEntry(e.ClientId, new PlayerInfo(e.DisplayName, e.AssignedColor));
        StatusText = $"{e.DisplayName} joined the board.";
    }

    private void OnPeerLeft(object? sender, PeerLeftEventArgs e)
    {
        RemoveRosterEntry(e.ClientId);
        StatusText = $"Peer {e.ClientId} disconnected.";
    }

    private void OnClientConnected(object? sender, EventArgs e)
    {
        ClearJoinAttempt();
        ConnectionState = ShellConnectionState.Connected;
        StatusText = $"Connected as {DisplayName}.";
    }

    private void OnClientOperationReceived(object? sender, ClientOperationReceivedEventArgs e)
    {
        ApplyInboundOperation(e.Operation);

        if (e.Operation is FullSyncResponseOperation)
        {
            _pendingFullSyncRequest?.TrySetResult(true);
            _pendingFullSyncRequest = null;
        }

        if (ConnectionState == ShellConnectionState.Joining)
        {
            ClearJoinAttempt();
            ConnectionState = ShellConnectionState.Connected;
        }

        if (e.Operation is FullSyncResponseOperation || e.Operation is AddElementOperation or UpdateElementOperation or DeleteElementOperation or MoveElementOperation)
        {
            StatusText = $"Connected as {DisplayName}.";
        }
    }

    public Task DispatchLocalBoardOperation(BoardOperation operation)
    {
        if (Host is not null)
        {
            return PublishLocalBoardOperation(operation);
        }

        var targetBoard = Board;

        ApplyLocalBoardOperation(targetBoard, operation);

        Client?.SendOperation(operation);

        // Track shadow undo counter for client mode
        if (operation is AddElementOperation or UpdateElementOperation or DeleteElementOperation or MoveElementOperation)
        {
            _undoShadowCount++;
            _redoShadowCount = 0;
            NotifyUndoRedoChanged();
        }

        return Task.CompletedTask;
    }

    private Task UndoAsync()
    {
        if (Host is not null)
        {
            if (Host.TryUndo())
            {
                SyncBoardFromHost();
                NotifyUndoRedoChanged();
            }
        }
        else if (Client is not null && _undoShadowCount > 0)
        {
            Client.SendOperation(new UndoOperation());
            _undoShadowCount--;
            _redoShadowCount++;
            NotifyUndoRedoChanged();
        }

        return Task.CompletedTask;
    }

    private Task RedoAsync()
    {
        if (Host is not null)
        {
            if (Host.TryRedo())
            {
                SyncBoardFromHost();
                NotifyUndoRedoChanged();
            }
        }
        else if (Client is not null && _redoShadowCount > 0)
        {
            Client.SendOperation(new RedoOperation());
            _redoShadowCount--;
            _undoShadowCount++;
            NotifyUndoRedoChanged();
        }

        return Task.CompletedTask;
    }

    private void NotifyUndoRedoChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        _undoCommand.RaiseCanExecuteChanged();
        _redoCommand.RaiseCanExecuteChanged();
    }

    public Task PublishLocalBoardOperation(BoardOperation operation)
    {
        if (Host is not null)
        {
            if (Host.TryApplyLocalOperation(operation))
            {
                Host.BroadcastOperation(operation);
                SyncBoardFromHost();
                NotifyUndoRedoChanged();
            }
        }
        else
        {
            Client?.SendOperation(operation);
        }

        return Task.CompletedTask;
    }

    public void SyncBoardFromHost()
    {
        if (Host is null)
            return;

        Board = CloneBoardState(Host.BoardState);
    }

    private void OnClientDisconnected(object? sender, EventArgs e)
    {
        ClearJoinAttempt();
        FailPendingFullSyncRequest(new InvalidOperationException("Disconnected before full sync arrived."));
        if (Client is not null && !Client.IsConnected)
        {
            Client = null;
        }

        if (ConnectionState is ShellConnectionState.Joining or ShellConnectionState.Connected)
        {
            ConnectionState = ShellConnectionState.Disconnected;
        }

        UpdateStatusText();
    }

    private void RaiseCommandStates()
    {
        _startHostCommand.RaiseCanExecuteChanged();
        _stopHostCommand.RaiseCanExecuteChanged();
        _connectCommand.RaiseCanExecuteChanged();
        _disconnectCommand.RaiseCanExecuteChanged();
        _loadBoardCommand.RaiseCanExecuteChanged();
        _saveBoardCommand.RaiseCanExecuteChanged();
        _setHostModeCommand.RaiseCanExecuteChanged();
        _setJoinModeCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSwitchMode));
        OnPropertyChanged(nameof(CanLoadBoard));
        OnPropertyChanged(nameof(CanSaveBoard));
        NotifyUndoRedoChanged();
    }

    private void UpdateStatusText()
    {
        if (ConnectionState == ShellConnectionState.Hosting && Host is not null)
        {
            StatusText = $"Hosting on port {Host.Port}.";
        }
        else if (ConnectionState == ShellConnectionState.Disconnected)
        {
            StatusText = "Ready. Choose Host or Join.";
        }
    }

    private void HandleShellError(string message)
    {
        StatusText = message;
    }

    private void ApplyLocalBoardOperation(BoardState targetBoard, BoardOperation operation)
    {
        switch (operation)
        {
            case AddElementOperation add:
                ApplyAddElement(targetBoard, add.Element);
                break;
            case UpdateElementOperation update:
                ApplyUpdateElement(targetBoard, update);
                break;
            case DeleteElementOperation delete:
                ApplyDeleteElement(targetBoard, delete.ElementId);
                break;
            case MoveElementOperation move:
                ApplyMoveElement(targetBoard, move);
                break;
        }

        if (ReferenceEquals(targetBoard, Board))
        {
            Board = CloneBoardState(Board);
        }
    }

    private void ApplyInboundOperation(BoardOperation operation)
    {
        switch (operation)
        {
            case FullSyncResponseOperation sync:
                ApplyFullSync(sync);
                return;
            case PeerJoinedOperation joined:
                UpsertRosterEntry(joined.ClientId, new PlayerInfo(joined.DisplayName, joined.AssignedColor));
                return;
            case PeerLeftOperation left:
                RemoveRosterEntry(left.ClientId);
                return;
            case CursorUpdateOperation cursorUpdate:
                UpsertRemoteCursor(cursorUpdate);
                return;
            case DrawStrokePointOperation drawStrokePoint:
                UpsertRemoteStrokePreview(drawStrokePoint);
                return;
            case CancelStrokeOperation cancelStroke:
                RemoveRemoteStrokePreview(cancelStroke.StrokeId);
                return;
            case AddElementOperation addElement when addElement.Element is StrokeElement strokeElement:
                RemoveRemoteStrokePreview(strokeElement.Id);
                break;
        }

        ApplyLocalBoardOperation(Board, operation);
    }

    private void ApplyFullSync(FullSyncResponseOperation sync)
    {
        _localClientId = sync.ClientId == Guid.Empty ? null : sync.ClientId;
        Roster = new Dictionary<Guid, PlayerInfo>(sync.PlayerRoster);
        ReconcileRemoteState();

        if (sync.BoardState is not null)
        {
            Board = CloneBoardState(sync.BoardState);
        }

        // Reset shadow undo/redo counters after full sync
        _undoShadowCount = 0;
        _redoShadowCount = 0;
        NotifyUndoRedoChanged();
    }

    private static void ApplyAddElement(BoardState targetBoard, BoardElement element)
    {
        var existingIndex = targetBoard.Elements.FindIndex(boardElement => boardElement.Id == element.Id);
        if (existingIndex >= 0)
        {
            targetBoard.Elements[existingIndex] = element;
        }
        else
        {
            targetBoard.Elements.Add(element);
        }
    }

    private static void ApplyUpdateElement(BoardState targetBoard, UpdateElementOperation update)
    {
        var element = targetBoard.Elements.FirstOrDefault(boardElement => boardElement.Id == update.ElementId);
        if (element is null)
        {
            return;
        }

        foreach (var kvp in update.ModifiedProperties)
        {
            switch (kvp.Key)
            {
                case "Position" when kvp.Value is System.Numerics.Vector2 position:
                    element.Position = position;
                    break;
                case "Size" when kvp.Value is System.Numerics.Vector2 size:
                    element.Size = size;
                    break;
                case "Rotation" when kvp.Value is float rotation:
                    element.Rotation = rotation;
                    break;
                case "ZIndex" when kvp.Value is int zIndex:
                    element.ZIndex = zIndex;
                    break;
                case "IsLocked" when kvp.Value is bool isLocked:
                    element.IsLocked = isLocked;
                    break;
                case "Text" when element is TextElement text:
                    text.Text = kvp.Value?.ToString() ?? string.Empty;
                    break;
                case "FontSize" when element is TextElement text:
                    text.FontSize = Convert.ToSingle(kvp.Value);
                    break;
                case "Color" when element is TextElement text && kvp.Value is SkiaSharp.SKColor textColor:
                    text.Color = textColor;
                    break;
                case "FontFamily" when element is TextElement text:
                    text.FontFamily = kvp.Value?.ToString() ?? string.Empty;
                    break;
                case "StrokeColor" when element is ShapeElement shape && kvp.Value is SkiaSharp.SKColor strokeColor:
                    shape.StrokeColor = strokeColor;
                    break;
                case "FillColor" when element is ShapeElement shape && kvp.Value is SkiaSharp.SKColor fillColor:
                    shape.FillColor = fillColor;
                    break;
                case "StrokeWidth" when element is ShapeElement shape:
                    shape.StrokeWidth = Convert.ToSingle(kvp.Value);
                    break;
                case "Type" when element is ShapeElement shape && kvp.Value is ShapeType shapeType:
                    shape.Type = shapeType;
                    break;
            }
        }
    }

    private static void ApplyDeleteElement(BoardState targetBoard, Guid elementId)
    {
        var element = targetBoard.Elements.FirstOrDefault(boardElement => boardElement.Id == elementId);
        if (element is null)
        {
            return;
        }

        targetBoard.Elements.Remove(element);
    }

    private static void ApplyMoveElement(BoardState targetBoard, MoveElementOperation move)
    {
        var element = targetBoard.Elements.FirstOrDefault(boardElement => boardElement.Id == move.ElementId);
        if (element is null)
        {
            return;
        }

        element.Position = move.Position;
        element.Size = move.Size;
        element.Rotation = move.Rotation;
    }

    private static void CopyBoardState(BoardState source, BoardState target)
    {
        var clone = CloneBoardState(source);
        target.BoardId = clone.BoardId;
        target.BoardName = clone.BoardName;
        target.LastModified = clone.LastModified;
        target.Elements = clone.Elements;
    }

    private static BoardState CloneBoardState(BoardState source)
    {
        return MessagePackSerializer.Deserialize<BoardState>(
            MessagePackSerializer.Serialize(source, MessagePackSetup.Options),
            MessagePackSetup.Options);
    }

    private void UpsertRosterEntry(Guid clientId, PlayerInfo playerInfo)
    {
        var roster = new Dictionary<Guid, PlayerInfo>(Roster)
        {
            [clientId] = playerInfo
        };

        Roster = roster;
        ReconcileRemoteState();
    }

    private void RemoveRosterEntry(Guid clientId)
    {
        if (Roster.ContainsKey(clientId))
        {
            var roster = new Dictionary<Guid, PlayerInfo>(Roster);
            roster.Remove(clientId);
            Roster = roster;
        }

        RemoveRemoteCursor(clientId);
        RemoveRemoteStrokePreviews(clientId);
    }

    private void UpsertRemoteCursor(CursorUpdateOperation operation)
    {
        if (ShouldIgnoreLocalPresence(operation.SenderId))
        {
            return;
        }

        var playerInfo = GetPlayerInfo(operation.SenderId);
        var cursors = new Dictionary<Guid, RemoteCursorState>(RemoteCursors)
        {
            [operation.SenderId] = new RemoteCursorState(
                operation.SenderId,
                playerInfo?.DisplayName ?? $"Player {ShortClientId(operation.SenderId)}",
                playerInfo?.AssignedColor ?? SkiaSharp.SKColors.White,
                operation.Position)
        };

        RemoteCursors = cursors;
    }

    private void UpsertRemoteStrokePreview(DrawStrokePointOperation operation)
    {
        if (ShouldIgnoreLocalPresence(operation.SenderId))
        {
            return;
        }

        var playerInfo = GetPlayerInfo(operation.SenderId);
        var previews = new Dictionary<Guid, RemoteStrokePreviewState>(RemoteStrokePreviews);
        if (previews.TryGetValue(operation.StrokeId, out var existing))
        {
            var points = existing.Points as List<System.Numerics.Vector2> ?? new List<System.Numerics.Vector2>(existing.Points);
            if (points.Count == 0 || points[^1] != operation.Point)
            {
                points.Add(operation.Point);
            }

            previews[operation.StrokeId] = existing with { Points = points };
        }
        else
        {
            previews[operation.StrokeId] = new RemoteStrokePreviewState(
                operation.SenderId,
                operation.StrokeId,
                playerInfo?.DisplayName ?? $"Player {ShortClientId(operation.SenderId)}",
                playerInfo?.AssignedColor ?? SkiaSharp.SKColors.White,
                new List<System.Numerics.Vector2> { operation.Point });
        }

        RemoteStrokePreviews = previews;
    }

    private void RemoveRemoteStrokePreview(Guid strokeId)
    {
        if (!RemoteStrokePreviews.ContainsKey(strokeId))
        {
            return;
        }

        var previews = new Dictionary<Guid, RemoteStrokePreviewState>(RemoteStrokePreviews);
        previews.Remove(strokeId);
        RemoteStrokePreviews = previews;
    }

    private void RemoveRemoteCursor(Guid clientId)
    {
        if (!RemoteCursors.ContainsKey(clientId))
        {
            return;
        }

        var cursors = new Dictionary<Guid, RemoteCursorState>(RemoteCursors);
        cursors.Remove(clientId);
        RemoteCursors = cursors;
    }

    private void RemoveRemoteStrokePreviews(Guid clientId)
    {
        if (RemoteStrokePreviews.Count == 0)
        {
            return;
        }

        var previews = RemoteStrokePreviews.Where(entry => entry.Value.ClientId != clientId).ToDictionary(entry => entry.Key, entry => entry.Value);
        if (previews.Count != RemoteStrokePreviews.Count)
        {
            RemoteStrokePreviews = previews;
        }
    }

    private void ReconcileRemoteState()
    {
        if (Roster.Count == 0)
        {
            RemoteCursors = new Dictionary<Guid, RemoteCursorState>();
            RemoteStrokePreviews = new Dictionary<Guid, RemoteStrokePreviewState>();
            return;
        }

        var validIds = Roster.Keys.ToHashSet();
        if (_localClientId.HasValue)
        {
            validIds.Remove(_localClientId.Value);
        }

        RemoteCursors = RebuildRemoteCursors(validIds);
        RemoteStrokePreviews = RebuildRemoteStrokePreviews(validIds);
    }

    private Dictionary<Guid, RemoteCursorState> RebuildRemoteCursors(HashSet<Guid> validIds)
    {
        var cursors = new Dictionary<Guid, RemoteCursorState>();

        foreach (var entry in RemoteCursors)
        {
            if (!validIds.Contains(entry.Key))
            {
                continue;
            }

            var playerInfo = GetPlayerInfo(entry.Key);
            cursors[entry.Key] = new RemoteCursorState(
                entry.Key,
                playerInfo?.DisplayName ?? entry.Value.DisplayName,
                playerInfo?.AssignedColor ?? entry.Value.AssignedColor,
                entry.Value.Position);
        }

        return cursors;
    }

    private Dictionary<Guid, RemoteStrokePreviewState> RebuildRemoteStrokePreviews(HashSet<Guid> validIds)
    {
        var previews = new Dictionary<Guid, RemoteStrokePreviewState>();

        foreach (var entry in RemoteStrokePreviews)
        {
            if (!validIds.Contains(entry.Value.ClientId))
            {
                continue;
            }

            var playerInfo = GetPlayerInfo(entry.Value.ClientId);
            previews[entry.Key] = new RemoteStrokePreviewState(
                entry.Value.ClientId,
                entry.Value.StrokeId,
                playerInfo?.DisplayName ?? entry.Value.DisplayName,
                playerInfo?.AssignedColor ?? entry.Value.AssignedColor,
                entry.Value.Points);
        }

        return previews;
    }

    private PlayerInfo? GetPlayerInfo(Guid clientId) => Roster.TryGetValue(clientId, out var info) ? info : null;

    private bool ShouldIgnoreLocalPresence(Guid clientId) => _localClientId.HasValue && _localClientId.Value == clientId;

    private void ResetCollaboratorState()
    {
        _localClientId = null;
        Roster = new Dictionary<Guid, PlayerInfo>();
        RemoteCursors = new Dictionary<Guid, RemoteCursorState>();
        RemoteStrokePreviews = new Dictionary<Guid, RemoteStrokePreviewState>();
    }

    private void StartAutosave()
    {
        if (!_autosaveTimer.IsEnabled)
        {
            _autosaveTimer.Start();
        }
    }

    private void StopAutosave()
    {
        if (_autosaveTimer.IsEnabled)
        {
            _autosaveTimer.Stop();
        }
    }

    private async Task HostAutosaveAsync()
    {
        if (_isAutosaving || Host is null)
        {
            return;
        }

        _isAutosaving = true;
        try
        {
            await SaveHostBoardToDocumentsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            HandleShellError($"Failed to autosave board: {ex.Message}");
        }
        finally
        {
            _isAutosaving = false;
        }
    }

    private async Task SaveHostBoardToDocumentsAsync()
    {
        var host = Host;
        if (host is null)
        {
            return;
        }

        var board = host.BoardState;
        await BoardFileStore.SaveAsync(board, GetHostAutosavePath(board)).ConfigureAwait(false);
    }

    private string GetHostAutosavePath(BoardState board)
    {
        var fileName = string.IsNullOrWhiteSpace(board.BoardName)
            ? "board.bfga"
            : $"{SanitizeFileName(board.BoardName)}.bfga";

        var folder = Path.Combine(_documentsFolderProvider(), "BFGA");
        return Path.Combine(folder, fileName);
    }

    private async Task RequestFullSyncAndWaitAsync()
    {
        if (Client is null)
        {
            return;
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingFullSyncRequest = completion;
        Client.RequestFullSync();

        try
        {
            if (await Task.WhenAny(completion.Task, Task.Delay(_fullSyncTimeout)).ConfigureAwait(true) != completion.Task)
            {
                throw new TimeoutException($"Timed out waiting for full sync after {_fullSyncTimeout.TotalSeconds:0.#} seconds.");
            }

            await completion.Task.ConfigureAwait(true);
        }
        finally
        {
            if (ReferenceEquals(_pendingFullSyncRequest, completion))
            {
                _pendingFullSyncRequest = null;
            }
        }
    }

    private void FailPendingFullSyncRequest(Exception exception)
    {
        _pendingFullSyncRequest?.TrySetException(exception);
        _pendingFullSyncRequest = null;
    }

    private static string ShortClientId(Guid clientId) => clientId.ToString("N")[..8];

    private void AbortJoinAttempt(string message)
    {
        ClearJoinAttempt();
        Client?.Dispose();
        Client = null;
        ConnectionState = ShellConnectionState.Disconnected;
        StatusText = message;
    }

    private void ClearJoinAttempt()
    {
        _joinStartedAt = null;
    }

    private AsyncRelayCommand CreateShellCommand(Func<Task> execute, Func<bool>? canExecute, string errorPrefix)
    {
        return new AsyncRelayCommand(execute, canExecute, ex => HandleShellError($"{errorPrefix}{ex.Message}"));
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(fileName) ? "board" : fileName.Trim();
    }
}
