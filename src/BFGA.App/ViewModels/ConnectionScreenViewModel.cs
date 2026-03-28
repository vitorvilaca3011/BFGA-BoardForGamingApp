using BFGA.App.Infrastructure;

namespace BFGA.App.ViewModels;

public sealed class ConnectionScreenViewModel : ViewModelBase
{
    public ConnectionScreenViewModel(MainViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
    }

    public MainViewModel MainViewModel { get; }
}
