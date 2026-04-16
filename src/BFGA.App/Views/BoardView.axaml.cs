using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using BFGA.App.Infrastructure;
using BFGA.App.ViewModels;
using BFGA.Canvas.Rendering;
using BFGA.Canvas.Tools;
using BFGA.Core;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using System.ComponentModel;
using System.Numerics;
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

    public static readonly StyledProperty<EraserPreviewState?> EraserPreviewProperty =
        AvaloniaProperty.Register<BoardView, EraserPreviewState?>(nameof(EraserPreview));

    public static readonly StyledProperty<IReadOnlyDictionary<Guid, RemoteLaserState>?> RemoteLasersProperty =
        AvaloniaProperty.Register<BoardView, IReadOnlyDictionary<Guid, RemoteLaserState>?>(nameof(RemoteLasers));

    public static readonly StyledProperty<LocalLaserState?> LocalLaserProperty =
        AvaloniaProperty.Register<BoardView, LocalLaserState?>(nameof(LocalLaser));

    public static readonly StyledProperty<PingMarkerState?> LocalPingProperty =
        AvaloniaProperty.Register<BoardView, PingMarkerState?>(nameof(LocalPing));

    static BoardView()
    {
        DotGridOpacityProperty.Changed.AddClassHandler<BoardView>((bv, e) =>
        {
            bv.viewport.DotGridOpacity = (float)e.NewValue!;
        });

        BoardProperty.Changed.AddClassHandler<BoardView>((bv, _) =>
        {
            bv.HandleBoardChanged();
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

    public EraserPreviewState? EraserPreview
    {
        get => GetValue(EraserPreviewProperty);
        set => SetValue(EraserPreviewProperty, value);
    }

    public IReadOnlyDictionary<Guid, RemoteLaserState>? RemoteLasers
    {
        get => GetValue(RemoteLasersProperty);
        set => SetValue(RemoteLasersProperty, value);
    }

    public LocalLaserState? LocalLaser
    {
        get => GetValue(LocalLaserProperty);
        set => SetValue(LocalLaserProperty, value);
    }

    public PingMarkerState? LocalPing
    {
        get => GetValue(LocalPingProperty);
        set => SetValue(LocalPingProperty, value);
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
        viewport.PointerExited += HandlePointerExited;
        viewport.PointerCaptureLost += HandlePointerCaptureLost;
        DataContextChanged += HandleDataContextChanged;
        InlineTextEditor.KeyDown += HandleInlineTextEditorKeyDown;
        InlineTextEditor.LostFocus += HandleInlineTextEditorLostFocus;
        InlineTextEditor.TextChanged += HandleInlineTextEditorTextChanged;
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
    private PointerPhase _currentPointerPhase;
    private long _laserPressStartTimestampMs;
    private Point _laserPressStartScreenPoint;
    private bool _localLaserCanceled;
    private System.Numerics.Vector2? _inlineTextEditPosition;
    private Guid? _inlineTextEditElementId;
    private string? _inlineTextEditOriginalText;
    private bool _isCommittingInlineText;

    private void HandleDataContextChanged(object? sender, EventArgs e)
    {
        if (_boardScreenViewModel is not null)
        {
            _boardScreenViewModel.PropertyChanged -= HandleBoardScreenPropertyChanged;
            _boardScreenViewModel.ImageImportRequested -= HandleImageImportRequested;
        }

        _boardScreenViewModel = DataContext as BoardScreenViewModel;

        if (_boardScreenViewModel is not null)
        {
            _boardScreenViewModel.PropertyChanged += HandleBoardScreenPropertyChanged;
            _boardScreenViewModel.ImageImportRequested += HandleImageImportRequested;
        }

        SyncToolController();
    }

    private void HandleBoardScreenPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BoardScreenViewModel.SelectedTool)
            or nameof(BoardScreenViewModel.SelectedStrokeColor)
            or nameof(BoardScreenViewModel.SelectedFillColor)
            or nameof(BoardScreenViewModel.StrokeWidth)
            or nameof(BoardScreenViewModel.Opacity)
            or nameof(BoardScreenViewModel.FontSize)
            or nameof(BoardScreenViewModel.FontFamily))
        {
            SyncToolController();
        }

        if (_boardScreenViewModel?.IsEditingText == true
            && e.PropertyName is nameof(BoardScreenViewModel.FontSize)
                or nameof(BoardScreenViewModel.FontFamily)
                or nameof(BoardScreenViewModel.SelectedStrokeColor)
                or nameof(BoardScreenViewModel.Opacity))
        {
            UpdateInlineTextEditorVisuals();
        }

        if (e.PropertyName is nameof(BoardScreenViewModel.SelectedTool))
        {
            SyncEraserPreview(null, false);
            if (_boardScreenViewModel?.SelectedTool != BoardToolType.LaserPointer)
                CancelLocalLaser();

            UpdateCursorForTool();
        }

        if (e.PropertyName is nameof(BoardScreenViewModel.SelectedStrokeColor)
            or nameof(BoardScreenViewModel.Opacity)
            or nameof(BoardScreenViewModel.FontSize)
            or nameof(BoardScreenViewModel.FontFamily))
        {
            UpdateSelectedTextPropertiesFromPanel();
        }
    }

    private void UpdateCursorForTool()
    {
        try
        {
            var tool = _boardScreenViewModel?.SelectedTool ?? BoardToolType.Select;
            viewport.Cursor = BoardCursorFactory.Create(tool);
        }
        catch (InvalidOperationException)
        {
            // ICursorFactory not available (e.g. headless test environment)
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

        SyncSelectionOverlay();

        if (logThisPhase)
        {
            mainViewModel.LogBoardDebug("sync-tool", () => $"selected={boardScreen.SelectedTool} controller={_toolController.CurrentTool} elements={activeBoard.Elements.Count} stroke={boardScreen.SelectedStrokeColor} fill={boardScreen.SelectedFillColor} width={boardScreen.StrokeWidth:0.##} opacity={boardScreen.Opacity:0.##}");
        }
    }

    private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        StartPointerGesture();
        LogPointerEvent("pointer-pressed", e);

        if (_boardScreenViewModel is not null && _boardScreenViewModel.IsEditingText)
        {
            CommitInlineText();
            return;
        }

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

        if (_toolController?.CurrentTool == BoardToolType.Text)
        {
            var boardPoint = viewport.ScreenToBoard(e.GetPosition(viewport));
            if (!TryStartInlineTextEditExistingText(boardPoint))
                StartInlineTextEditing(boardPoint);
            e.Handled = true;
            return;
        }

        if (_toolController?.CurrentTool == BoardToolType.LaserPointer)
        {
            var screenPoint = e.GetPosition(viewport);
            BeginLocalLaser(viewport.ScreenToBoard(screenPoint), screenPoint, Environment.TickCount64);
            e.Pointer.Capture(viewport);
            e.Handled = true;
            return;
        }

        var clickCount = e.GetCurrentPoint(viewport).Properties.PointerUpdateKind == Avalonia.Input.PointerUpdateKind.LeftButtonPressed
            ? e.ClickCount : 1;
        if (clickCount >= 2 && _toolController?.CurrentTool == BoardToolType.Select)
        {
            var boardPoint = viewport.ScreenToBoard(e.GetPosition(viewport));
            if (TryStartInlineTextEditExistingText(boardPoint))
            {
                e.Handled = true;
                return;
            }
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

        if (_toolController?.CurrentTool == BoardToolType.LaserPointer)
        {
            UpdateLocalLaser(viewport.ScreenToBoard(e.GetPosition(viewport)), Environment.TickCount64);
            e.Handled = true;
            return;
        }

        SyncEraserPreview(viewport.ScreenToBoard(e.GetPosition(viewport)), true);

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
            if (_toolController?.CurrentTool == BoardToolType.LaserPointer)
            {
                var screenPoint = e.GetPosition(viewport);
                if (!_localLaserCanceled)
                    CompleteLocalLaser(viewport.ScreenToBoard(screenPoint), screenPoint, Environment.TickCount64);

                e.Handled = true;
                return;
            }

            SyncEraserPreview(viewport.ScreenToBoard(e.GetPosition(viewport)), true);
            if (TryHandlePointer(e, PointerPhase.Released))
                e.Handled = true;
        }
        finally
        {
            e.Pointer.Capture(null);
            SyncEraserPreview(null, false);
            StartPointerGesture();
        }
    }

    private void HandleBoardChanged()
    {
        if (_toolController is null)
            return;

        var board = Board;
        if (board is null)
        {
            viewport.SelectionOverlay = null;
            return;
        }

        _toolController.SetBoard(board);
        SyncSelectionOverlay();
    }

    private void HandlePointerExited(object? sender, PointerEventArgs e)
    {
        SyncEraserPreview(null, false);

        if (_toolController?.CurrentTool == BoardToolType.LaserPointer
            && e.Pointer.Captured == viewport)
        {
            CancelLocalLaser();
            e.Pointer.Capture(null);
        }
    }

    private void HandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CancelLocalLaser();
    }

    private void BeginLocalLaser(Vector2 boardPoint, Point screenPoint, long timestampMs)
    {
        var color = _boardScreenViewModel?.SelectedStrokeColor ?? SkiaSharp.SKColors.White;
        var laser = new LocalLaserState(color)
        {
            IsActive = true,
            HeadPosition = boardPoint,
            LastUpdateMs = timestampMs
        };
        laser.Trail.Add(boardPoint, timestampMs);
        LocalLaser = laser;
        _laserPressStartTimestampMs = timestampMs;
        _laserPressStartScreenPoint = screenPoint;
        _localLaserCanceled = false;
        LocalPing = null;
        _boardScreenViewModel!.MainViewModel.PublishLocalBoardOperation(new LaserPointerOperation(Guid.Empty, boardPoint, true));
    }

    private void UpdateLocalLaser(Vector2 boardPoint, long timestampMs)
    {
        if (LocalLaser is null || !LocalLaser.IsActive)
            return;

        if (!UpdateLocalLaserState(boardPoint, timestampMs))
            return;

        _boardScreenViewModel!.MainViewModel.PublishLocalBoardOperation(new LaserPointerOperation(Guid.Empty, boardPoint, true));
    }

    private void CompleteLocalLaser(Vector2 boardPoint, Point screenPoint, long timestampMs)
    {
        if (LocalLaser is null || !LocalLaser.IsActive)
            return;

        UpdateLocalLaserState(boardPoint, timestampMs);

        var elapsed = timestampMs - _laserPressStartTimestampMs;
        var dx = screenPoint.X - _laserPressStartScreenPoint.X;
        var dy = screenPoint.Y - _laserPressStartScreenPoint.Y;
        var movement = Math.Sqrt(dx * dx + dy * dy);

        if (elapsed < 200 && movement < 5)
            LocalPing = new PingMarkerState(boardPoint, timestampMs, LocalLaser.Color);

        LocalLaser.IsActive = false;
        LocalLaser.LastUpdateMs = timestampMs;
        _boardScreenViewModel!.MainViewModel.PublishLocalBoardOperation(new LaserPointerOperation(Guid.Empty, boardPoint, false));
    }

    private void CancelLocalLaser()
    {
        if (LocalLaser is null)
            return;

        _localLaserCanceled = true;
        LocalLaser.IsActive = false;
        LocalLaser.LastUpdateMs = Environment.TickCount64;
        _boardScreenViewModel!.MainViewModel.PublishLocalBoardOperation(new LaserPointerOperation(Guid.Empty, LocalLaser.HeadPosition, false));
    }

    private bool UpdateLocalLaserState(Vector2 boardPoint, long timestampMs)
    {
        if (LocalLaser is null)
            return false;

        LocalLaser.HeadPosition = boardPoint;
        LocalLaser.LastUpdateMs = timestampMs;

        var points = LocalLaser.Trail.GetPoints();
        if (points.Length != 0 && points[^1].Position == boardPoint)
            return false;

        LocalLaser.Trail.Add(boardPoint, timestampMs);
        return true;
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

            _currentPointerPhase = phase;

            ApplyToolResult(result, phase.ToString().ToLowerInvariant(), logThisPhase);

            return result.Handled;
        }
        catch (Exception ex)
        {
            _boardScreenViewModel.MainViewModel.LogBoardDebug($"pointer-error-{phase.ToString().ToLowerInvariant()}", () => ex.ToString());
            throw;
        }
    }

    public void DeleteSelection()
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        SyncToolController();
        var result = _toolController.DeleteSelectedElements();
        ApplyToolResult(result, "delete-selection", logThisPhase: true);
    }

    private void StartInlineTextEditing(System.Numerics.Vector2 boardPosition)
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        _inlineTextEditPosition = boardPosition;
        _inlineTextEditElementId = null;
        _inlineTextEditOriginalText = null;

        var screenPoint = viewport.BoardToScreen(boardPosition);
        var zoom = viewport.Zoom;

        InlineTextEditor.Text = string.Empty;
        InlineTextEditor.FontSize = _boardScreenViewModel.FontSize * zoom;
        InlineTextEditor.FontFamily = new FontFamily(_boardScreenViewModel.FontFamily);
        InlineTextEditor.Foreground = new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(255 * _boardScreenViewModel.Opacity),
                _boardScreenViewModel.SelectedStrokeColor.Red,
                _boardScreenViewModel.SelectedStrokeColor.Green,
                _boardScreenViewModel.SelectedStrokeColor.Blue));

        PositionInlineTextEditor(screenPoint);
        InlineTextEditor.IsVisible = true;
        InlineTextEditor.Focus();
        InlineTextEditor.SelectAll();

        _boardScreenViewModel.IsEditingText = true;
        _boardScreenViewModel.EditingTextElementId = null;
    }

    private bool TryStartInlineTextEditExistingText(System.Numerics.Vector2 boardPoint)
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return false;

        var hit = HitTestHelper.GetTopmostHit(_toolController.Board, boardPoint);
        if (hit is not TextElement textElement)
            return false;

        _inlineTextEditPosition = textElement.Position;
        _inlineTextEditElementId = textElement.Id;
        _inlineTextEditOriginalText = textElement.Text;

        var screenPoint = viewport.BoardToScreen(textElement.Position);
        var zoom = viewport.Zoom;

        InlineTextEditor.Text = textElement.Text;
        InlineTextEditor.FontSize = textElement.FontSize * zoom;
        var textColor = textElement.Color;
        InlineTextEditor.FontFamily = new FontFamily(textElement.FontFamily);
        InlineTextEditor.Foreground = new SolidColorBrush(Color.FromArgb(textColor.Alpha, textColor.Red, textColor.Green, textColor.Blue));

        PositionInlineTextEditor(screenPoint);
        InlineTextEditor.IsVisible = true;
        InlineTextEditor.Focus();
        InlineTextEditor.SelectAll();

        if (_boardScreenViewModel is not null)
        {
            _boardScreenViewModel.SelectedTool = BoardToolType.Text;
            _boardScreenViewModel.FontSize = textElement.FontSize;
            _boardScreenViewModel.FontFamily = textElement.FontFamily;
            _boardScreenViewModel.SelectedStrokeColor = textColor;
            _boardScreenViewModel.Opacity = textColor.Alpha / 255f;
        }

        _boardScreenViewModel.IsEditingText = true;
        _boardScreenViewModel.EditingTextElementId = textElement.Id;

        _toolController.Selection.Select(textElement.Id);
        SyncSelectionOverlay();
        viewport.InvalidateBoard();

        return true;
    }

    private void CommitInlineText()
    {
        if (_boardScreenViewModel is null || _toolController is null || _isCommittingInlineText)
            return;

        var text = InlineTextEditor.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            CancelInlineText();
            return;
        }

        _isCommittingInlineText = true;

        try
        {
            var mainViewModel = _boardScreenViewModel.MainViewModel;
            var baseColor = _boardScreenViewModel.SelectedStrokeColor;
            var color = new SkiaSharp.SKColor(baseColor.Red, baseColor.Green, baseColor.Blue, (byte)Math.Round(255 * _boardScreenViewModel.Opacity));

            if (_inlineTextEditElementId.HasValue && _inlineTextEditPosition.HasValue)
            {
                var element = _toolController.Board.Elements.FirstOrDefault(e => e.Id == _inlineTextEditElementId.Value);
                if (element is TextElement textElement)
                {
                    textElement.Text = text;
                    textElement.FontSize = _boardScreenViewModel.FontSize;
                    textElement.FontFamily = _boardScreenViewModel.FontFamily;
                    textElement.Color = color;
                    textElement.Size = MeasureTextSize(textElement.Text, textElement.FontSize, textElement.FontFamily);
                    viewport.InvalidateBoard();
                    mainViewModel.PublishLocalBoardOperation(
                        new UpdateElementOperation(textElement.Id, new Dictionary<string, object>
                        {
                            ["Text"] = textElement.Text,
                            ["FontSize"] = textElement.FontSize,
                            ["FontFamily"] = textElement.FontFamily,
                            ["Color"] = textElement.Color
                        }));
                    if (mainViewModel.Host is not null)
                    {
                        mainViewModel.SyncBoardFromHost();
                        _toolController.SetBoard(mainViewModel.Board);
                    }
                }
            }
            else if (_inlineTextEditPosition.HasValue)
            {
                var position = _inlineTextEditPosition.Value;
                var textElement = _toolController.PlaceText(text, position, color, _boardScreenViewModel.FontSize, _boardScreenViewModel.FontFamily);
                _toolController.Selection.Select(textElement.Id);
                var result = new ToolResult(true, true, [new AddElementOperation(textElement)]);
                ApplyToolResult(result, "place-text", logThisPhase: true);
            }
        }
        finally
        {
            _isCommittingInlineText = false;
            HideInlineTextEditor();

            if (_boardScreenViewModel is not null)
            {
                _boardScreenViewModel.IsEditingText = false;
                _boardScreenViewModel.EditingTextElementId = null;
                _boardScreenViewModel.SelectedTool = BoardToolType.Select;
            }

            _inlineTextEditPosition = null;
            _inlineTextEditElementId = null;
            _inlineTextEditOriginalText = null;
        }
    }

    private void CancelInlineText()
    {
        HideInlineTextEditor();

        if (_boardScreenViewModel is not null)
        {
            _boardScreenViewModel.IsEditingText = false;
            _boardScreenViewModel.EditingTextElementId = null;
        }

        _inlineTextEditPosition = null;
        _inlineTextEditElementId = null;
        _inlineTextEditOriginalText = null;
    }

    private void HideInlineTextEditor()
    {
        InlineTextEditor.IsVisible = false;
        InlineTextEditor.Text = string.Empty;
        viewport.Focus();
    }

    private void PositionInlineTextEditor(Point screenPoint)
    {
        UpdateInlineTextEditorVisuals();
        Avalonia.Controls.Canvas.SetLeft(InlineTextEditor, screenPoint.X);
        Avalonia.Controls.Canvas.SetTop(InlineTextEditor, screenPoint.Y);
    }

    private void HandleInlineTextEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateInlineTextEditorVisuals();
    }

    private void UpdateInlineTextEditorVisuals()
    {
        if (_boardScreenViewModel is null)
            return;

        var zoom = viewport.Zoom;
        var fontSize = _boardScreenViewModel.FontSize * zoom;
        var text = InlineTextEditor.Text ?? string.Empty;
        var typeface = new Typeface(_boardScreenViewModel.FontFamily);

        var contentWidth = 40d;
        var contentHeight = 28d;

        try
        {
            using var layout = new TextLayout(
                string.IsNullOrEmpty(text) ? "W" : text,
                typeface,
                fontSize,
                Brushes.White,
                textWrapping: TextWrapping.Wrap,
                maxWidth: double.PositiveInfinity);

            contentWidth = layout.WidthIncludingTrailingWhitespace + layout.OverhangLeading + layout.OverhangTrailing;
            contentHeight = layout.Extent;

            if (text.Contains('\n'))
            {
                var longestLine = text.Split('\n').MaxBy(line => line.Length) ?? string.Empty;
                contentWidth = Math.Max(contentWidth, longestLine.Length * fontSize * 0.6d);
                contentHeight = Math.Max(contentHeight, layout.TextLines.Count * fontSize * 1.4d);
            }
        }
        catch (InvalidOperationException)
        {
            var fallbackText = string.IsNullOrEmpty(text) ? "W" : text;
            var lineCount = Math.Max(1, fallbackText.Split('\n').Length);
            var longestLine = fallbackText.Split('\n').MaxBy(line => line.Length) ?? fallbackText;
            contentWidth = Math.Max(40d, longestLine.Length * fontSize * 0.6d);
            contentHeight = Math.Max(28d, lineCount * fontSize * 1.4d);
        }

        var width = Math.Max(40d, contentWidth + 8d);
        var height = Math.Max(28d, contentHeight + 8d);

        InlineTextEditor.FontSize = fontSize;
        InlineTextEditor.FontFamily = typeface.FontFamily;
        InlineTextEditor.Width = width;
        InlineTextEditor.Height = height;
        InlineTextEditor.Foreground = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(255 * _boardScreenViewModel.Opacity),
            _boardScreenViewModel.SelectedStrokeColor.Red,
            _boardScreenViewModel.SelectedStrokeColor.Green,
            _boardScreenViewModel.SelectedStrokeColor.Blue));
    }

    private static Vector2 MeasureTextSize(string text, float fontSize, string fontFamily)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Vector2.Zero;

        try
        {
            using var layout = new TextLayout(
                text,
                new Typeface(fontFamily),
                fontSize,
                Brushes.White,
                maxWidth: double.PositiveInfinity);

            return new Vector2(
                (float)(layout.WidthIncludingTrailingWhitespace + layout.OverhangLeading + layout.OverhangTrailing),
                (float)layout.Extent);
        }
        catch (InvalidOperationException)
        {
            return new Vector2(text.Length * fontSize * 0.6f, fontSize * 1.4f);
        }
    }

    private void HandleInlineTextEditorKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            CancelInlineText();
            e.Handled = true;
        }
        else if (e.Key == Avalonia.Input.Key.Enter && !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
        {
            CommitInlineText();
            e.Handled = true;
        }
    }

    private void HandleInlineTextEditorLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_boardScreenViewModel?.IsEditingText == true)
            CommitInlineText();
    }

    private void SyncEraserPreview(System.Numerics.Vector2? boardPoint, bool isActive)
    {
        if (_toolController?.CurrentTool != BoardToolType.Eraser || !isActive || boardPoint is null)
        {
            EraserPreview = null;
            return;
        }

        EraserPreview = new EraserPreviewState(boardPoint.Value, 10f, true);
    }

    private void ApplyToolResult(ToolResult result, string operationName, bool logThisPhase)
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        var mainViewModel = _boardScreenViewModel.MainViewModel;

        if (logThisPhase)
        {
            mainViewModel.LogBoardDebug($"tool-result-{operationName}", () => $"handled={result.Handled} boardChanged={result.BoardChanged} operations={result.Operations.Count}");
        }

        if (result.BoardChanged)
        {
            if (logThisPhase)
            {
                mainViewModel.LogBoardDebug($"invalidate-{operationName}", () => "board-changed=true action=invalidate");
            }

            viewport.InvalidateBoard();
        }
        else if (logThisPhase)
        {
            mainViewModel.LogBoardDebug($"invalidate-{operationName}", () => "board-changed=false action=skip");
        }

        if (result.HasOperations)
        {
            foreach (var operation in result.Operations)
            {
                mainViewModel.PublishLocalBoardOperation(operation);
            }

            if (mainViewModel.Host is not null)
            {
                mainViewModel.SyncBoardFromHost();
                _toolController.SetBoard(mainViewModel.Board);
                SyncSelectionOverlay();
                if (logThisPhase)
                {
                    mainViewModel.LogBoardDebug($"publish-{operationName}", () => $"operations={result.Operations.Count} action=host-resync");
                }
            }
            else if (logThisPhase)
            {
                mainViewModel.LogBoardDebug($"publish-{operationName}", () => $"operations={result.Operations.Count} action=published");
            }
        }
        else if (logThisPhase)
        {
            mainViewModel.LogBoardDebug($"publish-{operationName}", () => "operations=0 action=skip");
        }

        SyncSelectionOverlay();
    }

    private void SyncSelectionOverlay()
    {
        if (_toolController is null)
        {
            viewport.SelectionOverlay = null;
            if (_boardScreenViewModel is not null)
                _boardScreenViewModel.HasSelectedTextSelection = false;
            return;
        }

        if (_toolController.CurrentTool != BoardToolType.Select)
        {
            viewport.SelectionOverlay = null;
            if (_boardScreenViewModel is not null)
                _boardScreenViewModel.HasSelectedTextSelection = false;
            return;
        }

        if (_boardScreenViewModel is not null)
        {
            var selectedId = _toolController.Selection.ActiveElementId;
            var selected = selectedId is null
                ? null
                : _toolController.Board.Elements.FirstOrDefault(e => e.Id == selectedId);
            _boardScreenViewModel.HasSelectedTextSelection = selected is TextElement;
        }

        viewport.SelectionOverlay = new SelectionOverlayState(
            _toolController.Selection.SelectedElementIds.ToArray(),
            _toolController.Selection.ActiveElementId,
            _toolController.GetSelectionHandles(),
            _toolController.GetSelectionBoxRect());
    }

    private void UpdateSelectedTextPropertiesFromPanel()
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        if (_toolController.Selection.ActiveElementId is not Guid activeId)
            return;

        var element = _toolController.Board.Elements.FirstOrDefault(e => e.Id == activeId);
        if (element is not TextElement textElement)
            return;

        textElement.Color = new SkiaSharp.SKColor(
            _boardScreenViewModel.SelectedStrokeColor.Red,
            _boardScreenViewModel.SelectedStrokeColor.Green,
            _boardScreenViewModel.SelectedStrokeColor.Blue,
            (byte)Math.Round(255 * _boardScreenViewModel.Opacity));
        textElement.FontSize = _boardScreenViewModel.FontSize;
        textElement.FontFamily = _boardScreenViewModel.FontFamily;
        textElement.Size = MeasureTextSize(textElement.Text, textElement.FontSize, textElement.FontFamily);

        _boardScreenViewModel.MainViewModel.PublishLocalBoardOperation(
            new UpdateElementOperation(textElement.Id, new Dictionary<string, object>
            {
                ["Color"] = textElement.Color,
                ["FontSize"] = textElement.FontSize,
                ["FontFamily"] = textElement.FontFamily
            }));

        viewport.InvalidateBoard();
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

    private async void HandleImageImportRequested(object? sender, EventArgs e)
    {
        await ImportImageFromFileAsync();
    }

    public async Task ImportImageFromFileAsync()
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        var mainViewModel = _boardScreenViewModel.MainViewModel;
        var fileDialogService = mainViewModel.FileDialogService;
        if (fileDialogService is null)
            return;

        try
        {
            var imagePath = await fileDialogService.OpenImagePathAsync();
            if (string.IsNullOrEmpty(imagePath))
                return;

            var imageData = await System.IO.File.ReadAllBytesAsync(imagePath);
            if (imageData.Length == 0)
                return;

            var fileName = System.IO.Path.GetFileName(imagePath);
            PlaceImageOnBoard(imageData, fileName);
        }
        catch (Exception ex)
        {
            mainViewModel.LogBoardDebug("image-import-error", () => ex.ToString());
        }
    }

    public async Task CopySelectedImageToClipboardAsync()
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        var clipboardService = _boardScreenViewModel.MainViewModel.ClipboardService;
        if (clipboardService is null)
            return;

        // Find the selected element
        var activeId = _toolController.Selection.ActiveElementId;
        if (activeId is null)
            return;

        var element = _toolController.Board.Elements.FirstOrDefault(e => e.Id == activeId);
        if (element is not BFGA.Core.Models.ImageElement imageElement)
            return;

        if (imageElement.ImageData is null || imageElement.ImageData.Length == 0)
            return;

        try
        {
            await clipboardService.WriteImageAsync(imageElement.ImageData, imageElement.OriginalFileName);
        }
        catch (Exception ex)
        {
            _boardScreenViewModel.MainViewModel.LogBoardDebug("image-copy-error", () => ex.ToString());
        }
    }

    public async Task ImportImageFromClipboardAsync()
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        var mainViewModel = _boardScreenViewModel.MainViewModel;
        var clipboardService = mainViewModel.ClipboardService;
        if (clipboardService is null)
            return;

        try
        {
            var clipboardImage = await clipboardService.ReadImageAsync();
            if (clipboardImage is null)
                return;

            PlaceImageOnBoard(clipboardImage.ImageData, clipboardImage.FileName);
        }
        catch (Exception ex)
        {
            mainViewModel.LogBoardDebug("clipboard-paste-error", () => ex.ToString());
        }
    }

    private void PlaceImageOnBoard(byte[] imageData, string fileName)
    {
        if (_boardScreenViewModel is null || _toolController is null)
            return;

        var mainViewModel = _boardScreenViewModel.MainViewModel;

        // Determine size from image dimensions or use a default
        var defaultSize = new System.Numerics.Vector2(300, 200);
        using var codec = SkiaSharp.SKCodec.Create(new System.IO.MemoryStream(imageData));
        if (codec is not null)
        {
            var info = codec.Info;
            defaultSize = new System.Numerics.Vector2(info.Width, info.Height);
            // Limit to reasonable size (max 800px on longest side)
            var maxSide = Math.Max(defaultSize.X, defaultSize.Y);
            if (maxSide > 800)
            {
                var scale = 800f / maxSide;
                defaultSize *= scale;
            }
        }

        // Place at center of viewport in board coordinates
        var viewportWidth = viewport.Bounds.Width > 0 ? viewport.Bounds.Width : viewport.Width;
        var viewportHeight = viewport.Bounds.Height > 0 ? viewport.Bounds.Height : viewport.Height;
        var viewportCenter = new Avalonia.Point(viewportWidth / 2, viewportHeight / 2);
        var boardCenter = viewport.ScreenToBoard(viewportCenter);
        var position = new System.Numerics.Vector2(
            boardCenter.X - defaultSize.X / 2,
            boardCenter.Y - defaultSize.Y / 2);

        SyncToolController();
        var imageElement = _toolController.PlaceImage(imageData, fileName, position, defaultSize);
        _toolController.Selection.Select(imageElement.Id);

        // Publish the operation
        var operation = new AddElementOperation(imageElement);
        mainViewModel.PublishLocalBoardOperation(operation);

        if (mainViewModel.Host is not null)
        {
            mainViewModel.SyncBoardFromHost();
            _toolController.SetBoard(mainViewModel.Board);
            SyncSelectionOverlay();
        }

        viewport.InvalidateBoard();

        // Switch back to Select tool
        _boardScreenViewModel.SelectedTool = BoardToolType.Select;
    }
}
