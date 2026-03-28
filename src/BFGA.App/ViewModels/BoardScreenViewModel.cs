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
    private BoardToolType _selectedTool = BoardToolType.Select;
    private SKColor _selectedStrokeColor = SKColors.White;
    private SKColor _selectedFillColor = SKColors.Transparent;
    private float _strokeWidth = 2f;
    private float _opacity = 1f;

    public BoardScreenViewModel(MainViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        MainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

        _selectToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Select);
        _handToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Hand);
        _penToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Pen);
        _rectangleToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Rectangle);
        _ellipseToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Ellipse);
        _imageToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Image);
        _eraserToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Eraser);
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Roster))
            OnPropertyChanged(nameof(IsRosterVisible));
    }

    public void Dispose()
    {
        MainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
    }

    public MainViewModel MainViewModel { get; }

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
            OnPropertyChanged(nameof(IsPropertyPanelVisible));
            OnPropertyChanged(nameof(ShowFillSection));
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

    public string SelectedToolText => SelectedTool switch
    {
        BoardToolType.Select => "Select",
        BoardToolType.Hand => "Hand",
        BoardToolType.Pen => "Pen",
        BoardToolType.Rectangle => "Rectangle",
        BoardToolType.Ellipse => "Ellipse",
        BoardToolType.Image => "Image",
        BoardToolType.Eraser => "Eraser",
        _ => SelectedTool.ToString()
    };

    public RelayCommand SelectToolCommand => _selectToolCommand;
    public RelayCommand HandToolCommand => _handToolCommand;
    public RelayCommand PenToolCommand => _penToolCommand;
    public RelayCommand RectangleToolCommand => _rectangleToolCommand;
    public RelayCommand EllipseToolCommand => _ellipseToolCommand;
    public RelayCommand ImageToolCommand => _imageToolCommand;
    public RelayCommand EraserToolCommand => _eraserToolCommand;

    public bool IsSelectToolActive => SelectedTool == BoardToolType.Select;

    public bool IsHandToolActive => SelectedTool == BoardToolType.Hand;

    public bool IsPenToolActive => SelectedTool == BoardToolType.Pen;

    public bool IsRectangleToolActive => SelectedTool == BoardToolType.Rectangle;

    public bool IsEllipseToolActive => SelectedTool == BoardToolType.Ellipse;

    public bool IsImageToolActive => SelectedTool == BoardToolType.Image;

    public bool IsEraserToolActive => SelectedTool == BoardToolType.Eraser;

    public bool IsPropertyPanelVisible => SelectedTool is BoardToolType.Pen or BoardToolType.Rectangle or BoardToolType.Ellipse;

    public bool ShowFillSection => SelectedTool is BoardToolType.Rectangle or BoardToolType.Ellipse;

    public bool IsRosterVisible => MainViewModel.Roster.Count > 0;
}
