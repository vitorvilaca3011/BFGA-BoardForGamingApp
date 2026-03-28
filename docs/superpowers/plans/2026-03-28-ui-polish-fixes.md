# UI Polish & Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 7 UI/UX issues identified during manual testing: DLL lock on close, window chrome, toolbar remake, property panel text, background grid, zoom bar, and settings panel.

**Architecture:** Each task targets a specific UI concern. Tasks are ordered by dependency: DLL fix first (standalone bug), then tool model refactor (needed by toolbar), then window chrome (needed by settings), then toolbar/grid/panel/zoom/settings in dependency order.

**Tech Stack:** .NET 9, Avalonia UI, SkiaSharp, CommunityToolkit.Mvvm patterns (RelayCommand/AsyncRelayCommand), MessagePack serialization.

**Spec:** `docs/superpowers/specs/2026-03-28-ui-polish-fixes-design.md`

**Testing policy:** Per user direction, skip heavy test additions. Update existing tests that break (Shape enum removal, layout assertions that reference old vertical toolbar/DockPanel structure). Verify via `dotnet build` + `dotnet test` (248 existing tests must not regress).

**Known test files that will need updates:**
- `tests/BFGA.App.Tests/BoardScreenLayoutTests.cs`: Asserts `DockPanel`, `DockPanel.Dock="Left"`, toolbar icon sequence (old vertical order), separator count — all change with horizontal floating toolbar.
- `tests/BFGA.App.Tests/BoardScreenViewModelTests.cs`: Asserts `IsXToolActive` properties — must add assertions for new tool types (Arrow/Line/Text/LaserPointer).
- `tests/BFGA.Core.Tests/BoardToolControllerTests.cs:408,428,449`: References `BoardToolType.Shape` — change to `BoardToolType.Rectangle`.
- `tests/BFGA.App.Tests/MainViewModelTests.cs:785`: Asserts `BoardToolType.Shape` — change to `BoardToolType.Rectangle`.

---

### Task 1: DLL Lock Fix — Async Window Closing

**Problem:** `CloseAsync().GetAwaiter().GetResult()` in `OnClosed` blocks UI thread, causing deadlock when async operations try to dispatch back to UI. Process stays alive with DLLs locked.

**Files:**
- Modify: `src/BFGA.App/MainWindow.axaml.cs:13-42` (replace `OnClosed` with `Closing` handler)

- [ ] **Step 1: Add `_isClosing` flag and `Closing` handler to MainWindow**

In `src/BFGA.App/MainWindow.axaml.cs`, add a private field and subscribe to `Closing` in the constructor:

```csharp
private bool _isClosing;
```

In the constructor, after `viewModel.StartPolling()`, add:

```csharp
Closing += OnWindowClosing;
```

- [ ] **Step 2: Implement `OnWindowClosing` method**

Add this method to `MainWindow.axaml.cs`:

```csharp
private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
{
    if (_isClosing)
        return; // Second call after cleanup — allow close

    e.Cancel = true;
    _isClosing = true;

    try
    {
        Task cleanupTask = Task.CompletedTask;
        if (DataContext is MainViewModel viewModel)
            cleanupTask = viewModel.CloseAsync();

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3));
        var winner = await Task.WhenAny(cleanupTask, timeoutTask);

        // If cleanup finished first, observe any exceptions
        if (winner == cleanupTask)
            await cleanupTask;
        // If timeout won, proceed — OS reclaims resources on exit
    }
    catch (Exception)
    {
        // Swallow cleanup errors — we're shutting down
    }

    Close();
}
```

**Why this pattern**: `CloseAsync()` stays on the calling thread (no `Task.Run`), so `Dispose()` runs on the UI thread where Avalonia controls are safe. The 3-second timeout wraps the *entire* cleanup chain (`CloseAsync` → `HostAutosaveAsync` → `Dispose`). If any of those hangs, the timeout fires and we close anyway. Re-awaiting `cleanupTask` after `WhenAny` ensures exceptions are observed rather than swallowed.

- [ ] **Step 3: Replace `OnClosed` override**

Remove the `OnClosed` override (lines 26-31). Replace with a simpler version that only handles non-MainViewModel DataContexts (fallback):

```csharp
protected override void OnClosed(EventArgs e)
{
    if (!_isClosing && DataContext is IDisposable disposable and not MainViewModel)
        disposable.Dispose();
    base.OnClosed(e);
}
```

Keep the static `CloseDataContext` method for test compatibility but it will no longer be the primary close path.

- [ ] **Step 4: No changes needed to `MainViewModel.CloseAsync`**

The existing `CloseAsync` signature and body remain unchanged — it already calls `HostAutosaveAsync()` then `Dispose()`. The timeout is enforced externally by `Task.WhenAny` in the window closing handler, so no `CancellationToken` parameter is needed.
```

- [ ] **Step 5: Verify**

Run: `dotnet build src/BFGA.App/BFGA.App.csproj`
Expected: Build succeeds.

Run: `dotnet test`
Expected: 248 tests pass.

**Manual verification (when possible):** Launch the app with `dotnet run --project src/BFGA.App/BFGA.App.csproj`, close the window, confirm the process exits promptly (no hanging). Then rebuild immediately to confirm no DLL lock.

- [ ] **Step 6: Commit**

```bash
git add src/BFGA.App/MainWindow.axaml.cs
git commit -m "fix(window): async closing to prevent DLL lock deadlock"
```

---

### Task 2: Tool Model Refactor — Remove Shape, Add New Tool Types

**Problem:** `BoardToolType.Shape` is an internal routing hack. Need to remove it, add Arrow/Line/Text/LaserPointer as inert placeholders, and fix Rectangle/Ellipse routing.

**Files:**
- Modify: `src/BFGA.Canvas/Tools/BoardToolType.cs:1-13` (remove Shape, add new types)
- Modify: `src/BFGA.Canvas/Tools/BoardToolController.cs:92-105` (replace Shape case with Rectangle/Ellipse)
- Modify: `src/BFGA.App/Views/BoardView.axaml.cs:166-179` (remove Shape remapping)
- Modify: `tests/BFGA.Core.Tests/BoardToolControllerTests.cs:408,428,449` (update Shape references)
- Modify: `tests/BFGA.App.Tests/MainViewModelTests.cs:785` (update Shape assertion)

- [ ] **Step 1: Update `BoardToolType` enum**

In `src/BFGA.Canvas/Tools/BoardToolType.cs`, replace the entire file:

```csharp
namespace BFGA.Canvas.Tools;

public enum BoardToolType
{
    Select,
    Hand,
    Pen,
    Rectangle,
    Ellipse,
    Image,
    Eraser,
    Arrow,
    Line,
    Text,
    LaserPointer
}
```

Note: `Shape` is removed. New values `Arrow`, `Line`, `Text`, `LaserPointer` are added at the end to avoid changing existing ordinal values for Select(0) through Eraser(6).

- [ ] **Step 2: Update `BoardToolController` to handle Rectangle/Ellipse directly**

In `src/BFGA.Canvas/Tools/BoardToolController.cs`:

**At `HandlePointerDown` (line 92-113):** Replace the Shape case:

Old (lines 102-105):
```csharp
case BoardToolType.Rectangle:
case BoardToolType.Ellipse:
case BoardToolType.Shape:
    return HandleShapeDown(position);
