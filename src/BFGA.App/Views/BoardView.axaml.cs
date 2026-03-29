using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using BFGA.App.Infrastructure;
using BFGA.App.ViewModels;
using BFGA.Canvas.Rendering;
using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using System.ComponentModel;
using System.Windows.Input;

namespace BFGA.App.Views;

public partial class BoardView : UserControl, INotifyPropertyChanged
{
    public static readonly StyledProperty<BoardState?> BoardProperty =
        AvaloniaProperty.Register<BoardView, BoardState?>(nameof(Board));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteCursorState>?> RemoteCursorsProperty =
        AvaloniaProperty.Register<BoardView, IReadOnlyDictionary<Guid, RemoteCursorState>?>(nameof(RemoteCursors));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteStrokePreviewState>?> RemoteStrokePreviewsProperty =
        AvaloniaProperty.Register<BoardView, IReadOnlyDictionary<Guid, RemoteStrokePreviewState>?>(nameof(RemoteStrokePreviews));

    public static readonly StyledProperty<float> DotGridOpacityProperty =
        AvaloniaProperty.Register<BoardView, float>(nameof(DotGridOpacity), 0.1f);

    static BoardView()
    {
        DotGridOpacityProperty.Changed.AddClassHandler<BoardView>((bv, e) =>
        {
            bv.viewport.DotGridOpacity = (float)e.NewValue!;
        });
    }

    public BoardState? Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    public IReadOnlyDictionary<Guid, RemoteCursorState>? RemoteCursors
    {
        get => GetValue(RemoteCursorsProperty);
        set => SetValue(RemoteCursorsProperty, value);
    }

    public IReadOnlyDictionary<Guid, RemoteStrokePreviewState>? RemoteStrokePreviews
    {
        get => GetValue(RemoteStrokePreviewsProperty);
        set => SetValue(RemoteStrokePreviewsProperty, value);
    }

    public float DotGridOpacity
    {
        get => GetValue(DotGridOpacityProperty);
        set => SetValue(DotGridOpacityProperty, value);
    }

    public BoardView()
    {
        InitializeComponent();
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        ZoomResetCommand = new RelayCommand(ResetZoom);
        viewport.ZoomChanged += HandleZoomChanged;
        viewport.PointerPressed += HandlePointerPressed;
        viewport.PointerMoved += HandlePointerMoved;
        viewport.PointerReleased += HandlePointerReleased;
        DataContextChanged += HandleDataContextChanged;
        SyncToolController();
    }

    public void InvalidateBoard()
    {
        viewport.InvalidateBoard();
    }

    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ZoomResetCommand { get; }

    public string ZoomLabel => $"Zoom: {GetZoomPercent():0}%";

    private double _zoomFactor = 1.0;
    private bool _isUpdatingZoomLevel;

    public double ZoomLevel
    {
        get => _zoomFactor;
        set
        {
            if (_isUpdatingZoomLevel)
                return;

            SetZoomLevel(value);
        }
    }

    private void ZoomIn() => SetZoom(GetZoomFactor() * 1.1);
    private void ZoomOut() => SetZoom(GetZoomFactor() / 1.1);
    private void ResetZoom() => SetZoom(1.0);

    public void SetZoomLevel(double zoom) => SetZoom(zoom);

    private void SetZoom(double zoom)
    {
        var clampedZoom = Math.Clamp(zoom, 0.2, 3.0);
        _isUpdatingZoomLevel = true;
        _zoomFactor = clampedZoom;
        var centerX = viewport.Bounds.Width / 2.0;
        var centerY = viewport.Bounds.Height / 2.0;
        viewport.SetZoom(clampedZoom, centerX, centerY);

        NotifyPropertyChanged(nameof(ZoomLabel));
        NotifyPropertyChanged(nameof(ZoomLevel));
        _isUpdatingZoomLevel = false;
    }

