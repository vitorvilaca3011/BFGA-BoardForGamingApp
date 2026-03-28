using Avalonia;

namespace BFGA.App.Tests;

public class StartupSmokeTests
{
    [Fact]
    public void App_Initialize_LoadsXamlWithoutThrowing()
    {
        var app = new BFGA.App.App();

        app.Initialize();

        Assert.True(true);
    }
}
