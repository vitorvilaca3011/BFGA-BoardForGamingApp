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
            or nameof(BoardScreenViewModel.Opacity))
        {
            SyncToolController();
        }

        if (e.PropertyName is nameof(BoardScreenViewModel.SelectedTool))
        {
            UpdateCursorForTool();
        }
    }

    private void UpdateCursorForTool()
    {
        try
        {
            var tool = _boardScreenViewModel?.SelectedTool ?? BoardToolType.Select;
            viewport.Cursor = new Cursor(tool switch
            {
                BoardToolType.Hand => StandardCursorType.Hand,
                BoardToolType.Pen => StandardCursorType.Cross,
                BoardToolType.Rectangle => StandardCursorType.Cross,
                BoardToolType.Ellipse => StandardCursorType.Cross,
                BoardToolType.Arrow => StandardCursorType.Cross,
                BoardToolType.Line => StandardCursorType.Cross,
                BoardToolType.Eraser => StandardCursorType.Cross,
                BoardToolType.Text => StandardCursorType.Ibeam,
                BoardToolType.Image => StandardCursorType.Hand,
                _ => StandardCursorType.Arrow
            });
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

        // Find the selected element
        var activeId = _toolController.Selection.ActiveElementId;
        if (activeId is null)
            return;

        var element = _toolController.Board.Elements.FirstOrDefault(e => e.Id == activeId);
        if (element is not BFGA.Core.Models.ImageElement imageElement)
            return;

        if (imageElement.ImageData is null || imageElement.ImageData.Length == 0)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        try
        {
#pragma warning disable CS0618
            var dataObject = new Avalonia.Input.DataObject();
            dataObject.Set("image/png", imageElement.ImageData);
            await clipboard.SetDataObjectAsync(dataObject);
#pragma warning restore CS0618
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

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;

#pragma warning disable CS0618
            var formats = await clipboard.GetFormatsAsync();
#pragma warning restore CS0618
            if (formats is null)
                return;

            byte[]? imageData = null;
            string fileName = "clipboard.png";

            // Try to get image data from clipboard
            var imageFormat = formats.FirstOrDefault(f =>
                f.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                f.Contains("png", StringComparison.OrdinalIgnoreCase) ||
                f.Contains("bitmap", StringComparison.OrdinalIgnoreCase));

            if (imageFormat is not null)
            {
#pragma warning disable CS0618
                var data = await clipboard.GetDataAsync(imageFormat);
#pragma warning restore CS0618
                if (data is byte[] bytes)
                    imageData = bytes;
                else if (data is System.IO.Stream stream)
                {
                    using var ms = new System.IO.MemoryStream();
                    await stream.CopyToAsync(ms);
                    imageData = ms.ToArray();
                }
            }

            // Try file drop list as fallback
            if (imageData is null)
            {
                var fileFormats = new[] { "Files", "FileNames", "text/uri-list" };
                foreach (var ff in fileFormats)
                {
                    if (!formats.Contains(ff))
                        continue;

#pragma warning disable CS0618
                    var data = await clipboard.GetDataAsync(ff);
#pragma warning restore CS0618
                    if (data is IEnumerable<string> filePaths)
                    {
                        var path = filePaths.FirstOrDefault(p =>
                            p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                            p.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
                        if (path is not null && System.IO.File.Exists(path))
                        {
                            imageData = await System.IO.File.ReadAllBytesAsync(path);
                            fileName = System.IO.Path.GetFileName(path);
                        }
                    }

                    if (imageData is not null)
                        break;
                }
            }

            if (imageData is null || imageData.Length == 0)
                return;

            PlaceImageOnBoard(imageData, fileName);
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
        var viewportCenter = new Avalonia.Point(viewport.Bounds.Width / 2, viewport.Bounds.Height / 2);
        var boardCenter = viewport.ScreenToBoard(viewportCenter);
        var position = new System.Numerics.Vector2(
            boardCenter.X - defaultSize.X / 2,
            boardCenter.Y - defaultSize.Y / 2);

        SyncToolController();
        var imageElement = _toolController.PlaceImage(imageData, fileName, position, defaultSize);

        // Publish the operation
        var operation = new AddElementOperation(imageElement);
        mainViewModel.PublishLocalBoardOperation(operation);

        if (mainViewModel.Host is not null)
        {
            mainViewModel.SyncBoardFromHost();
            _toolController.SetBoard(mainViewModel.Board);
        }

        viewport.InvalidateBoard();

        // Switch back to Select tool
        _boardScreenViewModel.SelectedTool = BoardToolType.Select;
    }
}