```

New:
```csharp
case BoardToolType.Rectangle:
    ShapeType = ShapeType.Rectangle;
    return HandleShapeDown(position);
case BoardToolType.Ellipse:
    ShapeType = ShapeType.Ellipse;
    return HandleShapeDown(position);
```

This sets the `ShapeType` automatically when pointer goes down, so `BoardView` no longer needs to manage it.

**At `SetTool` method (lines 55-61):** No changes needed — it already accepts any `BoardToolType` and resets gesture mode. Unknown tool types will just clear the active state (no-op for canvas interaction).

- [ ] **Step 3: Remove Shape remapping in `BoardView.axaml.cs`**

In `src/BFGA.App/Views/BoardView.axaml.cs`, replace the `SyncToolController` switch block (lines 166-179):

Old:
```csharp
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
```

New:
```csharp
_toolController.SetTool(boardScreen.SelectedTool);
```

That's it — one line. `BoardToolController.HandlePointerDown` now sets `ShapeType` internally for Rectangle/Ellipse.

- [ ] **Step 4: Update `BoardToolControllerTests`**

In `tests/BFGA.Core.Tests/BoardToolControllerTests.cs`, update the three Shape-referencing tests:

**Test at line ~408** (`ShapeTool_CreatesNormalizedRectangleFromDrag`):
Change `controller.SetTool(BoardToolType.Shape)` to `controller.SetTool(BoardToolType.Rectangle)`.

**Test at line ~428** (`ShapeTool_ClickOnly_DoesNotInsertZeroSizeShape`):
Change `controller.SetTool(BoardToolType.Shape)` to `controller.SetTool(BoardToolType.Rectangle)`.

**Test at line ~449** (`ShapeTool_InsertsShapeAboveExistingElements`):
Change `controller.SetTool(BoardToolType.Shape)` to `controller.SetTool(BoardToolType.Rectangle)`.

- [ ] **Step 5: Update `MainViewModelTests`**

In `tests/BFGA.App.Tests/MainViewModelTests.cs`, at line ~785 in `BoardView_WiresSelectedToolIntoRuntimeController`:

Change:
```csharp
Assert.Equal(BoardToolType.Shape, currentTool);
```
To:
```csharp
Assert.Equal(BoardToolType.Rectangle, currentTool);
```

Remove the `ShapeType` assertion at line ~786 (no longer exposed as a separate property from the view layer).

- [ ] **Step 6: Verify**

Run: `dotnet build`
Expected: Build succeeds.

Run: `dotnet test`
Expected: 248 tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor(tools): remove Shape enum, add Arrow/Line/Text/LaserPointer placeholders"
```

---

### Task 3: Window & Custom Title Bar

**Problem:** Maximizing causes invisible window. Need fixed size + custom title bar with minimize/close/gear buttons.

**Files:**
- Modify: `src/BFGA.App/MainWindow.axaml:1-29` (add window properties, custom title bar)
- Modify: `src/BFGA.App/MainWindow.axaml.cs` (add minimize/close handlers)
- Modify: `src/BFGA.App/Styles/WhiteboardTheme.axaml` (add title bar styles)
- Modify: `src/BFGA.App/ViewModels/MainViewModel.cs` (add `IsSettingsOpen` property)
- Modify: `src/BFGA.App/Styles/Colors.axaml` (add CloseButtonHover color)

- [ ] **Step 1: Add `IsSettingsOpen` to MainViewModel**

In `src/BFGA.App/ViewModels/MainViewModel.cs`, add a private field among the state fields (around line 50):

```csharp
private bool _isSettingsOpen;
```

Add the public property near the other UI state properties:

```csharp
public bool IsSettingsOpen
{
    get => _isSettingsOpen;
    set => SetProperty(ref _isSettingsOpen, value);
}

public bool IsBoardScreen => CurrentScreen is BoardScreenViewModel;
```

Also, in the `CurrentScreen` property setter (wherever `OnPropertyChanged(nameof(CurrentScreen))` is called), add:

```csharp
OnPropertyChanged(nameof(IsBoardScreen));
```

- [ ] **Step 2: Add CloseButtonHover color**

In `src/BFGA.App/Styles/Colors.axaml`, add before the closing `</ResourceDictionary>`:

```xml
<SolidColorBrush x:Key="CloseButtonHover" Color="#C42B1C" />
```

- [ ] **Step 3: Rewrite `MainWindow.axaml` with fixed size and custom title bar**

Replace the entire `src/BFGA.App/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:views="clr-namespace:BFGA.App.Views"
        xmlns:vm="clr-namespace:BFGA.App.ViewModels"
        mc:Ignorable="d" d:DesignWidth="1400" d:DesignHeight="900"
        x:Class="BFGA.App.MainWindow"
        x:DataType="vm:MainViewModel"
        Title="BFGA"
        Width="1400" Height="900"
        CanResize="False"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaTitleBarHeightHint="40"
        ExtendClientAreaChromeHints="NoChrome"
        WindowStartupLocation="CenterScreen"
        Background="{DynamicResource BgBase}"
        KeyDown="OnKeyDown">

    <Window.DataTemplates>
        <DataTemplate DataType="vm:ConnectionScreenViewModel">
            <views:ConnectionScreen />
        </DataTemplate>
        <DataTemplate DataType="vm:BoardScreenViewModel">
            <views:BoardScreen />
        </DataTemplate>
    </Window.DataTemplates>

    <DockPanel>
        <!-- Custom Title Bar -->
        <Border DockPanel.Dock="Top" Height="40"
                Background="{DynamicResource BgBase}"
                IsHitTestVisible="True">
            <DockPanel Margin="12,0">
                <!-- Left: App name -->
                <TextBlock DockPanel.Dock="Left"
                           Text="BFGA"
                           VerticalAlignment="Center"
                           FontFamily="{DynamicResource InterMediumFont}"
                           FontSize="14"
                           Foreground="{DynamicResource TextSecondary}" />

                <!-- Right: Window controls -->
                <StackPanel DockPanel.Dock="Right"
                            Orientation="Horizontal"
                            Spacing="0"
                            VerticalAlignment="Stretch">

                    <!-- Gear / Settings button (only on board screen) -->
                    <Button Classes="title-bar-button"
                            Click="OnSettingsClick"
                            IsVisible="{Binding IsBoardScreen}"
                            ToolTip.Tip="Settings">
                        <PathIcon Data="M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M12 13a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"
                                 Width="14" Height="14" />
                    </Button>

                    <!-- Minimize -->
                    <Button Classes="title-bar-button"
                            Click="OnMinimizeClick"
                            ToolTip.Tip="Minimize">
                        <PathIcon Data="M4,12 L20,12" Width="14" Height="14" />
                    </Button>

                    <!-- Close -->
                    <Button Classes="title-bar-button close-button"
                            Click="OnCloseClick"
                            ToolTip.Tip="Close">
                        <PathIcon Data="M18,6 L6,18 M6,6 L18,18" Width="14" Height="14" />
                    </Button>
                </StackPanel>

                <!-- Spacer -->
                <Border />
            </DockPanel>
        </Border>

        <!-- Main content -->
        <TransitioningContentControl Content="{Binding CurrentScreen}">
            <TransitioningContentControl.PageTransition>
                <CrossFade Duration="0:0:0.3" />
            </TransitioningContentControl.PageTransition>
        </TransitioningContentControl>
    </DockPanel>
</Window>
```

