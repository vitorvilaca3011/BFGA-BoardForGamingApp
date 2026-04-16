using System.ComponentModel;
using BFGA.App.Infrastructure;
using BFGA.Canvas.Tools;
using SkiaSharp;

namespace BFGA.App.ViewModels;

public sealed class BoardScreenViewModel : ViewModelBase, IDisposable
{
    private readonly RelayCommand _selectToolCommand;
    private readonly RelayCommand _handToolCommand;
    private readonly RelayCommand _penToolCommand;
    private readonly RelayCommand _rectangleToolCommand;
    private readonly RelayCommand _ellipseToolCommand;
    private readonly RelayCommand _imageToolCommand;
    private readonly RelayCommand _eraserToolCommand;
    private readonly RelayCommand _arrowToolCommand;
    private readonly RelayCommand _lineToolCommand;
    private readonly RelayCommand _textToolCommand;
    private readonly RelayCommand _laserPointerToolCommand;
    private readonly RelayCommand _toggleSettingsCommand;
    private BoardToolType _selectedTool = BoardToolType.Select;
    private SKColor _selectedStrokeColor = SKColors.White;
    private SKColor _selectedFillColor = SKColors.Transparent;
    private float _strokeWidth = 2f;
    private float _opacity = 1f;
    private float _fontSize = 24f;
    private string _fontFamily = "Inter";
    private bool _isEditingText;
    private Guid? _editingTextElementId;
    private bool _hasSelectedTextSelection;