    private void HandleZoomChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingZoomLevel)
            return;

        var clampedZoom = Math.Clamp(viewport.Zoom, 0.2, 3.0);
        _zoomFactor = clampedZoom;
        NotifyPropertyChanged(nameof(ZoomLevel));
        NotifyPropertyChanged(nameof(ZoomLabel));
    }

    private BoardScreenViewModel? _boardScreenViewModel;
    private BoardToolController? _toolController;
    private int _moveLogThrottleCounter;
    private bool _isPanning;
    private Point _lastPanPosition;

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (_boardScreenViewModel is not null)
        {
            _boardScreenViewModel.PropertyChanged -= HandleBoardScreenPropertyChanged;
        }

        _boardScreenViewModel = DataContext as BoardScreenViewModel;

        if (_boardScreenViewModel is not null)
        {
            _boardScreenViewModel.PropertyChanged += HandleBoardScreenPropertyChanged;
        }

        SyncToolController();
    }

    private void HandleBoardScreenPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BoardScreenViewModel.SelectedTool)
            or nameof(BoardScreenViewModel.SelectedStrokeColor)
            or nameof(BoardScreenViewModel.SelectedFillColor)
            or nameof(BoardScreenViewModel.StrokeWidth)
            or nameof(BoardScreenViewModel.Opacity))
        {
            SyncToolController();
        }
    }

    private void SyncToolController()
        => SyncToolController(logThisPhase: true);

    private void SyncToolController(bool logThisPhase)
    {
        var boardScreen = _boardScreenViewModel;
        if (boardScreen is null)
            return;

        var mainViewModel = boardScreen.MainViewModel;
        var activeBoard = mainViewModel.Board;

        _toolController ??= new BoardToolController(activeBoard);
        _toolController.SetBoard(activeBoard);
        _toolController.StrokeColor = boardScreen.SelectedStrokeColor;
        _toolController.FillColor = boardScreen.SelectedFillColor;
        _toolController.StrokeWidth = boardScreen.StrokeWidth;
        _toolController.Opacity = boardScreen.Opacity;

        if (_toolController.CurrentTool != boardScreen.SelectedTool)
            _toolController.SetTool(boardScreen.SelectedTool);
        if (logThisPhase)
        {
            mainViewModel.LogBoardDebug("sync-tool", () => $"selected={boardScreen.SelectedTool} controller={_toolController.CurrentTool} elements={activeBoard.Elements.Count} stroke={boardScreen.SelectedStrokeColor} fill={boardScreen.SelectedFillColor} width={boardScreen.StrokeWidth:0.##} opacity={boardScreen.Opacity:0.##}");
        }
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        StartPointerGesture();
        LogPointerEvent("pointer-pressed", e);

        var isHandTool = _toolController?.CurrentTool == BoardToolType.Hand;
        var isMiddleButton = e.GetCurrentPoint(viewport).Properties.IsMiddleButtonPressed;
        if (isHandTool || isMiddleButton)
        {
            _isPanning = true;
            _lastPanPosition = e.GetPosition(viewport);
            e.Pointer.Capture(viewport);
            e.Handled = true;
            return;
        }

        if (!TryHandlePointer(e, PointerPhase.Pressed))
            return;

        e.Handled = true;
        e.Pointer.Capture(viewport);
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured != viewport)
            return;

        if (_isPanning)
        {
            var currentPos = e.GetPosition(viewport);
            var delta = new System.Numerics.Vector2(
                (float)(currentPos.X - _lastPanPosition.X),
                (float)(currentPos.Y - _lastPanPosition.Y));
            viewport.PanBy(delta);
            _lastPanPosition = currentPos;
            e.Handled = true;
            return;
        }

        var shouldLog = ShouldLogMoveEvent();

        if (TryHandlePointer(e, PointerPhase.Moved, shouldLog))
            e.Handled = true;
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        LogPointerEvent("pointer-released", e);
        if (e.Pointer.Captured != viewport)
            return;

        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            StartPointerGesture();
            return;
        }

        try
        {
            if (TryHandlePointer(e, PointerPhase.Released))
                e.Handled = true;
        }
        finally
        {
            e.Pointer.Capture(null);
            StartPointerGesture();
        }
    }

    private bool TryHandlePointer(PointerEventArgs e, PointerPhase phase, bool logThisPhase = true)
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return false;

        try
        {
            SyncToolController(logThisPhase);

            var boardPoint = viewport.ScreenToBoard(e.GetPosition(viewport));
            var viewportPoint = e.GetPosition(viewport);
            var zoom = viewport.Zoom;
            var mainViewModel = _boardScreenViewModel.MainViewModel;
            if (logThisPhase)
            {
                mainViewModel.LogBoardDebug($"pointer-{phase.ToString().ToLowerInvariant()}", () => $"tool={_toolController.CurrentTool} zoom={zoom:0.00} viewport=({viewportPoint.X:0.##},{viewportPoint.Y:0.##}) board=({boardPoint.X:0.##},{boardPoint.Y:0.##})");
            }

            var result = phase switch
            {
                PointerPhase.Pressed => _toolController.HandlePointerDown(boardPoint, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control)),
                PointerPhase.Moved => _toolController.HandlePointerMove(boardPoint, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control)),
                PointerPhase.Released => _toolController.HandlePointerUp(boardPoint, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control)),
                _ => ToolResult.None
            };

            if (logThisPhase)
            {
                mainViewModel.LogBoardDebug($"tool-result-{phase.ToString().ToLowerInvariant()}", () => $"handled={result.Handled} boardChanged={result.BoardChanged} operations={result.Operations.Count}");
            }

            if (result.BoardChanged)
            {
                if (logThisPhase)
                {
                    mainViewModel.LogBoardDebug($"invalidate-{phase.ToString().ToLowerInvariant()}", () => "board-changed=true action=invalidate");
                }
                viewport.InvalidateBoard();
            }
            else
            {
                if (logThisPhase)
                {
                    mainViewModel.LogBoardDebug($"invalidate-{phase.ToString().ToLowerInvariant()}", () => "board-changed=false action=skip");
                }
            }

            if (result.HasOperations)
            {
                foreach (var operation in result.Operations)
                {
                    _boardScreenViewModel.MainViewModel.PublishLocalBoardOperation(operation);
                }

                if (_boardScreenViewModel.MainViewModel.Host is not null)
                {
                    _boardScreenViewModel.MainViewModel.SyncBoardFromHost();
                    _toolController.SetBoard(_boardScreenViewModel.MainViewModel.Board);
                    if (logThisPhase)
                    {
                        mainViewModel.LogBoardDebug($"publish-{phase.ToString().ToLowerInvariant()}", () => $"operations={result.Operations.Count} action=host-resync");
                    }
                }
                else
                {
                    if (logThisPhase)
                    {
                        mainViewModel.LogBoardDebug($"publish-{phase.ToString().ToLowerInvariant()}", () => $"operations={result.Operations.Count} action=published");
                    }
                }
            }

            if (!result.HasOperations)
            {
                if (logThisPhase)
                {
                    mainViewModel.LogBoardDebug($"publish-{phase.ToString().ToLowerInvariant()}", () => "operations=0 action=skip");
                }
            }

            return result.Handled;
        }
        catch (Exception ex)
        {
            _boardScreenViewModel.MainViewModel.LogBoardDebug($"pointer-error-{phase.ToString().ToLowerInvariant()}", () => ex.ToString());
            throw;
        }
    }

    private void LogPointerEvent(string eventName, PointerEventArgs e)
    {
        LogPointerEvent(eventName, () =>
        {
            var viewportPoint = e.GetPosition(viewport);
            var boardPoint = viewport.ScreenToBoard(viewportPoint);
            return $"zoom={viewport.Zoom:0.00} viewport=({viewportPoint.X:0.##},{viewportPoint.Y:0.##}) board=({boardPoint.X:0.##},{boardPoint.Y:0.##})";
        });
    }

    internal void LogPointerEvent(string eventName, Func<string> messageFactory)
    {
        if (_boardScreenViewModel is null)
        {
            return;
        }

        var mainViewModel = _boardScreenViewModel.MainViewModel;
        if (!mainViewModel.IsBoardDebugLoggingEnabled)
        {
            return;
        }

        mainViewModel.LogBoardDebug(eventName, messageFactory);
    }

    private bool ShouldLogMoveEvent()
    {
        var shouldLog = _moveLogThrottleCounter % 12 == 0;
        _moveLogThrottleCounter++;
        return shouldLog;
    }

    private void StartPointerGesture() => _moveLogThrottleCounter = 0;

    private enum PointerPhase
    {
        Pressed,
        Moved,
        Released
    }

    private double GetZoomFactor()
    {
        return _zoomFactor;
    }

    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }

    private PropertyChangedEventHandler? _propertyChanged;

    private void NotifyPropertyChanged(string propertyName)
        => _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private double GetZoomPercent() => GetZoomFactor() * 100.0;
}