- [ ] **Step 4: Add title bar button handlers to `MainWindow.axaml.cs`**

Add these methods to `MainWindow.axaml.cs`:

```csharp
private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    if (DataContext is MainViewModel vm)
        vm.IsSettingsOpen = !vm.IsSettingsOpen;
}

private void OnMinimizeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    WindowState = WindowState.Minimized;
}

private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
{
    Close();
}
```

- [ ] **Step 5: Add title bar styles to WhiteboardTheme.axaml**

Add these styles to `src/BFGA.App/Styles/WhiteboardTheme.axaml` (before the toolbar styles):

```xml
<!-- Title bar buttons -->
<Style Selector="Button.title-bar-button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Foreground" Value="{DynamicResource TextSecondary}" />
    <Setter Property="Width" Value="46" />
    <Setter Property="Height" Value="40" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="CornerRadius" Value="0" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
    <Setter Property="WindowChrome.IsHitTestVisibleInChrome" Value="True" />
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.15" />
            <BrushTransition Property="Foreground" Duration="0:0:0.15" />
        </Transitions>
    </Setter>
</Style>

<Style Selector="Button.title-bar-button:pointerover">
    <Setter Property="Background" Value="{DynamicResource BgOverlay}" />
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
</Style>

<Style Selector="Button.title-bar-button.close-button:pointerover">
    <Setter Property="Background" Value="{DynamicResource CloseButtonHover}" />
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
</Style>
```

- [ ] **Step 6: Verify**

Run: `dotnet build src/BFGA.App/BFGA.App.csproj`
Expected: Build succeeds.

Run: `dotnet test`
Expected: 248 tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(window): fixed size 1400x900 with custom title bar"
```

---

### Task 4: Floating Horizontal Toolbar + New Tool Commands + Shortcuts

**Problem:** Vertical sidebar toolbar is non-standard. Need Excalidraw-style horizontal floating bar with new tool types.

**Files:**
- Modify: `src/BFGA.App/Assets/ToolIcons.axaml:1-10` (add all new icons, redesign existing)
- Modify: `src/BFGA.App/Views/ToolBar.axaml:1-64` (rewrite as horizontal floating bar)
- Modify: `src/BFGA.App/ViewModels/BoardScreenViewModel.cs` (add commands for Arrow/Line/Text/LaserPointer)
- Modify: `src/BFGA.App/Views/BoardScreen.axaml:8-13` (move toolbar from dock-left to grid overlay top-center)
- Modify: `src/BFGA.App/MainWindow.axaml.cs:79-112` (add A/L/T shortcuts)
- Modify: `src/BFGA.App/Styles/WhiteboardTheme.axaml` (update toolbar styles for horizontal layout)

- [ ] **Step 1: Add new tool icons to `ToolIcons.axaml`**

Replace the entire `src/BFGA.App/Assets/ToolIcons.axaml` with Lucide-style outline icons for all tools:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Navigation -->
    <StreamGeometry x:Key="ToolIconSelect">M3,3 L10,20 L12,13 L19,15 Z</StreamGeometry>
    <StreamGeometry x:Key="ToolIconHand">M18,11 V6 A1,1 0 0 0 16,6 V10 M14,5.5 V4 A1,1 0 0 0 12,4 V10 M10,5.5 V4 A1,1 0 0 0 8,4 V10 M8,10 V7 A1,1 0 0 0 6,7 V14 C6,18 9,21 13,21 C17,21 20,18 20,14 V11 A1,1 0 0 0 18,11 Z</StreamGeometry>

    <!-- Shapes -->
    <StreamGeometry x:Key="ToolIconRectangle">M3,5 H21 V19 H3 Z</StreamGeometry>
    <StreamGeometry x:Key="ToolIconEllipse">M12,4 C17.52,4 22,7.58 22,12 C22,16.42 17.52,20 12,20 C6.48,20 2,16.42 2,12 C2,7.58 6.48,4 12,4 Z</StreamGeometry>
    <StreamGeometry x:Key="ToolIconArrow">M5,12 H19 M19,12 L13,6 M19,12 L13,18</StreamGeometry>
    <StreamGeometry x:Key="ToolIconLine">M5,19 L19,5</StreamGeometry>

    <!-- Freehand -->
    <StreamGeometry x:Key="ToolIconPen">M21.174,6.812 A1,1 0 0 0 19.86,5.244 L5.22,17.372 L3,21 L6.628,18.78 Z M15,11 L13,9</StreamGeometry>

    <!-- Text & Media -->
    <StreamGeometry x:Key="ToolIconText">M6,4 L6,8 M18,4 L18,8 M6,4 H18 M12,4 V20 M9,20 H15</StreamGeometry>
    <StreamGeometry x:Key="ToolIconImage">M3,5 C3,3.9 3.9,3 5,3 H19 C20.1,3 21,3.9 21,5 V19 C21,20.1 20.1,21 19,21 H5 C3.9,21 3,20.1 3,19 Z M3,16 L8,11 L13,16 M14,15 L17,12 L21,16 M10,9 A1.5,1.5 0 1 1 10,8.99</StreamGeometry>

    <!-- Utility -->
    <StreamGeometry x:Key="ToolIconEraser">M10.5,21 H18 M7,21 L20.5,7.5 A2.12,2.12 0 0 0 17,4 L3.5,17.5 A2.12,2.12 0 0 0 7,21 Z M2,21 H7 M13,7.5 L17,11.5</StreamGeometry>
    <StreamGeometry x:Key="ToolIconLaser">M12,12 m-1,0 a1,1 0 1,0 2,0 a1,1 0 1,0 -2,0 M12,2 V5 M12,19 V22 M2,12 H5 M19,12 H22 M4.93,4.93 L7.05,7.05 M16.95,16.95 L19.07,19.07 M4.93,19.07 L7.05,16.95 M16.95,7.05 L19.07,4.93</StreamGeometry>

    <!-- Bottom bar icons -->
    <StreamGeometry x:Key="IconUndo">M3,7 V13 H9 M3,13 A9,6 0 0 1 21,13</StreamGeometry>
    <StreamGeometry x:Key="IconRedo">M21,7 V13 H15 M21,13 A9,6 0 0 0 3,13</StreamGeometry>
    <StreamGeometry x:Key="IconZoomIn">M11,4 A7,7 0 1 0 11,18 A7,7 0 0 0 11,4 M21,21 L16.65,16.65 M8,11 H14 M11,8 V14</StreamGeometry>
    <StreamGeometry x:Key="IconZoomOut">M11,4 A7,7 0 1 0 11,18 A7,7 0 0 0 11,4 M21,21 L16.65,16.65 M8,11 H14</StreamGeometry>
</ResourceDictionary>
```