    public BoardScreenViewModel(MainViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        MainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

        _selectToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Select);
        _handToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Hand);
        _penToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Pen);
        _rectangleToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Rectangle);
        _ellipseToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Ellipse);
        _imageToolCommand = new RelayCommand(() => ImageImportRequested?.Invoke(this, EventArgs.Empty));
        _eraserToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Eraser);
        _arrowToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Arrow);
        _lineToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Line);
        _textToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Text);
        _laserPointerToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.LaserPointer);
        _toggleSettingsCommand = new RelayCommand(() => MainViewModel.IsSettingsOpen = !MainViewModel.IsSettingsOpen);
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Roster))
        {
            OnPropertyChanged(nameof(IsRosterVisible));
        }

        if (e.PropertyName == nameof(MainViewModel.LaserPresenceColor))
        {
            OnPropertyChanged(nameof(LaserPresenceColor));
        }
    }

    public void Dispose()
    {
        MainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
    }

    public MainViewModel MainViewModel { get; }

    public event EventHandler? ImageImportRequested;

    public BoardToolType SelectedTool
    {
        get => _selectedTool;
        set
        {
            if (!SetProperty(ref _selectedTool, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedToolText));
            OnPropertyChanged(nameof(IsSelectToolActive));
            OnPropertyChanged(nameof(IsHandToolActive));
            OnPropertyChanged(nameof(IsPenToolActive));
            OnPropertyChanged(nameof(IsRectangleToolActive));
            OnPropertyChanged(nameof(IsEllipseToolActive));
            OnPropertyChanged(nameof(IsImageToolActive));
            OnPropertyChanged(nameof(IsEraserToolActive));
            OnPropertyChanged(nameof(IsArrowToolActive));
            OnPropertyChanged(nameof(IsLineToolActive));
            OnPropertyChanged(nameof(IsTextToolActive));
            OnPropertyChanged(nameof(IsLaserPointerToolActive));
            OnPropertyChanged(nameof(IsPropertyPanelVisible));
            OnPropertyChanged(nameof(ShowFillSection));
            OnPropertyChanged(nameof(IsTextSelectionActive));
            MainViewModel.LogBoardDebug("selected-tool", () => $"tool={_selectedTool}");
        }
    }

    public SKColor SelectedStrokeColor
    {
        get => _selectedStrokeColor;
        set => SetProperty(ref _selectedStrokeColor, value);
    }

    public SKColor SelectedFillColor
    {
        get => _selectedFillColor;
        set => SetProperty(ref _selectedFillColor, value);
    }

    public SKColor LaserPresenceColor
    {
        get => MainViewModel.LaserPresenceColor;
        set => MainViewModel.LaserPresenceColor = value;
    }

    public float StrokeWidth
    {
        get => _strokeWidth;
        set => SetProperty(ref _strokeWidth, value);
    }

    public float Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, Math.Clamp(value, 0f, 1f));
    }

    public float FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, Math.Clamp(value, 8f, 200f));
    }

    public string FontFamily
    {
        get => _fontFamily;
        set => SetProperty(ref _fontFamily, value);
    }

    public bool IsEditingText
    {
        get => _isEditingText;
        set => SetProperty(ref _isEditingText, value);
    }

    public Guid? EditingTextElementId
    {
        get => _editingTextElementId;
        set => SetProperty(ref _editingTextElementId, value);
    }

    public bool HasSelectedTextSelection
    {
        get => _hasSelectedTextSelection;
        set
        {
            if (!SetProperty(ref _hasSelectedTextSelection, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsPropertyPanelVisible));
            OnPropertyChanged(nameof(IsTextSelectionActive));
        }
    }

    public static IReadOnlyList<string> AvailableFontFamilies { get; } =
        ["Inter", "Arial", "Times New Roman", "Courier New", "Verdana", "Comic Sans MS"];

    public string SelectedToolText => SelectedTool switch
    {
        BoardToolType.Select => "Select",
        BoardToolType.Hand => "Hand",
        BoardToolType.Pen => "Pen",
        BoardToolType.Rectangle => "Rectangle",
        BoardToolType.Ellipse => "Ellipse",
        BoardToolType.Image => "Image",
        BoardToolType.Eraser => "Eraser",
        BoardToolType.Arrow => "Arrow",
        BoardToolType.Line => "Line",
        BoardToolType.Text => "Text",
        BoardToolType.LaserPointer => "Laser Pointer",
        _ => SelectedTool.ToString()
    };

    public RelayCommand SelectToolCommand => _selectToolCommand;
    public RelayCommand HandToolCommand => _handToolCommand;
    public RelayCommand PenToolCommand => _penToolCommand;
    public RelayCommand RectangleToolCommand => _rectangleToolCommand;
    public RelayCommand EllipseToolCommand => _ellipseToolCommand;
    public RelayCommand ImageToolCommand => _imageToolCommand;
    public RelayCommand EraserToolCommand => _eraserToolCommand;
    public RelayCommand ArrowToolCommand => _arrowToolCommand;
    public RelayCommand LineToolCommand => _lineToolCommand;
    public RelayCommand TextToolCommand => _textToolCommand;
    public RelayCommand LaserPointerToolCommand => _laserPointerToolCommand;
    public RelayCommand ToggleSettingsCommand => _toggleSettingsCommand;

    public bool IsSelectToolActive => SelectedTool == BoardToolType.Select;

    public bool IsHandToolActive => SelectedTool == BoardToolType.Hand;

    public bool IsPenToolActive => SelectedTool == BoardToolType.Pen;

    public bool IsRectangleToolActive => SelectedTool == BoardToolType.Rectangle;

    public bool IsEllipseToolActive => SelectedTool == BoardToolType.Ellipse;

    public bool IsImageToolActive => SelectedTool == BoardToolType.Image;

    public bool IsEraserToolActive => SelectedTool == BoardToolType.Eraser;

    public bool IsArrowToolActive => SelectedTool == BoardToolType.Arrow;

    public bool IsLineToolActive => SelectedTool == BoardToolType.Line;

    public bool IsTextToolActive => SelectedTool == BoardToolType.Text;

    public bool IsLaserPointerToolActive => SelectedTool == BoardToolType.LaserPointer;

    public bool IsPropertyPanelVisible => SelectedTool is BoardToolType.Pen or BoardToolType.Rectangle or BoardToolType.Ellipse or BoardToolType.Arrow or BoardToolType.Line or BoardToolType.Text || HasSelectedTextSelection;

    public bool ShowFillSection => SelectedTool is BoardToolType.Rectangle or BoardToolType.Ellipse;

    public bool ShowTextFontSection => SelectedTool is BoardToolType.Text;

    public bool IsTextSelectionActive => HasSelectedTextSelection;

    public bool IsRosterVisible => MainViewModel.Roster.Count > 0;
}
