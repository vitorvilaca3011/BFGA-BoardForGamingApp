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

    public BoardView()
    {
        InitializeComponent();
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        ZoomResetCommand = new RelayCommand(ResetZoom);
        viewport.ZoomBorder.ZoomChanged += HandleZoomChanged;
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
        var zoomBorder = viewport.ZoomBorder;
        var centerX = viewport.Bounds.Width / 2.0;
        var centerY = viewport.Bounds.Height / 2.0;
        zoomBorder.ZoomTo(clampedZoom, centerX, centerY, true);

        NotifyPropertyChanged(nameof(ZoomLabel));
        NotifyPropertyChanged(nameof(ZoomLevel));
        _isUpdatingZoomLevel = false;
    }

    private void HandleZoomChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingZoomLevel)
            return;

        var zoomBorder = viewport.ZoomBorder;
        var clampedZoom = Math.Clamp(zoomBorder.ZoomX, 0.2, 3.0);
        _zoomFactor = clampedZoom;
        NotifyPropertyChanged(nameof(ZoomLevel));
        NotifyPropertyChanged(nameof(ZoomLabel));
    }

    private BoardScreenViewModel? _boardScreenViewModel;
    private BoardToolController? _toolController;

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
        if (e.PropertyName == nameof(BoardScreenViewModel.SelectedTool))
        {
            SyncToolController();
        }
    }

    private void SyncToolController()
    {
        var boardScreen = _boardScreenViewModel;
        if (boardScreen is null)
            return;

        var mainViewModel = boardScreen.MainViewModel;
        var activeBoard = mainViewModel.Board;

        _toolController ??= new BoardToolController(activeBoard);
        _toolController.SetBoard(activeBoard);

        switch (boardScreen.SelectedTool)
        {
            case BoardToolType.Rectangle:
                _toolController.SetTool(BoardToolType.Shape);
                _toolController.ShapeType = ShapeType.Rectangle;
                break;
            case BoardToolType.Ellipse:
                _toolController.SetTool(BoardToolType.Shape);
                _toolController.ShapeType = ShapeType.Ellipse;
                break;
            default:
                _toolController.SetTool(boardScreen.SelectedTool);
                break;
        }
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!TryHandlePointer(e, PointerPhase.Pressed))
            return;

        e.Handled = true;
        e.Pointer.Capture(viewport);
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Captured != viewport)
            return;

        if (TryHandlePointer(e, PointerPhase.Moved))
            e.Handled = true;
    }

    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured != viewport)
            return;

        try
        {
            if (TryHandlePointer(e, PointerPhase.Released))
                e.Handled = true;
        }
        finally
        {
            e.Pointer.Capture(null);
        }
    }

    private bool TryHandlePointer(PointerEventArgs e, PointerPhase phase)
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return false;

        SyncToolController();

        if (_toolController.CurrentTool == BoardToolType.Hand)
            return false;

        var boardPoint = viewport.CanvasPointToBoard(e.GetPosition(viewport.Canvas));
        var result = phase switch
        {
            PointerPhase.Pressed => _toolController.HandlePointerDown(boardPoint, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control)),
            PointerPhase.Moved => _toolController.HandlePointerMove(boardPoint, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control)),
            PointerPhase.Released => _toolController.HandlePointerUp(boardPoint, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control)),
            _ => ToolResult.None
        };

        if (result.BoardChanged)
        {
            viewport.InvalidateBoard();
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
            }
        }

        return result.Handled;
    }

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