- [ ] **Step 2: Add new tool commands to `BoardScreenViewModel`**

In `src/BFGA.App/ViewModels/BoardScreenViewModel.cs`:

Add 4 new command fields alongside the existing ones (around line 10-16):

```csharp
private readonly RelayCommand _arrowToolCommand;
private readonly RelayCommand _lineToolCommand;
private readonly RelayCommand _textToolCommand;
private readonly RelayCommand _laserPointerToolCommand;
```

In the constructor, after the existing command initializations (around line 33):

```csharp
_arrowToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Arrow);
_lineToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Line);
_textToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.Text);
_laserPointerToolCommand = new RelayCommand(() => SelectedTool = BoardToolType.LaserPointer);
```

Add the public command properties alongside existing ones (around line 109-115):

```csharp
public RelayCommand ArrowToolCommand => _arrowToolCommand;
public RelayCommand LineToolCommand => _lineToolCommand;
public RelayCommand TextToolCommand => _textToolCommand;
public RelayCommand LaserPointerToolCommand => _laserPointerToolCommand;
```

Add the IsActive properties alongside existing ones (around line 117-129):

```csharp
public bool IsArrowToolActive => SelectedTool == BoardToolType.Arrow;
public bool IsLineToolActive => SelectedTool == BoardToolType.Line;
public bool IsTextToolActive => SelectedTool == BoardToolType.Text;
public bool IsLaserPointerToolActive => SelectedTool == BoardToolType.LaserPointer;
```

Update the `SelectedTool` setter (lines 50-71) to notify all new properties:

```csharp
OnPropertyChanged(nameof(IsArrowToolActive));
OnPropertyChanged(nameof(IsLineToolActive));
OnPropertyChanged(nameof(IsTextToolActive));
OnPropertyChanged(nameof(IsLaserPointerToolActive));
```

Update `SelectedToolText` switch (lines 97-107) to add:

```csharp
BoardToolType.Arrow => "Arrow",
BoardToolType.Line => "Line",
BoardToolType.Text => "Text",
BoardToolType.LaserPointer => "Laser Pointer",
```

Update `IsPropertyPanelVisible` (line 131):

```csharp
public bool IsPropertyPanelVisible => SelectedTool is BoardToolType.Pen or BoardToolType.Rectangle or BoardToolType.Ellipse or BoardToolType.Arrow or BoardToolType.Line;
```

- [ ] **Step 3: Rewrite `ToolBar.axaml` as horizontal floating bar**

Replace the entire `src/BFGA.App/Views/ToolBar.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:BFGA.App.ViewModels"
             x:Class="BFGA.App.Views.ToolBar"
             x:DataType="vm:BoardScreenViewModel"
             Classes="whiteboard-toolbar">
    <Border Classes="whiteboard-floating-toolbar">
        <StackPanel Orientation="Horizontal" Spacing="2">
            <!-- Navigation -->
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsSelectToolActive}"
                    Command="{Binding SelectToolCommand}"
                    ToolTip.Tip="Select (V)">
                <PathIcon Data="{StaticResource ToolIconSelect}" Width="16" Height="16" />
            </Button>
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsHandToolActive}"
                    Command="{Binding HandToolCommand}"
                    ToolTip.Tip="Hand (H)">
                <PathIcon Data="{StaticResource ToolIconHand}" Width="16" Height="16" />
            </Button>

            <Border Width="1" Height="20" Background="{DynamicResource BorderDefault}" Margin="4,0" />

            <!-- Shapes -->
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsRectangleToolActive}"
                    Command="{Binding RectangleToolCommand}"
                    ToolTip.Tip="Rectangle (R)">
                <PathIcon Data="{StaticResource ToolIconRectangle}" Width="16" Height="16" />
            </Button>
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsEllipseToolActive}"
                    Command="{Binding EllipseToolCommand}"
                    ToolTip.Tip="Ellipse (E)">
                <PathIcon Data="{StaticResource ToolIconEllipse}" Width="16" Height="16" />
            </Button>
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsArrowToolActive}"
                    Command="{Binding ArrowToolCommand}"
                    ToolTip.Tip="Arrow (A)">
                <PathIcon Data="{StaticResource ToolIconArrow}" Width="16" Height="16" />
            </Button>
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsLineToolActive}"
                    Command="{Binding LineToolCommand}"
                    ToolTip.Tip="Line (L)">
                <PathIcon Data="{StaticResource ToolIconLine}" Width="16" Height="16" />
            </Button>

            <Border Width="1" Height="20" Background="{DynamicResource BorderDefault}" Margin="4,0" />

            <!-- Freehand -->
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsPenToolActive}"
                    Command="{Binding PenToolCommand}"
                    ToolTip.Tip="Pen (P)">
                <PathIcon Data="{StaticResource ToolIconPen}" Width="16" Height="16" />
            </Button>

            <Border Width="1" Height="20" Background="{DynamicResource BorderDefault}" Margin="4,0" />

            <!-- Text & Media -->
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsTextToolActive}"
                    Command="{Binding TextToolCommand}"
                    ToolTip.Tip="Text (T)">
                <PathIcon Data="{StaticResource ToolIconText}" Width="16" Height="16" />
            </Button>
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsImageToolActive}"
                    Command="{Binding ImageToolCommand}"
                    ToolTip.Tip="Image (I)">
                <PathIcon Data="{StaticResource ToolIconImage}" Width="16" Height="16" />
            </Button>

            <Border Width="1" Height="20" Background="{DynamicResource BorderDefault}" Margin="4,0" />

            <!-- Utility -->
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsEraserToolActive}"
                    Command="{Binding EraserToolCommand}"
                    ToolTip.Tip="Eraser (X)">
                <PathIcon Data="{StaticResource ToolIconEraser}" Width="16" Height="16" />
            </Button>
            <Button Classes="whiteboard-float-tool-button"
                    Classes.active="{Binding IsLaserPointerToolActive}"
                    Command="{Binding LaserPointerToolCommand}"
                    ToolTip.Tip="Laser Pointer">
                <PathIcon Data="{StaticResource ToolIconLaser}" Width="16" Height="16" />
            </Button>
        </StackPanel>
    </Border>
</UserControl>
```

- [ ] **Step 4: Update `BoardScreen.axaml` layout**

