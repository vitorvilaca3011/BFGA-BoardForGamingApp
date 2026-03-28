using BFGA.App.Infrastructure;
using BFGA.Canvas.Tools;

namespace BFGA.App.ViewModels;

public sealed class BoardScreenViewModel : ViewModelBase
{
    private readonly RelayCommand _selectToolCommand;
    private readonly RelayCommand _handToolCommand;
    private readonly RelayCommand _penToolCommand;
    private readonly RelayCommand _rectangleToolCommand;
    private readonly RelayCommand _ellipseToolCommand;
    private readonly RelayCommand _imageToolCommand;
    private readonly RelayCommand _eraserToolCommand;
    private BoardToolType _selectedTool = BoardToolType.Select;

    public BoardScreenViewModel(MainViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;

        _selectToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Select);
        _handToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Hand);
        _penToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Pen);
        _rectangleToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Rectangle);
        _ellipseToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Ellipse);
        _imageToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Image);
        _eraserToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Eraser);
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
        }
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
}