In `src/BFGA.App/Views/BoardScreen.axaml`, move the toolbar from dock-left to grid overlay top-center. Replace the full file:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:views="clr-namespace:BFGA.App.Views"
             xmlns:vm="clr-namespace:BFGA.App.ViewModels"
             x:Class="BFGA.App.Views.BoardScreen"
             x:DataType="vm:BoardScreenViewModel"
             Classes="whiteboard-shell">
    <Grid>
        <!-- Canvas (fills entire area) -->
        <Border Classes="whiteboard-canvas-shell">
            <views:BoardView x:Name="boardView"
                             HorizontalAlignment="Stretch"
                             VerticalAlignment="Stretch"
                             Board="{Binding MainViewModel.Board}"
                             RemoteCursors="{Binding MainViewModel.RemoteCursors}"
                             RemoteStrokePreviews="{Binding MainViewModel.RemoteStrokePreviews}" />
        </Border>

        <!-- Floating toolbar — top center -->
        <views:ToolBar x:Name="toolBar"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Top"
                       Margin="0,12,0,0"
                       DataContext="{Binding}" />

        <!-- Bottom bar — center bottom -->
        <views:BottomBar x:Name="bottomBar"
                         HorizontalAlignment="Center"
                         VerticalAlignment="Bottom"
                         Margin="0,0,0,12"
                         DataContext="{Binding}" />

        <!-- Property panel — right center -->
        <views:PropertyPanel HorizontalAlignment="Right"
                             VerticalAlignment="Center"
                             Margin="0,0,12,0"
                             DataContext="{Binding}" />

        <!-- Roster — top right -->
        <views:RosterOverlay HorizontalAlignment="Right"
                             VerticalAlignment="Top"
                             Margin="0,56,12,0"
                             DataContext="{Binding}" />
    </Grid>
</UserControl>
```

Note: RosterOverlay margin top changed from 12 to 56 to avoid overlapping with the floating toolbar.

- [ ] **Step 5: Update WhiteboardTheme.axaml for horizontal toolbar**

In `src/BFGA.App/Styles/WhiteboardTheme.axaml`:

Remove or replace the old vertical toolbar styles (`whiteboard-toolbar`, `whiteboard-toolbar-panel`, `whiteboard-tool-button`) with:

```xml
<!-- Floating toolbar container -->
<Style Selector="UserControl.whiteboard-toolbar">
    <Setter Property="Width" Value="NaN" />
</Style>

<Style Selector="Border.whiteboard-floating-toolbar">
    <Setter Property="Background" Value="{DynamicResource BgElevated}" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="Padding" Value="4" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}" />
    <Setter Property="BorderThickness" Value="1" />
</Style>

<!-- Floating toolbar buttons (32x32) -->
<Style Selector="Button.whiteboard-float-tool-button">
    <Setter Property="Width" Value="32" />
    <Setter Property="Height" Value="32" />
    <Setter Property="Padding" Value="0" />
    <Setter Property="Margin" Value="0" />
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Foreground" Value="{DynamicResource TextSecondary}" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="CornerRadius" Value="6" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.15" />
            <BrushTransition Property="Foreground" Duration="0:0:0.15" />
        </Transitions>
    </Setter>
</Style>

<Style Selector="Button.whiteboard-float-tool-button:pointerover">
    <Setter Property="Background" Value="{DynamicResource BgOverlay}" />
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
</Style>

<Style Selector="Button.whiteboard-float-tool-button.active">
    <Setter Property="Background" Value="#1A1A2A" />
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
</Style>
```

Keep the old `whiteboard-tool-button` styles since they're still used by BottomBar buttons.

- [ ] **Step 6: Add keyboard shortcuts A, L, T**

In `src/BFGA.App/MainWindow.axaml.cs`, update `TryHandleToolShortcut` to add the new shortcuts:

```csharp
case Key.A:
    boardScreen.ArrowToolCommand.Execute(null);
    return true;
case Key.L:
    boardScreen.LineToolCommand.Execute(null);
    return true;
case Key.T:
    boardScreen.TextToolCommand.Execute(null);
    return true;
```

Add these before the `default:` case in the switch statement.

- [ ] **Step 7: Update `BoardScreenLayoutTests.cs`**

In `tests/BFGA.App.Tests/BoardScreenLayoutTests.cs`, method `BoardScreen_UsesWhiteboardShellLayoutAndShortcuts`:

**Remove** these assertions (layout changed from DockPanel to Grid):
```csharp
Assert.Contains("DockPanel", xaml);                    // line 26
Assert.Contains("DockPanel.Dock=\"Left\"", xaml);      // line 27
Assert.DoesNotContain("DockPanel.Dock=\"Bottom\"", xaml); // line 28
Assert.Contains("DockPanel.Dock=\"Left\"", xaml);      // line 35 (duplicate)
```

**Replace** with Grid-based assertions:
```csharp
Assert.Contains("Grid", xaml);
Assert.Contains("HorizontalAlignment=\"Center\"", xaml);
Assert.Contains("VerticalAlignment=\"Top\"", xaml);
```

**Update separator count** (lines 44-45) — new horizontal toolbar uses 4 `BorderDefault` separators instead of 2 `BorderSubtle` ones:
```csharp
// Old:
Assert.Equal(2, CountOccurrences(toolbarXaml, "Height=\"1\""));
Assert.Equal(2, CountOccurrences(toolbarXaml, "BorderSubtle"));
// New:
Assert.Equal(4, CountOccurrences(toolbarXaml, "BorderDefault"));
```

**Replace icon sequence** (lines 46-56) with new horizontal order:
```csharp
AssertSequence(
    toolbarXaml,
    "ToolIconSelect",
    "ToolIconHand",
    "BorderDefault",
    "ToolIconRectangle",
    "ToolIconEllipse",
    "ToolIconArrow",
    "ToolIconLine",
    "BorderDefault",
    "ToolIconPen",
    "BorderDefault",
    "ToolIconText",
    "ToolIconImage",
    "BorderDefault",
    "ToolIconEraser",
    "ToolIconLaser");
```

**Add** new tool active binding assertions (after existing ones at lines 57-63):
```csharp
Assert.Contains("Classes.active=\"{Binding IsArrowToolActive}\"", toolbarXaml);
Assert.Contains("Classes.active=\"{Binding IsLineToolActive}\"", toolbarXaml);
Assert.Contains("Classes.active=\"{Binding IsTextToolActive}\"", toolbarXaml);
Assert.Contains("Classes.active=\"{Binding IsLaserPointerToolActive}\"", toolbarXaml);
```

- [ ] **Step 8: Update `BoardScreenViewModelTests.cs`**

In `tests/BFGA.App.Tests/BoardScreenViewModelTests.cs`:

**In `SelectedTool_UpdatesComputedActiveStates`** — add 4 new `Assert.False` lines after line 19 (initial state) and after line 29 (Ellipse active state):

After the initial Assert.False block (line 19), add:
```csharp
Assert.False(sut.IsArrowToolActive);
Assert.False(sut.IsLineToolActive);
Assert.False(sut.IsTextToolActive);
Assert.False(sut.IsLaserPointerToolActive);
```

After the Ellipse-active Assert.False block (line 29), add:
```csharp
Assert.False(sut.IsArrowToolActive);
Assert.False(sut.IsLineToolActive);
Assert.False(sut.IsTextToolActive);
Assert.False(sut.IsLaserPointerToolActive);
```

**In `SelectedTool_RaisesNotificationsForAllActiveFlags`** — add 4 new notification assertions after line 50:
```csharp
Assert.Contains(nameof(BoardScreenViewModel.IsArrowToolActive), changed);
Assert.Contains(nameof(BoardScreenViewModel.IsLineToolActive), changed);
Assert.Contains(nameof(BoardScreenViewModel.IsTextToolActive), changed);
Assert.Contains(nameof(BoardScreenViewModel.IsLaserPointerToolActive), changed);
```

- [ ] **Step 9: Verify**

Run: `dotnet build`
Expected: Build succeeds.

Run: `dotnet test`
Expected: 248 tests pass.

**Known untested behaviors (per "skip heavy test additions" policy):** New keyboard shortcuts (A/L/T) in `MainWindowShortcutTests.cs` and new property-panel visibility semantics for Arrow/Line in `PropertyPanelTests.cs` are not updated. These are new features, not regressions of existing tests.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat(toolbar): horizontal floating toolbar with new tool types"
```

---

### Task 5: Background Grid Visibility Fix

**Problem:** Dot grid is nearly invisible (#1F1F1F on #0D0D0D = ~7% contrast).

**Files:**
- Modify: `src/BFGA.Canvas/Rendering/ThemeColors.cs:10` (change DotGrid to white)
- Modify: `src/BFGA.Canvas/BoardCanvas.cs:198-200` (add DotGridOpacity property, use in render)
- Modify: `src/BFGA.Canvas/BoardViewport.cs` (forward DotGridOpacity property)
- Modify: `src/BFGA.App/Views/BoardView.axaml.cs` (add DotGridOpacity styled property)
- Modify: `src/BFGA.App/Views/BoardScreen.axaml` (bind DotGridOpacity)

- [ ] **Step 1: Change ThemeColors.DotGrid to white**

In `src/BFGA.Canvas/Rendering/ThemeColors.cs`, line 10:

```csharp
public static readonly SKColor DotGrid = new(0xFF, 0xFF, 0xFF);
```

- [ ] **Step 2: Add `DotGridOpacity` to `BoardCanvas`**

In `src/BFGA.Canvas/BoardCanvas.cs`, add a property:

```csharp
private float _dotGridOpacity = 0.1f;

public float DotGridOpacity
{
    get => _dotGridOpacity;
    set
    {
        _dotGridOpacity = Math.Clamp(value, 0f, 0.3f);
        InvalidateVisual();
    }
}
```

In the render method (around line 200), change the dot drawing call to use opacity-adjusted color:

Old:
```csharp
DotGridHelper.DrawDots(canvas, visibleBounds, Vector2.Zero, 24f, ThemeColors.DotGrid, 1.25f, zoomScale);
```

New:
```csharp
var dotColor = new SKColor(ThemeColors.DotGrid.Red, ThemeColors.DotGrid.Green, ThemeColors.DotGrid.Blue, (byte)(_parent._dotGridOpacity * 255));
DotGridHelper.DrawDots(canvas, visibleBounds, Vector2.Zero, 24f, dotColor, 1.25f, zoomScale);
```

Note: `_parent` refers to the enclosing `BoardCanvas` — the `BoardDrawOperation` is a nested class. Check how the nested class accesses the parent. If it's passed in, add `_dotGridOpacity` as a captured field in the draw operation constructor.

- [ ] **Step 3: Forward `DotGridOpacity` through `BoardViewport`**

In `src/BFGA.Canvas/BoardViewport.cs`, add a styled property:

```csharp
public static readonly StyledProperty<float> DotGridOpacityProperty =
    AvaloniaProperty.Register<BoardViewport, float>(nameof(DotGridOpacity), 0.1f);

public float DotGridOpacity
{
    get => GetValue(DotGridOpacityProperty);
    set => SetValue(DotGridOpacityProperty, value);
}
```

In the static constructor, wire property changes to forward to `BoardCanvas`:

```csharp
DotGridOpacityProperty.Changed.AddClassHandler<BoardViewport>((vp, e) =>
{
    vp._canvas.DotGridOpacity = (float)e.NewValue!;
});
```

- [ ] **Step 4: Forward `DotGridOpacity` through `BoardView`**

In `src/BFGA.App/Views/BoardView.axaml.cs`, add a styled property:

```csharp
public static readonly StyledProperty<float> DotGridOpacityProperty =
    AvaloniaProperty.Register<BoardView, float>(nameof(DotGridOpacity), 0.1f);

public float DotGridOpacity
{
    get => GetValue(DotGridOpacityProperty);
    set => SetValue(DotGridOpacityProperty, value);
}
```

Forward changes to viewport:

```csharp
DotGridOpacityProperty.Changed.AddClassHandler<BoardView>((bv, e) =>
{
    bv.viewport.DotGridOpacity = (float)e.NewValue!;
});
```

Where `viewport` is the `BoardViewport` control inside `BoardView.axaml`.

- [ ] **Step 5: Bind `DotGridOpacity` in `BoardScreen.axaml`**

In `src/BFGA.App/Views/BoardScreen.axaml`, update the BoardView element to include the binding:

```xml
<views:BoardView x:Name="boardView"
                 HorizontalAlignment="Stretch"
                 VerticalAlignment="Stretch"
                 Board="{Binding MainViewModel.Board}"
                 RemoteCursors="{Binding MainViewModel.RemoteCursors}"
                 RemoteStrokePreviews="{Binding MainViewModel.RemoteStrokePreviews}"
                 DotGridOpacity="{Binding MainViewModel.GridOpacity}" />
```

Note: `MainViewModel.GridOpacity` will be added in Task 8 (Settings). For now, add a placeholder property to MainViewModel:

In `src/BFGA.App/ViewModels/MainViewModel.cs`, add:

```csharp
private float _gridOpacity = 0.1f;

public float GridOpacity
{
    get => _gridOpacity;
    set => SetProperty(ref _gridOpacity, Math.Clamp(value, 0f, 0.3f));
}
```

- [ ] **Step 6: Verify**

Run: `dotnet build`
Expected: Build succeeds.

Run: `dotnet test`
Expected: 248 tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "fix(grid): visible dot grid with configurable opacity"
```

---

### Task 6: Property Panel Text Boldness

**Problem:** Section headers are hard to read with light font.

**Files:**
- Modify: `src/BFGA.App/Views/PropertyPanel.axaml:9,33,41,49` (update header TextBlocks)

- [ ] **Step 1: Update section header font in PropertyPanel.axaml**

In `src/BFGA.App/Views/PropertyPanel.axaml`, for each section header TextBlock ("PROPERTIES", "STROKE", "WIDTH", "OPACITY", "FILL"), add or update:

```xml
FontFamily="{DynamicResource InterMediumFont}"
Foreground="{DynamicResource TextSecondary}"
```

Apply to: the "PROPERTIES" title, "STROKE" header, "WIDTH" header, "OPACITY" header, and "FILL" header. These are the TextBlock elements at approximately lines 9, 11, 33, 41, and ~50.

- [ ] **Step 2: Verify**

Run: `dotnet build src/BFGA.App/BFGA.App.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/BFGA.App/Views/PropertyPanel.axaml
git commit -m "style(panel): bold section headers with medium font"
```

---

### Task 7: Zoom Bar Fix

**Problem:** Undo/redo and zoom +/- use unicode text that looks small and misplaced.

**Files:**
- Modify: `src/BFGA.App/Views/BottomBar.axaml:14-33` (replace text content with PathIcon)

- [ ] **Step 1: Replace BottomBar content with SVG PathIcons**

In `src/BFGA.App/Views/BottomBar.axaml`, replace the undo/redo buttons and zoom +/- buttons to use PathIcon instead of text Content. Replace the full file:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:BFGA.App.ViewModels"
             x:Class="BFGA.App.Views.BottomBar"
             x:Name="root"
             x:DataType="vm:BoardScreenViewModel"
             Classes="whiteboard-bottom-bar">
    <Border Classes="whiteboard-bottom-bar-panel"
            HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,12">
        <StackPanel Orientation="Horizontal" Spacing="4" VerticalAlignment="Center">
            <!-- Undo -->
            <Button Command="{Binding MainViewModel.UndoCommand}"
                    IsEnabled="{Binding MainViewModel.CanUndo}"
                    Classes="whiteboard-tool-button"
                    Width="28" Height="28"
                    ToolTip.Tip="Undo (Ctrl+Z)">
                <PathIcon Data="{StaticResource IconUndo}" Width="14" Height="14" />
            </Button>
            <!-- Redo -->
            <Button Command="{Binding MainViewModel.RedoCommand}"
                    IsEnabled="{Binding MainViewModel.CanRedo}"
                    Classes="whiteboard-tool-button"
                    Width="28" Height="28"
                    ToolTip.Tip="Redo (Ctrl+Y)">
                <PathIcon Data="{StaticResource IconRedo}" Width="14" Height="14" />
            </Button>

            <!-- Divider -->
            <Border Width="1" Height="16" Background="{DynamicResource BorderDefault}" Margin="4,0" />

            <!-- Zoom out -->
            <Button Command="{Binding BoardView.ZoomOutCommand, ElementName=root}"
                    Classes="whiteboard-tool-button"
                    Width="28" Height="28"
                    ToolTip.Tip="Zoom out">
                <PathIcon Data="{StaticResource IconZoomOut}" Width="14" Height="14" />
            </Button>

            <!-- Zoom slider -->
            <Slider Minimum="0.2" Maximum="3"
                    Value="{Binding BoardView.ZoomLevel, ElementName=root, Mode=TwoWay}"
                    Width="140" TickFrequency="0.1" VerticalAlignment="Center" />

            <!-- Zoom label -->
            <TextBlock Text="{Binding BoardView.ZoomLabel, ElementName=root}"
                       FontFamily="{DynamicResource InterMediumFont}"
                       FontSize="12"
                       Foreground="{DynamicResource TextSecondary}"
                       VerticalAlignment="Center"
                       MinWidth="52" TextAlignment="Center" />

            <!-- Zoom in -->
            <Button Command="{Binding BoardView.ZoomInCommand, ElementName=root}"
                    Classes="whiteboard-tool-button"
                    Width="28" Height="28"
                    ToolTip.Tip="Zoom in">
                <PathIcon Data="{StaticResource IconZoomIn}" Width="14" Height="14" />
            </Button>
        </StackPanel>
    </Border>
</UserControl>
```

- [ ] **Step 2: Verify**

Run: `dotnet build src/BFGA.App/BFGA.App.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/BFGA.App/Views/BottomBar.axaml
git commit -m "style(zoom): SVG icons for undo/redo/zoom buttons"
```

---

### Task 8: Settings Service & Settings Panel

**Problem:** No way to configure app settings (grid opacity, language, image folder, autosave).

**Files:**
- Create: `src/BFGA.App/Services/SettingsService.cs` (JSON persistence + debounce)
- Create: `src/BFGA.App/Views/SettingsPanel.axaml` (overlay UI)
- Create: `src/BFGA.App/Views/SettingsPanel.axaml.cs` (code-behind)
- Modify: `src/BFGA.App/ViewModels/MainViewModel.cs` (integrate SettingsService, wire GridOpacity)
- Modify: `src/BFGA.App/Views/BoardScreen.axaml` (add SettingsPanel overlay)
- Modify: `src/BFGA.App/Styles/WhiteboardTheme.axaml` (settings panel styles)

- [ ] **Step 1: Create `SettingsService.cs`**

Create `src/BFGA.App/Services/SettingsService.cs`:

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BFGA.App.Services;

public sealed class SettingsService : IDisposable
{
    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BFGA");

    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

    private CancellationTokenSource? _debounceCts;

    public float GridOpacity { get; set; } = 0.1f;
    public string Language { get; set; } = "English";
    public string DefaultImageFolder { get; set; } = string.Empty;
    public bool AutosaveEnabled { get; set; } = true;
    public int AutosaveIntervalSeconds { get; set; } = 60;

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return;

            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data is null)
                return;

            GridOpacity = Math.Clamp(data.GridOpacity, 0f, 0.3f);
            Language = data.Language ?? "English";
            DefaultImageFolder = data.DefaultImageFolder ?? string.Empty;
            AutosaveEnabled = data.AutosaveEnabled;
            AutosaveIntervalSeconds = data.AutosaveIntervalSeconds > 0 ? data.AutosaveIntervalSeconds : 60;
        }
        catch
        {
            // Corrupt settings — use defaults
        }
    }

    public void SaveDebounced()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                SaveImmediate();
            }
            catch (OperationCanceledException)
            {
                // Debounced — new save pending
            }
        }, token);
    }

    public void SaveImmediate()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var data = new SettingsData
            {
                GridOpacity = GridOpacity,
                Language = Language,
                DefaultImageFolder = DefaultImageFolder,
                AutosaveEnabled = AutosaveEnabled,
                AutosaveIntervalSeconds = AutosaveIntervalSeconds
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Non-critical — settings just won't persist
        }
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }

    private sealed class SettingsData
    {
        public float GridOpacity { get; set; } = 0.1f;
        public string? Language { get; set; } = "English";
        public string? DefaultImageFolder { get; set; } = string.Empty;
        public bool AutosaveEnabled { get; set; } = true;
        public int AutosaveIntervalSeconds { get; set; } = 60;
    }
}
```

- [ ] **Step 2: Integrate `SettingsService` into `MainViewModel`**

In `src/BFGA.App/ViewModels/MainViewModel.cs`:

Add a field (among the other private fields):

```csharp
private readonly SettingsService _settingsService = new();
```

In the constructor, after field initialization, load settings and sync:

```csharp
_settingsService.Load();
_gridOpacity = _settingsService.GridOpacity;
```

Update the `GridOpacity` property setter (from Task 5) to also persist:

```csharp
public float GridOpacity
{
    get => _gridOpacity;
    set
    {
        if (SetProperty(ref _gridOpacity, Math.Clamp(value, 0f, 0.3f)))
        {
            _settingsService.GridOpacity = _gridOpacity;
            _settingsService.SaveDebounced();
        }
    }
}
```

In `Dispose()`, add `_settingsService.Dispose()` before the end.

- [ ] **Step 3: Create `SettingsPanel.axaml`**

Create `src/BFGA.App/Views/SettingsPanel.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:BFGA.App.ViewModels"
             x:Class="BFGA.App.Views.SettingsPanel"
             x:DataType="vm:BoardScreenViewModel"
             IsVisible="{Binding MainViewModel.IsSettingsOpen}">
    <Border Classes="settings-panel" Width="260" Padding="16">
        <StackPanel Spacing="16">
            <!-- Header -->
            <DockPanel>
                <TextBlock Text="SETTINGS"
                           DockPanel.Dock="Left"
                           FontFamily="{DynamicResource InterMediumFont}"
                           FontSize="11"
                           Foreground="{DynamicResource TextSecondary}"
                           VerticalAlignment="Center" />
                <Button DockPanel.Dock="Right"
                        HorizontalAlignment="Right"
                        Classes="whiteboard-tool-button"
                        Width="24" Height="24"
                        Command="{Binding ToggleSettingsCommand}"
                        ToolTip.Tip="Close">
                    <PathIcon Data="M18,6 L6,18 M6,6 L18,18" Width="12" Height="12" />
                </Button>
            </DockPanel>

            <!-- Grid Opacity -->
            <StackPanel Spacing="4">
                <DockPanel>
                    <TextBlock Text="GRID OPACITY"
                               FontFamily="{DynamicResource InterMediumFont}"
                               FontSize="10"
                               Foreground="{DynamicResource TextSecondary}" />
                    <TextBlock DockPanel.Dock="Right"
                               HorizontalAlignment="Right"
                               FontFamily="{DynamicResource InterMediumFont}"
                               FontSize="10"
                               Foreground="{DynamicResource TextTertiary}">
                        <TextBlock.Text>
                            <MultiBinding StringFormat="{}{0:0}%">
                                <Binding Path="MainViewModel.GridOpacityPercent" />
                            </MultiBinding>
                        </TextBlock.Text>
                    </TextBlock>
                </DockPanel>
                <Slider Minimum="0" Maximum="30"
                        Value="{Binding MainViewModel.GridOpacityPercent, Mode=TwoWay}"
                        TickFrequency="1" />
            </StackPanel>

            <Border Height="1" Background="{DynamicResource BorderSubtle}" />

            <!-- Language (placeholder) -->
            <StackPanel Spacing="4">
                <TextBlock Text="LANGUAGE"
                           FontFamily="{DynamicResource InterMediumFont}"
                           FontSize="10"
                           Foreground="{DynamicResource TextSecondary}" />
                <ComboBox SelectedIndex="0"
                          IsEnabled="False"
                          HorizontalAlignment="Stretch"
                          FontSize="12">
                    <ComboBoxItem Content="English" />
                </ComboBox>
            </StackPanel>

            <!-- Default Image Folder (placeholder) -->
            <StackPanel Spacing="4">
                <TextBlock Text="DEFAULT IMAGE FOLDER"
                           FontFamily="{DynamicResource InterMediumFont}"
                           FontSize="10"
                           Foreground="{DynamicResource TextSecondary}" />
                <DockPanel>
                    <Button DockPanel.Dock="Right" Content="Browse" IsEnabled="False"
                            Classes="whiteboard-tool-button" Height="28" Margin="4,0,0,0" />
                    <TextBox Watermark="Not configured" IsEnabled="False" FontSize="12" />
                </DockPanel>
            </StackPanel>

            <!-- Autosave (placeholder) -->
            <StackPanel Spacing="4">
                <TextBlock Text="AUTOSAVE"
                           FontFamily="{DynamicResource InterMediumFont}"
                           FontSize="10"
                           Foreground="{DynamicResource TextSecondary}" />
                <DockPanel>
                    <ToggleSwitch DockPanel.Dock="Left" IsChecked="True" IsEnabled="False" />
                    <ComboBox SelectedIndex="1" IsEnabled="False" HorizontalAlignment="Right" FontSize="12">
                        <ComboBoxItem Content="30s" />
                        <ComboBoxItem Content="60s" />
                        <ComboBoxItem Content="120s" />
                    </ComboBox>
                </DockPanel>
            </StackPanel>
        </StackPanel>
    </Border>
</UserControl>
```

- [ ] **Step 4: Create `SettingsPanel.axaml.cs`**

Create `src/BFGA.App/Views/SettingsPanel.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace BFGA.App.Views;

public partial class SettingsPanel : UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 5: Add settings-related properties to MainViewModel & BoardScreenViewModel**

In `src/BFGA.App/ViewModels/MainViewModel.cs`, add a percentage wrapper for the slider:

```csharp
public float GridOpacityPercent
{
    get => _gridOpacity * 100f;
    set
    {
        GridOpacity = value / 100f;
        OnPropertyChanged();
    }
}
```

Also update the `GridOpacity` setter to notify `GridOpacityPercent`:

```csharp
public float GridOpacity
{
    get => _gridOpacity;
    set
    {
        if (SetProperty(ref _gridOpacity, Math.Clamp(value, 0f, 0.3f)))
        {
            _settingsService.GridOpacity = _gridOpacity;
            _settingsService.SaveDebounced();
            OnPropertyChanged(nameof(GridOpacityPercent));
        }
    }
}
```

In `src/BFGA.App/ViewModels/BoardScreenViewModel.cs`, add a command to toggle settings:

```csharp
private readonly RelayCommand _toggleSettingsCommand;
```

In constructor:
```csharp
_toggleSettingsCommand = new RelayCommand(() => MainViewModel.IsSettingsOpen = !MainViewModel.IsSettingsOpen);
```

Public property:
```csharp
public RelayCommand ToggleSettingsCommand => _toggleSettingsCommand;
```

- [ ] **Step 6: Add SettingsPanel to BoardScreen.axaml**

In `src/BFGA.App/Views/BoardScreen.axaml`, add the SettingsPanel overlay inside the Grid (after PropertyPanel, before RosterOverlay):

```xml
<!-- Settings panel — right -->
<views:SettingsPanel HorizontalAlignment="Right"
                     VerticalAlignment="Top"
                     Margin="0,56,12,0"
                     DataContext="{Binding}" />
```

- [ ] **Step 7: Add settings panel style to WhiteboardTheme.axaml**

Add to `src/BFGA.App/Styles/WhiteboardTheme.axaml`:

```xml
<Style Selector="Border.settings-panel">
    <Setter Property="Background" Value="{DynamicResource BgElevated}" />
    <Setter Property="CornerRadius" Value="12" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}" />
    <Setter Property="BorderThickness" Value="1" />
</Style>
```

- [ ] **Step 8: Verify**

Run: `dotnet build`
Expected: Build succeeds.

Run: `dotnet test`
Expected: 248 tests pass.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(settings): settings panel with grid opacity control"
```

---

### Task 9: Final Verification & Build Check

- [ ] **Step 1: Full build**

Run: `dotnet build`
Expected: 0 errors, 0 warnings (or only pre-existing warnings).

- [ ] **Step 2: Full test suite**

Run: `dotnet test`
Expected: 248 tests pass, 0 failures.

- [ ] **Step 3: Mark plan complete**

All 8 implementation tasks are done. Mark all checkboxes in this plan as complete.

- [ ] **Step 4: Final commit (if any uncommitted changes)**

```bash
git status
# If any changes: git add -A && git commit -m "chore: final cleanup"
```
