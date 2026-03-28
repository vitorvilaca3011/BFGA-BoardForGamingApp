# UI Redesign & Feature Additions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Overhaul BFGA's visual design (editorial aesthetic, Swiss-design influence) and add property panel, player roster, undo/redo, and transitions.

**Architecture:** Incremental overlay — modify existing files, add new components, keep all 203 tests passing. Theme split into Colors.axaml + Typography.axaml + WhiteboardTheme.axaml. Undo/redo is host-authoritative with client shadow counters.

**Tech Stack:** .NET 9, Avalonia 11.3.12, SkiaSharp 3.116.1, MessagePack, xUnit

**Spec:** `docs/superpowers/specs/2026-03-28-ui-redesign-design.md`

**Reference docs:** `docs/AVALONIA_REFERENCE.md`, `docs/UI-REDESIGN-SPECIFICATION.md`, `docs/UI-IMPLEMENTATION-GUIDE.md`

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `src/BFGA.App/Styles/Colors.axaml` | All 11 color tokens as SolidColorBrush resources |
| `src/BFGA.App/Styles/Typography.axaml` | Font family definitions + text style selectors |
| `src/BFGA.App/Assets/Fonts/Inter-ExtraLight.ttf` | Inter 200 weight |
| `src/BFGA.App/Assets/Fonts/Inter-Light.ttf` | Inter 300 weight |
| `src/BFGA.App/Assets/Fonts/Inter-Regular.ttf` | Inter 400 weight |
| `src/BFGA.App/Assets/Fonts/Inter-Medium.ttf` | Inter 500 weight |
| `src/BFGA.App/Assets/Fonts/JetBrainsMono-Regular.ttf` | JetBrains Mono 400 |
| `src/BFGA.App/Views/PropertyPanel.axaml` + `.cs` | Tool property panel (color swatches, sliders) |
| `src/BFGA.App/Views/RosterOverlay.axaml` + `.cs` | Player roster avatar bubbles |
| `src/BFGA.Canvas/Rendering/ThemeColors.cs` | SKColor constants bridging AXAML tokens to SkiaSharp |
| `src/BFGA.Network/UndoRedoManager.cs` | Per-user undo/redo stack logic |
| `tests/BFGA.Network.Tests/UndoRedoManagerTests.cs` | Undo/redo unit tests |
| `tests/BFGA.App.Tests/PropertyPanelTests.cs` | Property panel visibility + defaults |
| `tests/BFGA.App.Tests/RosterOverlayTests.cs` | Roster initials, colors, rendering |

### Modified Files
| File | Changes |
|------|---------|
| `src/BFGA.App/App.axaml` | Load Colors.axaml, Typography.axaml, font resources |
| `src/BFGA.App/BFGA.App.csproj` | Add font files as AvaloniaResource |
| `src/BFGA.App/Styles/WhiteboardTheme.axaml` | Refactor to reference color tokens, add component styles |
| `src/BFGA.App/Views/ConnectionView.axaml` | Full editorial redesign |
| `src/BFGA.App/Views/BoardScreen.axaml` | Add PropertyPanel, RosterOverlay, float toolbar |
| `src/BFGA.App/Views/ToolBar.axaml` | Tool grouping, active state, dividers |
| `src/BFGA.App/Views/BottomBar.axaml` | Add undo/redo buttons, float centered |
| `src/BFGA.App/Views/BoardView.axaml.cs` | Pass tool properties to BoardToolController |
| `src/BFGA.App/ViewModels/BoardScreenViewModel.cs` | Add color/width/opacity properties |
| `src/BFGA.App/ViewModels/MainViewModel.cs` | Add UndoCommand, RedoCommand, CanUndo, CanRedo |
| `src/BFGA.App/MainWindow.axaml` | Add screen transition, remove outer margin |
| `src/BFGA.App/MainWindow.axaml.cs` | Add Ctrl+Z/Y keyboard shortcuts |
| `src/BFGA.App/Networking/IGameHostSession.cs` | Add TryUndo, TryRedo, CanUndo, CanRedo |
| `src/BFGA.App/Networking/NetworkGameSessionFactory.cs` | Delegate undo/redo to GameHost |
| `src/BFGA.Canvas/Rendering/ThemeColors.cs` | (new file) |
| `src/BFGA.Canvas/BoardCanvas.cs` | Use ThemeColors for background + dot grid |
| `src/BFGA.Canvas/Tools/BoardToolController.cs` | Accept tool properties for new elements |
| `src/BFGA.Network/Protocol/BoardOperation.cs` | Add UndoOperation, RedoOperation |
| `src/BFGA.Network/GameHost.cs` | Integrate UndoRedoManager, handle undo/redo ops |

---

## Task 1: Theme Foundation — Colors.axaml + ThemeColors.cs

**Files:**
- Create: `src/BFGA.App/Styles/Colors.axaml`
- Create: `src/BFGA.Canvas/Rendering/ThemeColors.cs`
- Modify: `src/BFGA.App/App.axaml`
- Modify: `src/BFGA.Canvas/BoardCanvas.cs:192-200`
- Modify: `src/BFGA.App/Styles/WhiteboardTheme.axaml`

- [x] **Step 1: Create Colors.axaml**

Create `src/BFGA.App/Styles/Colors.axaml` with all 11 color tokens:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="BgBase" Color="#0A0A0A" />
    <SolidColorBrush x:Key="BgSurface" Color="#0D0D0D" />
    <SolidColorBrush x:Key="BgElevated" Color="#111111" />
    <SolidColorBrush x:Key="BgOverlay" Color="#161616" />
    <SolidColorBrush x:Key="TextPrimary" Color="#FAFAFA" />
    <SolidColorBrush x:Key="TextSecondary" Color="#B0B0B0" />
    <SolidColorBrush x:Key="TextTertiary" Color="#666666" />
    <SolidColorBrush x:Key="TextMuted" Color="#404040" />
    <SolidColorBrush x:Key="BorderDefault" Color="#2A2A2A" />
    <SolidColorBrush x:Key="BorderSubtle" Color="#1A1A1A" />
    <SolidColorBrush x:Key="AccentWhite" Color="#FFFFFF" />
</ResourceDictionary>
```

- [x] **Step 2: Create ThemeColors.cs for SkiaSharp bridge**

Create `src/BFGA.Canvas/Rendering/ThemeColors.cs`:

```csharp
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public static class ThemeColors
{
    public static readonly SKColor BgBase = new(0x0A, 0x0A, 0x0A);
    public static readonly SKColor BgSurface = new(0x0D, 0x0D, 0x0D);
    public static readonly SKColor BgElevated = new(0x11, 0x11, 0x11);
    public static readonly SKColor DotGrid = new(0x1F, 0x1F, 0x1F);
    public static readonly SKColor TextPrimary = new(0xFA, 0xFA, 0xFA);
    public static readonly SKColor TextSecondary = new(0xB0, 0xB0, 0xB0);
    public static readonly SKColor BorderDefault = new(0x2A, 0x2A, 0x2A);
}
```

- [x] **Step 3: Update App.axaml to load Colors.axaml**

In `src/BFGA.App/App.axaml`, add Colors.axaml as a merged resource dictionary (NOT `StyleInclude` — `Colors.axaml` is a `ResourceDictionary`, not a `Styles` file). Add inside `Application.Resources`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceInclude Source="avares://BFGA.App/Assets/ToolIcons.axaml" />
            <ResourceInclude Source="avares://BFGA.App/Styles/Colors.axaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>

<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://BFGA.App/Styles/WhiteboardTheme.axaml" />
</Application.Styles>
```

**IMPORTANT:** The existing `ToolIcons.axaml` MUST be preserved in `MergedDictionaries` — it was already there before this change. The existing test `App_RegistersWhiteboardThemeAndToolIcons` in `BoardScreenLayoutTests.cs` asserts it is registered. Dropping it would break toolbar icons and tests.

Note: `ResourceInclude` (for `ResourceDictionary` files) goes inside `Application.Resources`, while `StyleInclude` (for `Styles` files) stays in `Application.Styles`. This is an important Avalonia distinction.

- [x] **Step 4: Update WhiteboardTheme.axaml to use color tokens**

Replace all hardcoded hex colors in `src/BFGA.App/Styles/WhiteboardTheme.axaml` with `DynamicResource` references to the tokens from Colors.axaml. For example:
- `#101214` bg → `{DynamicResource BgBase}`
- `#111318` canvas bg → `{DynamicResource BgSurface}`
- `#F4F6F8` foreground → `{DynamicResource TextPrimary}`
- `#2C313A` border → `{DynamicResource BorderDefault}`
- `#D7DCE3` status → `{DynamicResource TextSecondary}`
- `#9097A3` shortcut → `{DynamicResource TextTertiary}`

Remove the 4 inline SolidColorBrush resources (ToolBarBackground, ToolBarBorderBrush, ToolButtonBackground, ToolButtonForeground) and replace their usages with the new token references.

- [x] **Step 5: Update BoardScreenLayoutTests.cs assertions**

`tests/BFGA.App.Tests/BoardScreenLayoutTests.cs` has a test `WhiteboardTheme_UsesTypedBrushAndCornerRadiusResources` (lines 60-73) that asserts the existence of `ToolBarBackground`, `ToolBarBorderBrush`, `ToolButtonBackground`, and `ToolButtonForeground` as `<SolidColorBrush>` resources in `WhiteboardTheme.axaml`. Since Step 4 removes these four resources (replaced by Colors.axaml tokens), **these assertions will fail**.

Update `WhiteboardTheme_UsesTypedBrushAndCornerRadiusResources` to assert the NEW resource names from `Colors.axaml` instead. Since `Colors.axaml` is a separate file, either:
- (a) Change the test to read `Colors.axaml` and assert the new token names (`BgBase`, `BgSurface`, `TextPrimary`, `BorderDefault`, etc.), OR
- (b) Keep the test reading `WhiteboardTheme.axaml` and assert it references the tokens via `DynamicResource` (e.g., `Assert.Contains("DynamicResource BgBase", themeXaml)`)

Option (b) is recommended — it validates that `WhiteboardTheme.axaml` actually uses the centralized tokens:

```csharp
[Fact]
public void WhiteboardTheme_UsesColorTokenReferences()
{
    var themeXaml = File.ReadAllText(...);
    
    // Old inline resources should be GONE
    Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolBarBackground\"", themeXaml);
    Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolBarBorderBrush\"", themeXaml);
    Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolButtonBackground\"", themeXaml);
    Assert.DoesNotContain("<SolidColorBrush x:Key=\"ToolButtonForeground\"", themeXaml);
    
    // Should reference centralized tokens
    Assert.Contains("DynamicResource BgBase", themeXaml);
    Assert.Contains("DynamicResource BgSurface", themeXaml);
    Assert.Contains("DynamicResource TextPrimary", themeXaml);
    Assert.Contains("DynamicResource BorderDefault", themeXaml);
    
    // PanelCornerRadius may still exist in WhiteboardTheme (it's a layout constant, not a color)
    Assert.Contains("<CornerRadius x:Key=\"PanelCornerRadius\"", themeXaml);
}
```

Also update `BoardScreen_UsesWhiteboardShellLayoutAndShortcuts` (line 29-31): it asserts `ToolBarBackground`, `ToolButtonSize`, and `PanelCornerRadius` exist in the theme. Replace ONLY the `ToolBarBackground` assertion with a token reference check. Keep `ToolButtonSize` and `PanelCornerRadius` assertions as-is — they are layout constants that remain in `WhiteboardTheme.axaml`, not color tokens being moved to `Colors.axaml`.

- [x] **Step 6: Update BoardCanvas.cs to use ThemeColors**

In `src/BFGA.Canvas/BoardCanvas.cs` lines 192-200, replace:
- `new SKColor(17, 19, 24)` → `ThemeColors.BgSurface`
- `new SKColor(140, 150, 165, 120)` → `ThemeColors.DotGrid`

- [x] **Step 7: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: 0 warnings, 0 errors, 203 tests pass (with updated assertions in BoardScreenLayoutTests).

- [x] **Step 8: Commit**

```
git add -A && git commit -m "feat(theme): create Colors.axaml + ThemeColors.cs color system

Split hardcoded colors into centralized token resources (Colors.axaml)
and SkiaSharp bridge (ThemeColors.cs). Update WhiteboardTheme.axaml and
BoardCanvas.cs to reference tokens instead of hex literals."
```

---

## Task 2: Typography — Fonts + Typography.axaml

**Files:**
- Create: `src/BFGA.App/Assets/Fonts/Inter-ExtraLight.ttf` (download)
- Create: `src/BFGA.App/Assets/Fonts/Inter-Light.ttf` (download)
- Create: `src/BFGA.App/Assets/Fonts/Inter-Regular.ttf` (download)
- Create: `src/BFGA.App/Assets/Fonts/Inter-Medium.ttf` (download)
- Create: `src/BFGA.App/Assets/Fonts/JetBrainsMono-Regular.ttf` (download)
- Create: `src/BFGA.App/Styles/Typography.axaml`
- Modify: `src/BFGA.App/BFGA.App.csproj`
- Modify: `src/BFGA.App/App.axaml`

- [x] **Step 1: Download font files**

Download Inter (ExtraLight 200, Light 300, Regular 400, Medium 500) and JetBrains Mono (Regular 400) `.ttf` files. Place in `src/BFGA.App/Assets/Fonts/`. These are open-source fonts (OFL license).

- [x] **Step 2: Add font files as AvaloniaResource in csproj**

In `src/BFGA.App/BFGA.App.csproj`, add:

```xml
<ItemGroup>
    <AvaloniaResource Include="Assets\Fonts\*.ttf" />
</ItemGroup>
```

- [x] **Step 3: Create Typography.axaml**

Create `src/BFGA.App/Styles/Typography.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Font family definitions -->
    <FontFamily x:Key="InterFont">avares://BFGA.App/Assets/Fonts#Inter</FontFamily>
    <FontFamily x:Key="MonoFont">avares://BFGA.App/Assets/Fonts#JetBrains Mono</FontFamily>
</ResourceDictionary>
```

- [x] **Step 4: Update App.axaml to load Typography.axaml**

Add Typography.axaml as another merged resource dictionary (like Colors.axaml, it's a `ResourceDictionary`):

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceInclude Source="avares://BFGA.App/Assets/ToolIcons.axaml" />
    <ResourceInclude Source="avares://BFGA.App/Styles/Colors.axaml" />
    <ResourceInclude Source="avares://BFGA.App/Styles/Typography.axaml" />
</ResourceDictionary.MergedDictionaries>
```

**IMPORTANT:** Keep `ToolIcons.axaml` as the first entry — it was already in the MergedDictionaries before Tasks 1-2.

- [x] **Step 5: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: 0 warnings, 0 errors, 203 tests pass.

- [x] **Step 6: Commit**

```
git add -A && git commit -m "feat(theme): add Inter + JetBrains Mono fonts and Typography.axaml

Bundle Inter (200-500) and JetBrains Mono (400) as AvaloniaResource.
Define InterFont and MonoFont FontFamily resources."
```

---

## Task 3: Connection Screen Editorial Redesign

**Files:**
- Modify: `src/BFGA.App/Views/ConnectionView.axaml` (full rewrite of AXAML)
- Modify: `src/BFGA.App/Views/ConnectionScreen.axaml` (if needed for background)
- Modify: `src/BFGA.App/MainWindow.axaml` (remove 12px outer margin)

- [x] **Step 1: Remove MainWindow outer margin**

In `src/BFGA.App/MainWindow.axaml`, change the root Grid `Margin="12"` to `Margin="0"`. The connection screen needs full-bleed background.

- [x] **Step 2: Redesign ConnectionView.axaml**

Rewrite `src/BFGA.App/Views/ConnectionView.axaml` with the editorial layout from spec §2.

Because compiled bindings are enabled project-wide, preserve the typed root header when rewriting this file:
- keep `xmlns:vm="clr-namespace:BFGA.App.ViewModels"`
- keep `x:DataType="vm:MainViewModel"`

Key layout structure:

```xml
<UserControl ...
             xmlns:vm="clr-namespace:BFGA.App.ViewModels"
             x:DataType="vm:MainViewModel">
  <Grid Background="{DynamicResource BgBase}">
    <!-- Section label "01 / CONNECTION" top-right -->
    <TextBlock Text="01 / CONNECTION"
               HorizontalAlignment="Right" VerticalAlignment="Top"
               Margin="0,32,32,0"
               FontFamily="{DynamicResource InterFont}" FontSize="11" FontWeight="Light"
               Foreground="{DynamicResource TextMuted}" LetterSpacing="3" />

    <!-- Vertical "BOARD" text bottom-left -->
    <TextBlock Text="B&#x0a;O&#x0a;A&#x0a;R&#x0a;D"
               HorizontalAlignment="Left" VerticalAlignment="Bottom"
               Margin="32,0,0,32"
               FontFamily="{DynamicResource InterFont}" FontSize="11" FontWeight="Light"
               Foreground="{DynamicResource TextMuted}" LetterSpacing="3" />

    <!-- Center card -->
    <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"
                Width="380" Spacing="32">

      <!-- Decorative circles -->
      <Canvas HorizontalAlignment="Center" Height="40" Width="100">
        <Ellipse Width="40" Height="40" Stroke="{DynamicResource BorderDefault}" StrokeThickness="1" Canvas.Left="0" />
        <Ellipse Width="40" Height="40" Stroke="{DynamicResource BorderDefault}" StrokeThickness="1" Canvas.Left="60" />
      </Canvas>

      <!-- Spaced logo -->
      <TextBlock Text="B  F  G  A"
                 HorizontalAlignment="Center"
                 FontFamily="{DynamicResource InterFont}" FontSize="28" FontWeight="ExtraLight"
                 Foreground="{DynamicResource TextPrimary}" LetterSpacing="12" />

      <!-- HOST / JOIN tab switcher -->
      <Grid ColumnDefinitions="*,*" HorizontalAlignment="Stretch">
        <Button Grid.Column="0" Content="HOST"
                Command="{Binding SetHostModeCommand}"
                Classes="connection-tab"
                Classes.active="{Binding IsHostMode}" />
        <Button Grid.Column="1" Content="JOIN"
                Command="{Binding SetJoinModeCommand}"
                Classes="connection-tab"
                Classes.active="{Binding IsJoinMode}" />
      </Grid>

      <!-- Form fields -->
      <StackPanel Spacing="16">
        <StackPanel Spacing="4">
          <TextBlock Text="DISPLAY NAME" Classes="input-label" />
          <TextBox Text="{Binding DisplayName}" Classes="connection-input" />
        </StackPanel>

        <!-- Host address: only in Join mode -->
        <StackPanel Spacing="4" IsVisible="{Binding IsJoinMode}">
          <TextBlock Text="HOST ADDRESS" Classes="input-label" />
          <TextBox Text="{Binding HostAddress}" Classes="connection-input" />
        </StackPanel>

        <StackPanel Spacing="4">
          <TextBlock Text="PORT" Classes="input-label" />
          <TextBox Text="{Binding HostPort}" Classes="connection-input" />
        </StackPanel>
      </StackPanel>

      <!-- Context-aware action button -->
      <!-- See spec §2.3 for state machine -->
      <Button Classes="connection-primary-btn"
              Content="{Binding PrimaryButtonText}"
              Command="{Binding PrimaryActionCommand}"
              IsEnabled="{Binding IsPrimaryButtonEnabled}" />

      <!-- Status text -->
      <TextBlock Text="{Binding StatusText}"
                 HorizontalAlignment="Center"
                 Foreground="{DynamicResource TextTertiary}"
                 FontFamily="{DynamicResource InterFont}" FontSize="12" />

      <!-- Secondary: Save/Load -->
      <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Spacing="16"
                  IsVisible="{Binding IsDisconnected}">
        <Button Content="LOAD BOARD" Command="{Binding LoadBoardCommand}" Classes="connection-secondary-btn" />
        <Button Content="SAVE BOARD" Command="{Binding SaveBoardCommand}" Classes="connection-secondary-btn" />
      </StackPanel>
    </StackPanel>
  </Grid>
</UserControl>
```

- [x] **Step 3: Add connection screen styles to WhiteboardTheme.axaml**

Add style selectors for the new classes: `connection-tab`, `connection-tab.active`, `input-label`, `connection-input`, `connection-primary-btn`, `connection-secondary-btn`. These use the color tokens from Colors.axaml and font families from Typography.axaml.

Key styles:
- `connection-tab`: Transparent bg, TextSecondary foreground, uppercase, Inter Medium 13px
- `connection-tab.active`: BorderSubtle bottom border, TextPrimary foreground
- `input-label`: MonoFont, 10px, uppercase, TextTertiary
- `connection-input`: BgSurface bg, BorderDefault border, TextPrimary foreground, Inter 14px
- `connection-primary-btn`: AccentWhite bg, BgBase foreground, Inter Medium 13px, full width
- `connection-secondary-btn`: Transparent bg, BorderDefault border, TextSecondary foreground

- [x] **Step 4: Add ViewModel support for tab switcher + context-aware button**

In `src/BFGA.App/ViewModels/MainViewModel.cs`, add computed properties:
- `IsHostMode` / `IsJoinMode` — derived from existing `SelectedMode`
- `SetHostModeCommand` / `SetJoinModeCommand` — set `SelectedMode`
- `PrimaryButtonText` — returns "START HOST" / "STOP HOST" / "CONNECT" / "CONNECTING..." based on `ConnectionState` + `SelectedMode`
- `PrimaryActionCommand` — routes to StartHostCommand / StopHostCommand / ConnectCommand based on state
- `IsPrimaryButtonEnabled` — false when Joining
- `IsDisconnected` — `ConnectionState == Disconnected`

These are purely computed wrappers around existing state — no new business logic.

**IMPORTANT — PropertyChanged notifications:** All of these computed properties depend on `SelectedMode` and/or `ConnectionState`. You MUST add `OnPropertyChanged` calls for them in the existing property setters:

In the `SelectedMode` setter (currently notifies `IsHostModeSelected` / `IsJoinModeSelected` at line ~106-107), add:
```csharp
OnPropertyChanged(nameof(IsHostMode));
OnPropertyChanged(nameof(IsJoinMode));
OnPropertyChanged(nameof(PrimaryButtonText));
OnPropertyChanged(nameof(PrimaryActionCommand));
OnPropertyChanged(nameof(IsPrimaryButtonEnabled));
```

In the `ConnectionState` setter (currently notifies `CanLoadBoard` / `CanSaveBoard` / `CurrentScreen` at line ~146-148), add:
```csharp
OnPropertyChanged(nameof(IsDisconnected));
OnPropertyChanged(nameof(PrimaryButtonText));
OnPropertyChanged(nameof(PrimaryActionCommand));
OnPropertyChanged(nameof(IsPrimaryButtonEnabled));
```

Without these, the tab active state and primary button text/command will go stale at runtime.

- [x] **Step 5: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: 0 warnings, 0 errors, 203+ tests pass.

- [x] **Step 6: Commit**

```
git add -A && git commit -m "feat(ui): redesign connection screen with editorial layout

Replace utilitarian form with centered card, HOST/JOIN tab switcher,
decorative elements, context-aware single action button, Inter typography.
Save/Load kept as secondary buttons."
```

---

## Task 4: Toolbar Redesign — Grouping + Active State

**Files:**
- Modify: `src/BFGA.App/Views/ToolBar.axaml`
- Modify: `src/BFGA.App/Styles/WhiteboardTheme.axaml` (tool button states)
- Modify: `src/BFGA.App/Views/ToolBar.axaml.cs` (if binding converter needed)

- [x] **Step 1: Add tool grouping dividers to ToolBar.axaml**

Rewrite `src/BFGA.App/Views/ToolBar.axaml` to group tools with dividers:
- Group 1: Select, Hand
- Divider (1px horizontal line, `BorderSubtle`)
- Group 2: Pen, Rectangle, Ellipse
- Divider
- Group 3: Image, Eraser

Use a `StackPanel` with `Border Height="1"` as dividers between groups.

Because compiled bindings are enabled project-wide, preserve the existing typed root header on `ToolBar.axaml` while rewriting it:
- keep `xmlns:vm="clr-namespace:BFGA.App.ViewModels"`
- keep `x:DataType="vm:BoardScreenViewModel"`
- keep the root `Classes="whiteboard-toolbar"`

- [x] **Step 2: Add active-state styles**

In `src/BFGA.App/Styles/WhiteboardTheme.axaml`, update the `whiteboard-tool-button` style:

```xml
<!-- Default state -->
<Style Selector="Button.whiteboard-tool-button">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Foreground" Value="{DynamicResource TextSecondary}" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="Width" Value="48" />
    <Setter Property="Height" Value="48" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="Padding" Value="0" />
    <Style.Animations>
        <Animation Duration="0:0:0.15">
            <KeyFrame Cue="0%"><Setter Property="Background" Value="Transparent" /></KeyFrame>
        </Animation>
    </Style.Animations>
</Style>

<!-- Hover -->
<Style Selector="Button.whiteboard-tool-button:pointerover">
    <Setter Property="Background" Value="{DynamicResource BgOverlay}" />
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
</Style>

<!-- Active (selected tool) -->
<Style Selector="Button.whiteboard-tool-button.active">
    <Setter Property="Background" Value="{DynamicResource BorderSubtle}" />
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
    <Setter Property="BorderBrush" Value="{DynamicResource AccentWhite}" />
    <Setter Property="BorderThickness" Value="2,0,0,0" />
</Style>
```

- [x] **Step 3: Bind active class to SelectedTool**

Each tool button needs `Classes.active="{Binding IsSelectToolActive}"` (etc.) bound to computed booleans in `BoardScreenViewModel`. Add 7 computed `bool` properties:
- `IsSelectToolActive`, `IsHandToolActive`, `IsPenToolActive`, `IsRectangleToolActive`, `IsEllipseToolActive`, `IsImageToolActive`, `IsEraserToolActive`
- Each returns `SelectedTool == BoardToolType.X`
- All fire `OnPropertyChanged` when `SelectedTool` changes

- [x] **Step 4: Vertically center the toolbar**

In `src/BFGA.App/Views/BoardScreen.axaml`, update the ToolBar dock to use `VerticalAlignment="Center"` within a left-docked container.

- [x] **Step 5: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: 0 warnings, 0 errors, 203+ tests pass.

- [x] **Step 6: Commit**

```
git add -A && git commit -m "feat(ui): redesign toolbar with grouping, active states, hover

Add tool group dividers, active tool left-border accent, hover/pressed
states, vertically centered positioning."
```

---

## Task 5: Property Panel — Color, Width, Opacity

**Files:**
- Create: `src/BFGA.App/Views/PropertyPanel.axaml` + `.cs`
- Create: `tests/BFGA.App.Tests/PropertyPanelTests.cs`
- Modify: `src/BFGA.App/ViewModels/BoardScreenViewModel.cs`
- Modify: `src/BFGA.App/Views/BoardScreen.axaml`
- Modify: `src/BFGA.App/Views/BoardView.axaml.cs`
- Modify: `src/BFGA.Canvas/Tools/BoardToolController.cs`
- Modify: `tests/BFGA.App.Tests/BoardScreenLayoutTests.cs`

- [x] **Step 1: Write failing tests for property panel visibility**

Create `tests/BFGA.App.Tests/PropertyPanelTests.cs`:

```csharp
public class PropertyPanelTests
{
    // NOTE: BoardScreenViewModel requires a MainViewModel in its constructor.
    // Tests should create a MainViewModel first (parameterless ctor for tests)
    // then pass it: new BoardScreenViewModel(new MainViewModel())
    private static BoardScreenViewModel CreateVm()
        => new(new MainViewModel());

    [Theory]
    [InlineData(BoardToolType.Pen, true)]
    [InlineData(BoardToolType.Rectangle, true)]
    [InlineData(BoardToolType.Ellipse, true)]
    [InlineData(BoardToolType.Select, false)]
    [InlineData(BoardToolType.Hand, false)]
    [InlineData(BoardToolType.Image, false)]
    [InlineData(BoardToolType.Eraser, false)]
    public void IsPropertyPanelVisible_DependsOnTool(BoardToolType tool, bool expected)
    {
        var vm = CreateVm();
        vm.SelectedTool = tool;
        Assert.Equal(expected, vm.IsPropertyPanelVisible);
    }

    [Fact]
    public void DefaultStrokeColor_IsWhite()
    {
        var vm = CreateVm();
        Assert.Equal(SKColors.White, vm.SelectedStrokeColor);
    }

    [Fact]
    public void DefaultStrokeWidth_Is2()
    {
        var vm = CreateVm();
        Assert.Equal(2f, vm.StrokeWidth);
    }

    [Fact]
    public void DefaultOpacity_Is1()
    {
        var vm = CreateVm();
        Assert.Equal(1f, vm.Opacity);
    }

    [Fact]
    public void DefaultFillColor_IsTransparent()
    {
        var vm = CreateVm();
        Assert.Equal(SKColors.Transparent, vm.SelectedFillColor);
    }

    [Fact]
    public void ShowsFillSection_OnlyForShapeTools()
    {
        var vm = CreateVm();
        vm.SelectedTool = BoardToolType.Pen;
        Assert.False(vm.ShowFillSection);

        vm.SelectedTool = BoardToolType.Rectangle;
        Assert.True(vm.ShowFillSection);

        vm.SelectedTool = BoardToolType.Ellipse;
        Assert.True(vm.ShowFillSection);
    }
}
```

- [x] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~PropertyPanelTests" -v n`
Expected: FAIL — properties don't exist yet.

- [x] **Step 3: Add properties to BoardScreenViewModel**

In `src/BFGA.App/ViewModels/BoardScreenViewModel.cs`, add:

```csharp
private SKColor _selectedStrokeColor = SKColors.White;
public SKColor SelectedStrokeColor
{
    get => _selectedStrokeColor;
    set => SetProperty(ref _selectedStrokeColor, value);
}

private SKColor _selectedFillColor = SKColors.Transparent;
public SKColor SelectedFillColor
{
    get => _selectedFillColor;
    set => SetProperty(ref _selectedFillColor, value);
}

private float _strokeWidth = 2f;
public float StrokeWidth
{
    get => _strokeWidth;
    set => SetProperty(ref _strokeWidth, value);
}

private float _opacity = 1f;
public float Opacity
{
    get => _opacity;
    set
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        SetProperty(ref _opacity, clamped);
    }
}

public bool IsPropertyPanelVisible => SelectedTool is BoardToolType.Pen
    or BoardToolType.Rectangle or BoardToolType.Ellipse;

public bool ShowFillSection => SelectedTool is BoardToolType.Rectangle
    or BoardToolType.Ellipse;
```

Fire `OnPropertyChanged(nameof(IsPropertyPanelVisible))` and `OnPropertyChanged(nameof(ShowFillSection))` when `SelectedTool` changes.

- [x] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~PropertyPanelTests" -v n`
Expected: PASS.

- [x] **Step 5: Create PropertyPanel.axaml + .cs**

Create `src/BFGA.App/Views/PropertyPanel.axaml` and `src/BFGA.App/Views/PropertyPanel.axaml.cs`.

Because compiled bindings are enabled project-wide, make the AXAML contract explicit:
- add `xmlns:vm="clr-namespace:BFGA.App.ViewModels"`
- set `x:DataType="vm:BoardScreenViewModel"` on the root `UserControl`
- keep `DataContext` inherited from `BoardScreen.axaml`

Use explicit button interactions for the swatches so the plan is literal. Add click handlers in `PropertyPanel.axaml.cs`:
- `OnStrokeSwatchClick(object? sender, RoutedEventArgs e)` → reads the clicked button's `Tag` hex string (for example `"#FFFFFF"`) and sets `((BoardScreenViewModel)DataContext!).SelectedStrokeColor`
- `OnFillSwatchClick(object? sender, RoutedEventArgs e)` → same for `SelectedFillColor`

Implement a small helper in the code-behind:

```csharp
private static SKColor ParseSkColor(object? tag)
    => tag is string hex ? SKColor.Parse(hex) : SKColors.Transparent;
```

Example structure:

```xml
<UserControl ...
             xmlns:vm="clr-namespace:BFGA.App.ViewModels"
             x:DataType="vm:BoardScreenViewModel"
    IsVisible="{Binding IsPropertyPanelVisible}">
    <Border Classes="property-panel" Width="220" Padding="16">
        <StackPanel Spacing="20">
            <TextBlock Text="PROPERTIES" Classes="input-label" />

            <StackPanel Spacing="8">
                <TextBlock Text="STROKE" Classes="input-label" />
                <WrapPanel ItemWidth="24" ItemHeight="24">
                    <Button Tag="#FFFFFF" Click="OnStrokeSwatchClick"
                            Classes="color-swatch" Background="#FFFFFF" />
                    <Button Tag="#000000" Click="OnStrokeSwatchClick"
                            Classes="color-swatch" Background="#000000" />
                    <!-- add the remaining 14 stroke swatch buttons here, each with Tag + Click -->
                </WrapPanel>
            </StackPanel>

            <!-- WIDTH slider -->
            <StackPanel Spacing="4">
                <TextBlock Text="WIDTH" Classes="input-label" />
                <Slider Minimum="1" Maximum="20" Value="{Binding StrokeWidth}" />
                <TextBlock Text="{Binding StrokeWidth, StringFormat='{}{0:F0}px'}"
                           Foreground="{DynamicResource TextTertiary}" FontSize="11" />
            </StackPanel>

            <!-- OPACITY slider -->
            <StackPanel Spacing="4">
                <TextBlock Text="OPACITY" Classes="input-label" />
                <Slider Minimum="0" Maximum="1" Value="{Binding Opacity}"
                        TickFrequency="0.01" IsSnapToTickEnabled="True" />
                <TextBlock Text="{Binding Opacity, StringFormat='{}{0:P0}'}"
                           Foreground="{DynamicResource TextTertiary}" FontSize="11" />
            </StackPanel>

            <!-- FILL section (shapes only) -->
            <StackPanel Spacing="8" IsVisible="{Binding ShowFillSection}">
                <TextBlock Text="FILL" Classes="input-label" />
                <WrapPanel ItemWidth="24" ItemHeight="24">
                    <Button Tag="#00000000" Click="OnFillSwatchClick"
                            Classes="color-swatch transparent-swatch" />
                    <Button Tag="#FFFFFF" Click="OnFillSwatchClick"
                            Classes="color-swatch" Background="#FFFFFF" />
                    <!-- add the remaining 14 fill swatch buttons here, each with Tag + Click -->
                </WrapPanel>
            </StackPanel>
        </StackPanel>
    </Border>
</UserControl>
```

And in `PropertyPanel.axaml.cs` implement the handlers against `BoardScreenViewModel` so the swatches actually change the selected colors. Every swatch button must carry `Classes="color-swatch"` so Task 11's hover animation applies, and each button must have an explicit visual fill (`Background="#..."`). For the transparent fill swatch, add a special class like `transparent-swatch` and style it in `WhiteboardTheme.axaml` with an outline/checker treatment so it does not render as a blank button.

- [x] **Step 6: Add PropertyPanel to BoardScreen.axaml**

In `src/BFGA.App/Views/BoardScreen.axaml`, keep the shell as a left-docked toolbar plus a center overlay grid. Move `BottomBar` out of the `DockPanel.Dock="Bottom"` slot so it can float bottom-center inside the board area, matching the approved spec.

**Preserve the current root contract:** keep `xmlns:vm="clr-namespace:BFGA.App.ViewModels"`, keep `x:DataType="vm:BoardScreenViewModel"`, and keep `Classes="whiteboard-shell"` on the root `UserControl`. Compiled bindings rely on the typed root, and `BoardScreenLayoutTests.cs` currently asserts the shell class.

**Preserve the current code-behind contract:** `BoardScreen.axaml.cs` expects named fields `boardView` and `bottomBar`, then wires `bottomBar.BoardView = boardView` in `OnAttachedToVisualTree()`. Keep `<views:BoardView x:Name="boardView" ... />` and `<views:BottomBar x:Name="bottomBar" ... />` in the rewritten XAML.

**Preserve the live board bindings verbatim:** the rewritten `<views:BoardView>` must still bind
- `Board="{Binding MainViewModel.Board}"`
- `RemoteCursors="{Binding MainViewModel.RemoteCursors}"`
- `RemoteStrokePreviews="{Binding MainViewModel.RemoteStrokePreviews}"`

These existing bindings are covered by `MainViewModelTests` and are required for board rendering plus remote cursor/stroke overlay updates.

```xml
<DockPanel>
    <views:ToolBar DockPanel.Dock="Left" VerticalAlignment="Center" DataContext="{Binding}" />
    <Grid> <!-- center area -->
        <Border Classes="whiteboard-canvas-shell">
            <views:BoardView x:Name="boardView"
                             HorizontalAlignment="Stretch"
                             VerticalAlignment="Stretch"
                             Board="{Binding MainViewModel.Board}"
                             RemoteCursors="{Binding MainViewModel.RemoteCursors}"
                             RemoteStrokePreviews="{Binding MainViewModel.RemoteStrokePreviews}" />
        </Border>
        <views:BottomBar x:Name="bottomBar"
                         HorizontalAlignment="Center" VerticalAlignment="Bottom"
                         Margin="0,0,0,12"
                         DataContext="{Binding}" />
        <views:PropertyPanel HorizontalAlignment="Right" VerticalAlignment="Center"
                             Margin="0,0,12,0"
                             DataContext="{Binding}" />
    </Grid>
</DockPanel>
```

Update `tests/BFGA.App.Tests/BoardScreenLayoutTests.cs` for the new layout contract: remove the old assertion that `BoardScreen.axaml` contains `DockPanel.Dock="Bottom"`, and replace it with checks that the file still contains `x:Name="bottomBar"`, still contains `views:BottomBar`, and places the bottom bar in the center overlay Grid rather than as a docked sibling.

- [x] **Step 7: Wire tool properties to BoardToolController**

In `src/BFGA.Canvas/Tools/BoardToolController.cs`, add a method or properties to accept tool defaults:

```csharp
public SKColor StrokeColor { get; set; } = SKColors.White;
public SKColor FillColor { get; set; } = SKColors.Transparent;
public float StrokeWidth { get; set; } = 2f;
public float Opacity { get; set; } = 1f;
```

In `HandlePenDown` (line ~230), use these instead of hardcoded `SKColors.Black` / `2f`:
```csharp
Color = ApplyOpacity(StrokeColor, Opacity),
Thickness = StrokeWidth,
```

In `HandleShapeDown` (line ~275), similarly use the properties.

Add helper:
```csharp
private static SKColor ApplyOpacity(SKColor color, float opacity)
    => color.WithAlpha((byte)(opacity * 255));
```

In `src/BFGA.App/Views/BoardView.axaml.cs`, before calling tool controller methods, sync the properties from the ViewModel:
```csharp
_toolController.StrokeColor = boardScreenVm.SelectedStrokeColor;
_toolController.FillColor = boardScreenVm.SelectedFillColor;
_toolController.StrokeWidth = boardScreenVm.StrokeWidth;
_toolController.Opacity = boardScreenVm.Opacity;
```

- [x] **Step 8: Add property-panel style to WhiteboardTheme.axaml**

```xml
<Style Selector="Border.property-panel">
    <Setter Property="Background" Value="{DynamicResource BgElevated}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="12" />
</Style>
```

- [x] **Step 9: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: 0 warnings, 0 errors, 203+ tests pass (new property panel tests included).

- [x] **Step 10: Commit**

```
git add -A && git commit -m "feat(ui): add property panel with color swatches, width, opacity

Context-sensitive panel visible for Pen/Rectangle/Ellipse tools.
16-color swatch grid, width slider 1-20px, opacity slider 0-100%.
Fill section shown only for shape tools. Wired to BoardToolController."
```

---

## Task 6: Player Roster Overlay

**Files:**
- Create: `src/BFGA.App/Views/RosterOverlay.axaml` + `.cs`
- Create: `tests/BFGA.App.Tests/RosterOverlayTests.cs`
- Modify: `src/BFGA.App/Views/BoardScreen.axaml`
- Modify: `src/BFGA.Network/GameHost.cs` (update PlayerColors palette)
- Modify: `tests/BFGA.Network.Tests/NetworkTests.cs` (host constructor compatibility + roster expectation)

- [x] **Step 0: Update GameHost.PlayerColors to match design spec palette**

The current `GameHost.PlayerColors` array (line ~175) uses the default SKColors palette (Red, Blue, Green, Orange, Purple, Cyan, Yellow, Magenta). The approved design spec (§6.3) requires a specific 8-color palette that looks better on dark backgrounds.

In `src/BFGA.Network/GameHost.cs`, replace the `PlayerColors` array:

```csharp
// BEFORE (current):
// private static readonly SKColor[] PlayerColors = [SKColors.Red, SKColors.Blue, ...];

// AFTER (spec palette from §6.3):
private static readonly SKColor[] PlayerColors =
[
    new SKColor(0xFF, 0x6B, 0x6B), // #FF6B6B - Coral Red
    new SKColor(0x4E, 0xCD, 0xC4), // #4ECDC4 - Teal
    new SKColor(0x45, 0xB7, 0xD1), // #45B7D1 - Sky Blue
    new SKColor(0x96, 0xCE, 0xB4), // #96CEB4 - Sage Green
    new SKColor(0xFF, 0xEA, 0xA7), // #FFEAA7 - Soft Yellow
    new SKColor(0xDD, 0xA0, 0xDD), // #DDA0DD - Plum
    new SKColor(0x98, 0xD8, 0xC8), // #98D8C8 - Mint
    new SKColor(0xF7, 0xDC, 0x6F), // #F7DC6F - Gold
];
```

This ensures roster avatar colors match the design spec.

- [x] **Step 1: Write failing tests for roster**

Create `tests/BFGA.App.Tests/RosterOverlayTests.cs`:

```csharp
public class RosterOverlayTests
{
    [Theory]
    [InlineData("Alice", "AL")]
    [InlineData("B", "B")]
    [InlineData("", "")]
    [InlineData("john doe", "JO")]
    public void GetInitials_ExtractsFirstTwoChars(string name, string expected)
    {
        Assert.Equal(expected, RosterOverlay.GetInitials(name));
    }
}
```

- [x] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~RosterOverlayTests" -v n`
Expected: FAIL.

- [x] **Step 3: Create RosterOverlay.axaml + .cs**

Create `src/BFGA.App/Views/RosterOverlay.axaml` as a `UserControl` with `x:Name="root"` and a code-behind `ItemsSource` styled property of type `IEnumerable<PlayerInfo>?`:
- Internal `ItemsControl` binds via `ItemsSource="{Binding ItemsSource, ElementName=root}"`
- The collection type should be `PlayerInfo` items (NOT `KeyValuePair<Guid, PlayerInfo>`)
- Each item: 28px circle with background color, 2-letter initials TextBlock
- Items overlap by -6px using negative margin
- Horizontal StackPanel, right-aligned

Code-behind exposes:
```csharp
public static readonly StyledProperty<IEnumerable<PlayerInfo>?> ItemsSourceProperty =
    AvaloniaProperty.Register<RosterOverlay, IEnumerable<PlayerInfo>?>(nameof(ItemsSource));

public IEnumerable<PlayerInfo>? ItemsSource
{
    get => GetValue(ItemsSourceProperty);
    set => SetValue(ItemsSourceProperty, value);
}

public static string GetInitials(string name)
    => name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();

public sealed class PlayerColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is SKColor c
            ? new SolidColorBrush(Avalonia.Media.Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue))
            : Brushes.Transparent;
}

public sealed class PlayerNameToInitialsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string name ? GetInitials(name) : string.Empty;
}
```

Declare `PlayerColorToBrushConverter` and `PlayerNameToInitialsConverter` as **top-level public classes in the `BFGA.App.Views` namespace** (they can live in the same `.cs` file as `RosterOverlay`). That way `RosterOverlay.axaml` can reference them via a local `views:` XML namespace.

In `RosterOverlay.axaml`, add both `xmlns:views="clr-namespace:BFGA.App.Views"` and `xmlns:network="clr-namespace:BFGA.Network;assembly=BFGA.Network"`, then register the converters in `UserControl.Resources`. The item template should be explicitly typed so compiled bindings work:

```xml
<UserControl x:Class="BFGA.App.Views.RosterOverlay"
             xmlns:views="clr-namespace:BFGA.App.Views"
             x:Name="root"
             x:DataType="views:RosterOverlay">
    <UserControl.Resources>
        <views:PlayerColorToBrushConverter x:Key="PlayerColorToBrushConverter" />
        <views:PlayerNameToInitialsConverter x:Key="PlayerNameToInitialsConverter" />
    </UserControl.Resources>

    <ItemsControl ItemsSource="{Binding ItemsSource, ElementName=root}">
        <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="network:PlayerInfo">
                <Border Classes="roster-avatar"
                        ToolTip.Tip="{Binding DisplayName}"
                        Background="{Binding AssignedColor, Converter={StaticResource PlayerColorToBrushConverter}}">
                    <TextBlock Text="{Binding DisplayName, Converter={StaticResource PlayerNameToInitialsConverter}}" />
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</UserControl>
```

This gives AXAML a real bindable path for both `AssignedColor` and initials — no direct calls to arbitrary static methods from bindings. Use the existing `PlayerInfo.AssignedColor` (SKColor) directly; the `PlayerColorToBrushConverter` handles the conversion to an Avalonia brush.

**Host entry in roster:** Currently `MainViewModel.Roster` is populated from network sync (`FullSyncResponseOperation.PlayerRoster`) which includes only connected clients, NOT the host itself. There are TWO things needed to make the host appear in all rosters:

1. **Host-side `GameHost.GetPlayerRoster()`** — must include the host itself so that when clients receive `FullSyncResponseOperation`, the roster includes the host. In `src/BFGA.Network/GameHost.cs`, modify `GetPlayerRoster()` (line ~261) to inject the host entry:

```csharp
public Dictionary<Guid, PlayerInfo> GetPlayerRoster()
{
    var roster = _players.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Info);
    // Include the host in the roster so clients see it
    roster[Guid.Empty] = new PlayerInfo(_hostDisplayName, SKColors.White);
    return roster;
}
```

This requires plumbing the host display name into `GameHost`. The full chain of changes:

1. **`IGameSessionFactory.CreateHost()`** → change to `CreateHost(string displayName)` in `src/BFGA.App/Networking/IGameSessionFactory.cs`
2. **`NetworkGameSessionFactory.CreateHost()`** → accept `string displayName`, pass to `new GameHost(displayName)` in `src/BFGA.App/Networking/NetworkGameSessionFactory.cs`
3. **`GameHost` constructors** → keep source compatibility by adding an overload pair in `src/BFGA.Network/GameHost.cs`:

   ```csharp
   public GameHost() : this("Host") { }

   public GameHost(string displayName)
   {
       _hostDisplayName = displayName;
       // existing initialization
   }
   ```

   This lets the app pass the real host display name while keeping existing direct `new GameHost()` test callers compiling.
4. **`MainViewModel.StartHostAsync()`** → change `_sessionFactory.CreateHost()` to `_sessionFactory.CreateHost(DisplayName)` (line ~351 of `src/BFGA.App/ViewModels/MainViewModel.cs`)
5. **`FakeGameSessionFactory.CreateHost()`** in `tests/BFGA.App.Tests/MainViewModelTests.cs` (line ~1055) → change to `CreateHost(string displayName)` to match the interface. The fake can ignore the parameter (`_ = displayName;`) and still return `new FakeGameHostSession()` unless tests need to assert the value later.
6. **`tests/BFGA.Network.Tests/NetworkTests.cs`** → update roster semantics now that `GetPlayerRoster()` includes the host. Replace `GameHost_PlayerRoster_InitiallyEmpty` with an assertion that a fresh host roster contains exactly one host entry:

   ```csharp
   var roster = host.GetPlayerRoster();
   Assert.Single(roster);
   Assert.True(roster.ContainsKey(Guid.Empty));
   Assert.Equal("Host", roster[Guid.Empty].DisplayName);
   ```

The host uses `Guid.Empty` as its sentinel ID (real client IDs are never `Guid.Empty`).

2. **Host-local ViewModel** — add the host entry to the local Roster in `StartHostAsync()` so the host's own UI shows it immediately (before any FullSync):

```csharp
// In MainViewModel.StartHostAsync(), after Host = host:
var hostEntry = new PlayerInfo(DisplayName, SKColors.White); // host gets white
UpsertRosterEntry(Guid.Empty, hostEntry); // Guid.Empty = host sentinel
```

And remove it in `StopHostAsync()`:
```csharp
RemoveRosterEntry(Guid.Empty);
```

This ensures the host always appears in the roster overlay. The `Guid.Empty` sentinel is safe because real network client IDs are never `Guid.Empty`.

**Binding under compiled bindings:** `MainViewModel.Roster` is `IReadOnlyDictionary<Guid, PlayerInfo>`, so binding an `ItemsControl` directly to `Roster` would yield `KeyValuePair<Guid, PlayerInfo>` items. Bind to `MainViewModel.Roster.Values` instead so each item is a `PlayerInfo`. Since `BoardScreen.axaml` has `x:DataType=BoardScreenViewModel` and compiled bindings are project-wide, use the full path `ItemsSource="{Binding MainViewModel.Roster.Values}"`. This is consistent with how BottomBar binds `MainViewModel.StatusText` through `BoardScreenViewModel.MainViewModel`.

- [x] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~RosterOverlayTests" -v n`
Expected: PASS.

- [x] **Step 5: Add RosterOverlay to BoardScreen.axaml**

In the center Grid of `BoardScreen.axaml`, overlay the roster top-right:

```xml
<views:RosterOverlay HorizontalAlignment="Right" VerticalAlignment="Top"
                     Margin="0,12,12,0"
                     ItemsSource="{Binding MainViewModel.Roster.Values}" />
```

The `RosterOverlay` should expose an `ItemsSource` styled property of type `IEnumerable<PlayerInfo>?`. Since `BoardScreen.axaml` inherits `BoardScreenViewModel` as DataContext, and compiled bindings are enabled, the path `MainViewModel.Roster.Values` resolves through `BoardScreenViewModel.MainViewModel` and gives the overlay a clean `PlayerInfo` collection.

- [x] **Step 6: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: All tests pass.

- [x] **Step 7: Commit**

```
git add -A && git commit -m "feat(ui): add player roster overlay with colored initials

Top-right avatar bubbles showing 2-letter initials in assigned colors.
8-color preset palette, -6px overlap stacking, hover tooltip."
```

---

## Task 7: Undo/Redo — Network Protocol

**Files:**
- Modify: `src/BFGA.Network/Protocol/BoardOperation.cs`
- Modify: `tests/BFGA.Network.Tests/ProtocolTests.cs`

- [x] **Step 1: Add UndoOperation and RedoOperation to BoardOperation.cs**

Add two new Union subtypes at keys 11 and 12:

```csharp
// Add to the [Union] attributes on BoardOperation class:
[Union(11, typeof(UndoOperation))]
[Union(12, typeof(RedoOperation))]

// New classes at end of file:
[MessagePackObject]
public sealed class UndoOperation : BoardOperation
{
    public override OperationType Type => OperationType.Undo;
}

[MessagePackObject]
public sealed class RedoOperation : BoardOperation
{
    public override OperationType Type => OperationType.Redo;
}
```

Add to `OperationType` enum:
```csharp
Undo = 11,
Redo = 12,
```

Also update `tests/BFGA.Network.Tests/ProtocolTests.cs` `BoardOperation_PolymorphicRoundTrip_AllTypes()` to include both new operation types in the `operations` array:

```csharp
new UndoOperation(),
new RedoOperation(),
```

- [x] **Step 2: Run protocol tests**

Run: `dotnet test tests/BFGA.Network.Tests --filter "FullyQualifiedName~ProtocolTests" -v n`
Expected: PASS — the polymorphic round-trip test now covers Undo/Redo too.

- [x] **Step 3: Build to verify MessagePack serialization compiles**

Run: `dotnet build src/BFGA.Network/BFGA.Network.csproj`
Expected: Build succeeds.

- [x] **Step 4: Commit**

```
git add -A && git commit -m "feat(undo): add UndoOperation and RedoOperation to protocol

New BoardOperation subtypes at Union keys 11/12 for undo/redo requests."
```

---

## Task 8: Undo/Redo — UndoRedoManager

**Files:**
- Create: `src/BFGA.Network/UndoRedoManager.cs`
- Create: `tests/BFGA.Network.Tests/UndoRedoManagerTests.cs`

- [x] **Step 1: Write failing tests**

Create `tests/BFGA.Network.Tests/UndoRedoManagerTests.cs`:

```csharp
public class UndoRedoManagerTests
{
    private readonly UndoRedoManager _sut = new();
    private readonly Guid _user1 = Guid.NewGuid();
    private readonly Guid _user2 = Guid.NewGuid();

    [Fact]
    public void CanUndo_FalseWhenEmpty()
    {
        Assert.False(_sut.CanUndo(_user1));
    }

    [Fact]
    public void CanRedo_FalseWhenEmpty()
    {
        Assert.False(_sut.CanRedo(_user1));
    }

    [Fact]
    public void Push_MakesCanUndoTrue()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);
        Assert.True(_sut.CanUndo(_user1));
    }

    [Fact]
    public void Undo_ReturnsInverseOp()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);

        var result = _sut.TryUndo(_user1);

        Assert.NotNull(result);
        Assert.IsType<DeleteElementOperation>(result);
    }

    [Fact]
    public void Undo_MakesCanRedoTrue()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);
        _sut.TryUndo(_user1);

        Assert.True(_sut.CanRedo(_user1));
        Assert.False(_sut.CanUndo(_user1));
    }

    [Fact]
    public void Redo_ReturnsForwardOp()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);
        _sut.TryUndo(_user1);

        var result = _sut.TryRedo(_user1);

        Assert.NotNull(result);
        Assert.IsType<AddElementOperation>(result);
    }

    [Fact]
    public void NewPush_ClearsRedoStack()
    {
        var op1 = CreateAddOp();
        _sut.Push(_user1, op1, CreateDeleteOp(op1));
        _sut.TryUndo(_user1);
        Assert.True(_sut.CanRedo(_user1));

        var op2 = CreateAddOp();
        _sut.Push(_user1, op2, CreateDeleteOp(op2));
        Assert.False(_sut.CanRedo(_user1));
    }

    [Fact]
    public void PerUser_StacksAreIsolated()
    {
        var op1 = CreateAddOp();
        _sut.Push(_user1, op1, CreateDeleteOp(op1));

        Assert.True(_sut.CanUndo(_user1));
        Assert.False(_sut.CanUndo(_user2));
    }

    [Fact]
    public void MaxDepth_DiscardsOldest()
    {
        for (int i = 0; i < 55; i++)
        {
            var op = CreateAddOp();
            _sut.Push(_user1, op, CreateDeleteOp(op));
        }

        // Should still work — max 50, oldest 5 discarded
        int undoCount = 0;
        while (_sut.TryUndo(_user1) is not null) undoCount++;
        Assert.Equal(50, undoCount);
    }

    [Fact]
    public void ClearUser_RemovesBothStacks()
    {
        var op = CreateAddOp();
        _sut.Push(_user1, op, CreateDeleteOp(op));
        _sut.TryUndo(_user1); // moves to redo

        _sut.ClearUser(_user1);

        Assert.False(_sut.CanUndo(_user1));
        Assert.False(_sut.CanRedo(_user1));
    }

    [Fact]
    public void Redo_ReturnsClonedAddSnapshot_NotMutatedLiveElement()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);

        _sut.TryUndo(_user1);

        // Mutate the original live element after it was pushed
        ((StrokeElement)forward.Element).Points.Add(new Vector2(999, 999));

        var redone = _sut.TryRedo(_user1);

        var add = Assert.IsType<AddElementOperation>(redone);
        var stroke = Assert.IsType<StrokeElement>(add.Element);
        Assert.DoesNotContain(new Vector2(999, 999), stroke.Points);
    }

    // Helpers
    private static AddElementOperation CreateAddOp()
    {
        var element = new StrokeElement { Id = Guid.NewGuid() };
        return new AddElementOperation { Element = element };
    }

    private static DeleteElementOperation CreateDeleteOp(AddElementOperation addOp)
        => new() { ElementId = addOp.Element.Id };
}
```

- [x] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/BFGA.Network.Tests --filter "FullyQualifiedName~UndoRedoManagerTests" -v n`
Expected: FAIL — class doesn't exist.

- [x] **Step 3: Implement UndoRedoManager**

Create `src/BFGA.Network/UndoRedoManager.cs`:

```csharp
using BFGA.Network.Protocol;
using MessagePack;

namespace BFGA.Network;

public sealed class UndoRedoManager
{
    private const int MaxDepth = 50;

    private readonly Dictionary<Guid, LinkedList<UndoEntry>> _undoStacks = new();
    private readonly Dictionary<Guid, Stack<UndoEntry>> _redoStacks = new();

    public bool CanUndo(Guid userId)
        => _undoStacks.TryGetValue(userId, out var stack) && stack.Count > 0;

    public bool CanRedo(Guid userId)
        => _redoStacks.TryGetValue(userId, out var stack) && stack.Count > 0;

    public void Push(Guid userId, BoardOperation forward, BoardOperation inverse)
    {
        if (!_undoStacks.TryGetValue(userId, out var undoStack))
        {
            undoStack = new LinkedList<UndoEntry>();
            _undoStacks[userId] = undoStack;
        }

        undoStack.AddLast(new UndoEntry(CloneOperation(forward), CloneOperation(inverse)));

        while (undoStack.Count > MaxDepth)
            undoStack.RemoveFirst();

        // Clear redo on new push
        if (_redoStacks.ContainsKey(userId))
            _redoStacks[userId].Clear();
    }

    public BoardOperation? TryUndo(Guid userId)
    {
        if (!_undoStacks.TryGetValue(userId, out var undoStack) || undoStack.Count == 0)
            return null;

        var entry = undoStack.Last!.Value;
        undoStack.RemoveLast();

        if (!_redoStacks.TryGetValue(userId, out var redoStack))
        {
            redoStack = new Stack<UndoEntry>();
            _redoStacks[userId] = redoStack;
        }
        redoStack.Push(entry);

        return CloneOperation(entry.InverseOp);
    }

    public BoardOperation? TryRedo(Guid userId)
    {
        if (!_redoStacks.TryGetValue(userId, out var redoStack) || redoStack.Count == 0)
            return null;

        var entry = redoStack.Pop();

        if (!_undoStacks.TryGetValue(userId, out var undoStack))
        {
            undoStack = new LinkedList<UndoEntry>();
            _undoStacks[userId] = undoStack;
        }
        undoStack.AddLast(entry);

        return CloneOperation(entry.ForwardOp);
    }

    public void ClearUser(Guid userId)
    {
        _undoStacks.Remove(userId);
        _redoStacks.Remove(userId);
    }

    private static T CloneOperation<T>(T operation) where T : BoardOperation
        => MessagePackSerializer.Deserialize<T>(
            MessagePackSerializer.Serialize(operation, BFGA.Core.MessagePackSetup.Options),
            BFGA.Core.MessagePackSetup.Options);

    private sealed record UndoEntry(BoardOperation ForwardOp, BoardOperation InverseOp);
}
```

Cloning is required because `AddElementOperation` can carry mutable `StrokeElement`/`ShapeElement` instances that later stay on the live board and continue changing. Store snapshots in the undo manager, not live references.

- [x] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/BFGA.Network.Tests --filter "FullyQualifiedName~UndoRedoManagerTests" -v n`
Expected: PASS.

- [x] **Step 5: Commit**

```
git add -A && git commit -m "feat(undo): implement UndoRedoManager with per-user stacks

Bounded undo/redo stacks (max 50), per-user isolation, redo clear on
new push. Uses LinkedList for O(1) oldest-discard."
```

---

## Task 9: Undo/Redo — Host Integration

**Files:**
- Modify: `src/BFGA.Network/GameHost.cs`
- Modify: `src/BFGA.App/Networking/IGameHostSession.cs`
- Modify: `src/BFGA.App/Networking/NetworkGameSessionFactory.cs`

- [x] **Step 1: Add UndoRedoManager to GameHost**

In `src/BFGA.Network/GameHost.cs`:
- Add field: `private readonly UndoRedoManager _undoManager = new();`
- Add field: `private static readonly Guid _hostUserId = Guid.Empty;`

Use this single internal sentinel everywhere for host-local operations and undo/redo tracking. Do **not** expose a public `HostUserId` property — the app layer should call `CanUndoLocal` / `TryUndoLocal()` instead.

- [x] **Step 2: Capture pre-change snapshots in ApplyOperation**

In `GameHost.ApplyOperation` (line ~370), before applying delete/update/move, snapshot the affected element:

```csharp
case DeleteElementOperation deleteOp:
    if (_boardElements.TryGetValue(deleteOp.ElementId, out var deletedElement))
    {
        // Clone for undo snapshot
        var inverseAdd = new AddElementOperation { Element = CloneElement(deletedElement) };
        _undoManager.Push(operation.SenderId, operation, inverseAdd);
        _boardElements.Remove(deleteOp.ElementId);
    }
    break;

case UpdateElementOperation updateOp:
    if (_boardElements.TryGetValue(updateOp.ElementId, out var prevElement))
    {
        // Capture INVERSE ModifiedProperties: snapshot the current values
        // for each key that is about to be overwritten.
        // UpdateElementOperation uses a Dictionary<string, object> ModifiedProperties,
        // NOT an Element payload. The inverse must capture the old values
        // for the same property keys.
        var inverseProps = new Dictionary<string, object>();
        foreach (var key in updateOp.ModifiedProperties.Keys)
        {
            inverseProps[key] = GetElementProperty(prevElement, key);
        }
        var inverseUpdate = new UpdateElementOperation(updateOp.ElementId, inverseProps);
        _undoManager.Push(operation.SenderId, operation, inverseUpdate);
        ApplyModifiedProperties(prevElement, updateOp.ModifiedProperties);
    }
    break;

case MoveElementOperation moveOp:
    if (_boardElements.TryGetValue(moveOp.ElementId, out var prevMoveElement))
    {
        // Capture inverse: restore to previous Position, Size, Rotation
        // Note: MoveElementOperation fields are Position, Size, Rotation (NOT NewPosition etc.)
        var inverseMoveOp = new MoveElementOperation
        {
            ElementId = moveOp.ElementId,
            Position = prevMoveElement.Position,
            Size = prevMoveElement.Size,
            Rotation = prevMoveElement.Rotation,
        };
        _undoManager.Push(operation.SenderId, operation, inverseMoveOp);
        // Apply the move
        prevMoveElement.Position = moveOp.Position;
        prevMoveElement.Size = moveOp.Size;
        prevMoveElement.Rotation = moveOp.Rotation;
    }
    break;

case AddElementOperation addOp:
    var inverseDelete = new DeleteElementOperation { ElementId = addOp.Element.Id };
        _undoManager.Push(operation.SenderId, operation, inverseDelete);
        _boardElements[addOp.Element.Id] = addOp.Element;
        break;
```

Add a `GetElementProperty` helper that reads a named property from a `BoardElement` using the same key scheme as `ApplyModifiedProperties`. Also add a `CloneElement` helper that deep-copies a `BoardElement` (using MessagePack serialize/deserialize roundtrip).

This is safe for add operations because `UndoRedoManager.Push(...)` now clones both `forward` and `inverse` operations before storing them. That prevents redo from replaying a later-mutated live element instance.

- [x] **Step 3: Handle UndoOperation and RedoOperation in HandleOperation + ApplyOperation**

**CRITICAL:** `HandleOperation` (line ~311 of GameHost.cs) unconditionally calls `ApplyOperation` then `BroadcastOperation` for every incoming op. For `UndoOperation`/`RedoOperation`, we must NOT run this default path — we must short-circuit BEFORE the unconditional broadcast, because:
1. The `UndoOperation`/`RedoOperation` request itself should never be broadcast to clients
2. Instead, the *result* (the inverse/forward op from the undo manager) is what gets applied and broadcast

**Modify `HandleOperation`** to short-circuit undo/redo before the normal apply+broadcast path:

```csharp
private void HandleOperation(NetPeer peer, BoardOperation operation)
{
    operation.SenderId = GetClientId(peer);
    OperationReceived?.Invoke(this, new OperationReceivedEventArgs(operation, operation.SenderId));

    bool isValid = ValidateOperation(operation);
    if (!isValid) return;

    // SHORT-CIRCUIT: Undo/Redo are meta-operations — resolve them to
    // concrete ops and apply+broadcast the result, NOT the request.
    switch (operation)
    {
        case UndoOperation:
        {
            var undoResult = _undoManager.TryUndo(operation.SenderId);
            if (undoResult is not null)
            {
                ApplyOperationNoUndo(undoResult); // apply WITHOUT pushing to undo stack
                BroadcastOperation(undoResult, IsOperationReliable(undoResult));
            }
            return; // ← IMPORTANT: skip the default apply+broadcast below
        }
        case RedoOperation:
        {
            var redoResult = _undoManager.TryRedo(operation.SenderId);
            if (redoResult is not null)
            {
                ApplyOperationNoUndo(redoResult); // apply WITHOUT pushing to undo stack
                BroadcastOperation(redoResult, IsOperationReliable(redoResult));
            }
            return; // ← IMPORTANT: skip the default apply+broadcast below
        }
    }

    ApplyOperation(operation);
    BroadcastOperation(operation, IsOperationReliable(operation));
}
```

**Add `ApplyOperationNoUndo` method** — same as `ApplyOperation` but skips the `_undoManager.Push()` calls. This prevents undo/redo results from being double-recorded on the undo stack. The simplest implementation is to extract the element-mutation logic into a shared helper and have `ApplyOperation` call it with `pushUndo: true` and `ApplyOperationNoUndo` call it with `pushUndo: false`:

```csharp
private void ApplyOperation(BoardOperation operation)
    => ApplyOperationCore(operation, pushUndo: true);

private void ApplyOperationNoUndo(BoardOperation operation)
    => ApplyOperationCore(operation, pushUndo: false);

private void ApplyOperationCore(BoardOperation operation, bool pushUndo)
{
    // ... existing switch cases, but wrap _undoManager.Push() calls in:
    // if (pushUndo) _undoManager.Push(operation.SenderId, operation, inverse);
}
```

Also add `UndoOperation` and `RedoOperation` to `ValidateOperation`'s whitelist — they should NOT be rejected (they are valid client requests), but they also shouldn't fall into the element-existence checks.

- [x] **Step 4: Wire host-local undo via TryApplyLocalOperation**

In `TryApplyLocalOperation`, when the operation is tagged for undo, use `_hostUserId` as the SenderId:

```csharp
public bool TryApplyLocalOperation(BoardOperation operation)
{
    if (!_isRunning || !ValidateOperation(operation))
        return false;

    operation.SenderId = _hostUserId; // Tag with host identity
    ApplyOperation(operation);
    return true;
}
```

- [x] **Step 5: Add undo/redo to IGameHostSession**

In `src/BFGA.App/Networking/IGameHostSession.cs`, add:

```csharp
bool CanUndo { get; }
bool CanRedo { get; }
bool TryUndo();
bool TryRedo();
```

- [x] **Step 6: Implement in NetworkGameSessionFactory.HostSessionAdapter**

In `src/BFGA.App/Networking/NetworkGameSessionFactory.cs`, delegate to new explicit methods on `GameHost`:

```csharp
public bool CanUndo => _inner.CanUndoLocal;
public bool CanRedo => _inner.CanRedoLocal;

public bool TryUndo() => _inner.TryUndoLocal();
public bool TryRedo() => _inner.TryRedoLocal();
```

This requires adding four public members to `GameHost` (in `src/BFGA.Network/GameHost.cs`):

```csharp
// Guid.Empty is the host's sentinel user ID
private static readonly Guid _hostUserId = Guid.Empty;

public bool CanUndoLocal => _undoManager.CanUndo(_hostUserId);
public bool CanRedoLocal => _undoManager.CanRedo(_hostUserId);

public bool TryUndoLocal()
{
    var result = _undoManager.TryUndo(_hostUserId);
    if (result is null) return false;
    ApplyOperationNoUndo(result);
    BroadcastOperation(result, IsOperationReliable(result));
    return true;
}

public bool TryRedoLocal()
{
    var result = _undoManager.TryRedo(_hostUserId);
    if (result is null) return false;
    ApplyOperationNoUndo(result);
    BroadcastOperation(result, IsOperationReliable(result));
    return true;
}
```

Use `ApplyOperationNoUndo()` here so executing an undo/redo result does **not** push a fresh entry back onto the undo stack. `GameHost` already has `BroadcastOperation(...)`; there is no `BroadcastToAllClients()` method to call. This approach keeps the undo internals encapsulated — `HostSessionAdapter` does NOT need to access `UndoManager` or any host user ID directly.

- [x] **Step 7: Update FakeGameHostSession in tests**

**CRITICAL:** `tests/BFGA.App.Tests/MainViewModelTests.cs` contains `FakeGameHostSession : IGameHostSession` (line ~1068). Adding `CanUndo`/`CanRedo`/`TryUndo()`/`TryRedo()` to `IGameHostSession` will break compilation unless the fake also implements them. Add stub members:

```csharp
// In FakeGameHostSession (tests/BFGA.App.Tests/MainViewModelTests.cs):
public bool CanUndo => false;
public bool CanRedo => false;
public bool TryUndo() => false;
public bool TryRedo() => false;
```

This is the minimum to keep existing tests compiling. If undo/redo tests are added for MainViewModel later, enhance these stubs.

- [x] **Step 8: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: All tests pass (existing + new UndoRedoManager tests). Verify that `FakeGameHostSession` compiles with the new interface members.

- [x] **Step 9: Commit**

```
git add -A && git commit -m "feat(undo): integrate UndoRedoManager into GameHost

Host captures pre-change snapshots, handles Undo/RedoOperation,
exposes TryUndo/TryRedo via IGameHostSession for app-layer binding.
Update FakeGameHostSession test double with stub undo/redo members."
```

---

## Task 10: Undo/Redo — UI Wiring

**Files:**
- Modify: `src/BFGA.App/ViewModels/MainViewModel.cs`
- Modify: `src/BFGA.App/Views/BottomBar.axaml`
- Modify: `src/BFGA.App/MainWindow.axaml.cs`
- Modify: `src/BFGA.App/Views/BoardView.axaml.cs`

- [x] **Step 1: Add undo/redo commands to MainViewModel**

In `src/BFGA.App/ViewModels/MainViewModel.cs`, add:

```csharp
private int _undoShadowCount;
private int _redoShadowCount;

private readonly AsyncRelayCommand _undoCommand;
private readonly AsyncRelayCommand _redoCommand;

public bool CanUndo => Host is not null ? Host.CanUndo : _undoShadowCount > 0;
public bool CanRedo => Host is not null ? Host.CanRedo : _redoShadowCount > 0;

public AsyncRelayCommand UndoCommand => _undoCommand;
public AsyncRelayCommand RedoCommand => _redoCommand;
```

Initialize commands in constructor with the existing `CreateShellCommand(...)` helper so they get `canExecute` predicates and consistent shell error handling:

```csharp
_undoCommand = CreateShellCommand(UndoAsync, () => CanUndo, "Failed to undo: ");
_redoCommand = CreateShellCommand(RedoAsync, () => CanRedo, "Failed to redo: ");
```

`UndoCommand` executes:
- Host mode: `if (Host?.TryUndo() == true) SyncBoardFromHost();`
- Client mode: send `new UndoOperation()` via `Client.SendOperation()`, decrement `_undoShadowCount` if greater than 0, increment `_redoShadowCount`, then fire `OnPropertyChanged(nameof(CanUndo))` / `OnPropertyChanged(nameof(CanRedo))`

`RedoCommand` similarly.

For normal client-side local edits, update the shadow counters in the **client branch of `DispatchLocalBoardOperation`**, not `PublishLocalBoardOperation`. In the current code, client edits flow through `DispatchLocalBoardOperation()` (`ApplyLocalBoardOperation(...)` + `Client.SendOperation(...)`), while host edits flow through `PublishLocalBoardOperation()`. After any element-mutating operation (`AddElementOperation`, `UpdateElementOperation`, `DeleteElementOperation`, `MoveElementOperation`) in the client branch, increment `_undoShadowCount`, reset `_redoShadowCount = 0`, and fire `OnPropertyChanged(nameof(CanUndo))` / `OnPropertyChanged(nameof(CanRedo))`.

Because the current runtime call site in `src/BFGA.App/Views/BoardView.axaml.cs` (line ~236) still calls `MainViewModel.PublishLocalBoardOperation(operation)`, update it to call `MainViewModel.DispatchLocalBoardOperation(operation)` instead. Otherwise tool-driven client edits will bypass the new shadow-counter logic and `CanUndo`/`CanRedo` will stay stale.

On `FullSyncResponse` received, reset both shadow counters to 0 and fire `CanUndo`/`CanRedo` property changed.

**CanUndo/CanRedo notification points** — whenever you fire `OnPropertyChanged(nameof(CanUndo))` / `OnPropertyChanged(nameof(CanRedo))`, also call `_undoCommand.RaiseCanExecuteChanged()` and `_redoCommand.RaiseCanExecuteChanged()` so button enablement and `MainWindow.OnKeyDown` checks stay correct.

Do that in these locations:
- After the client branch of `DispatchLocalBoardOperation` (shadow counter increment for element-mutating ops)
- After any successful host-side mutating operation in `PublishLocalBoardOperation` / host path of `DispatchLocalBoardOperation`, so host undo state updates immediately instead of waiting for the next poll tick
- After undo/redo command executes (counter change + host state change)
- After `ApplyFullSync` (shadow counter reset)
- After `ConnectionState` changes (host/client transition resets available state)
- In host mode, after the poll timer fires (since other players' operations change what the host can undo)

- [x] **Step 2: Add undo/redo buttons to BottomBar.axaml**

Rewrite `src/BFGA.App/Views/BottomBar.axaml` to float centered with undo/redo on the left:

**Preserve the existing control contract:** keep the `UserControl` root as `x:Name="root"` with `x:DataType="vm:BoardScreenViewModel"` and `Classes="whiteboard-bottom-bar"`. `BottomBar.axaml.cs` raises `PropertyChanged` for `BoardView`, the current zoom bindings rely on `ElementName=root`, and `BoardScreenLayoutTests.cs` asserts the root class.

**Binding strategy:** The BottomBar's `x:DataType` is `BoardScreenViewModel` and compiled bindings are enabled. Therefore:
- Undo/redo binds through `{Binding MainViewModel.UndoCommand}` and `{Binding MainViewModel.CanUndo}` (since `BoardScreenViewModel.MainViewModel` is a public property that exposes the `MainViewModel`)
- Zoom controls continue using `BoardView` bindings — keep the existing `ElementName` pattern or switch to a `BoardView` StyledProperty that the BottomBar references

```xml
<Border Classes="whiteboard-bottom-bar-panel"
        HorizontalAlignment="Center" VerticalAlignment="Bottom" Margin="0,0,0,12">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <!-- Undo/Redo — bound through MainViewModel because BottomBar's
             DataContext is BoardScreenViewModel (compiled bindings).
             BoardScreenViewModel already exposes MainViewModel as a public property. -->
        <Button Content="↶" Command="{Binding MainViewModel.UndoCommand}"
                IsEnabled="{Binding MainViewModel.CanUndo}" Classes="whiteboard-tool-button"
                Width="32" Height="32" />
        <Button Content="↷" Command="{Binding MainViewModel.RedoCommand}"
                IsEnabled="{Binding MainViewModel.CanRedo}" Classes="whiteboard-tool-button"
                Width="32" Height="32" />

        <!-- Divider -->
        <Border Width="1" Height="20" Background="{DynamicResource BorderSubtle}" />

        <!-- Zoom controls — keep existing ElementName=root binding pattern.
             These bind to BoardView properties via the BottomBar's x:Name="root" reference,
             NOT through the DataContext. This is the same pattern as the current BottomBar. -->
        <Button Command="{Binding BoardView.ZoomOutCommand, ElementName=root}"
                Classes="whiteboard-tool-button" Content="−" ToolTip.Tip="Zoom out" />
        <Slider Minimum="0.2" Maximum="3"
                Value="{Binding BoardView.ZoomLevel, ElementName=root, Mode=TwoWay}"
                Width="160" TickFrequency="0.1" ToolTip.Tip="Zoom level" />
        <TextBlock Text="{Binding BoardView.ZoomLabel, ElementName=root}"
                   Classes="whiteboard-shortcut-hint" />
        <Button Command="{Binding BoardView.ZoomInCommand, ElementName=root}"
                Classes="whiteboard-tool-button" Content="+" ToolTip.Tip="Zoom in" />
    </StackPanel>
</Border>
```

**IMPORTANT:** The BottomBar's `x:DataType` is `BoardScreenViewModel` and compiled bindings are enabled project-wide (`AvaloniaUseCompiledBindingsByDefault=true` in csproj). All undo/redo bindings MUST use the `MainViewModel.` prefix (e.g., `{Binding MainViewModel.UndoCommand}`) because `UndoCommand`/`CanUndo`/`CanRedo` live on `MainViewModel`, not `BoardScreenViewModel`. `BoardScreenViewModel` already exposes `MainViewModel` as a public property.

**Zoom bindings** continue using `ElementName=root` to reach `BoardView` properties — this is the existing pattern that works and is tested. The existing tests in `BoardScreenLayoutTests.cs` assert `ZoomInCommand`, `Slider`, etc. The layout change (from Grid to StackPanel) will require updating those test assertions — see Task 12 for the integration test update step.

**Test update:** `BoardScreenLayoutTests.cs` (lines 54-56, 72-82) asserts specific bottom-bar content like `ZoomInCommand`, `Slider`, `ZoomLevel`. The StackPanel rewrite preserves these bindings, but if the Grid `ColumnDefinitions` or structure changes, update the layout test assertions to match. Also, the existing `StatusText` and `ConnectionState` TextBlocks are being moved — the tests that check for those must be updated.

- [x] **Step 3: Add Ctrl+Z / Ctrl+Y keyboard shortcuts**

In `src/BFGA.App/MainWindow.axaml.cs` `OnKeyDown`, modify the pattern match to capture the `MainViewModel` as `vm`, then add undo/redo shortcuts before the tool shortcuts:

```csharp
private void OnKeyDown(object? sender, KeyEventArgs e)
{
    if (DataContext is not MainViewModel { CurrentScreen: BoardScreenViewModel boardScreen } vm)
    {
        return;
    }

    // Check Ctrl+Shift+Z FIRST (most specific), then Ctrl+Z, then Ctrl+Y
    if (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
    {
        if (vm.RedoCommand.CanExecute(null)) vm.RedoCommand.Execute(null);
        e.Handled = true;
        return;
    }
    if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
    {
        if (vm.UndoCommand.CanExecute(null)) vm.UndoCommand.Execute(null);
        e.Handled = true;
        return;
    }
    if (e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
    {
        if (vm.RedoCommand.CanExecute(null)) vm.RedoCommand.Execute(null);
        e.Handled = true;
        return;
    }

    if (!TryHandleToolShortcut(boardScreen, e.Key, e.KeyModifiers))
    {
        return;
    }

    e.Handled = true;
}
```

**IMPORTANT:** The existing pattern match `DataContext is not MainViewModel { CurrentScreen: BoardScreenViewModel boardScreen }` must be extended to capture the `MainViewModel` itself as `vm` (nested pattern: `... boardScreen } vm`). Without this, `vm.UndoCommand` won't compile because there's no `vm` in scope.

- [x] **Step 4: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: All tests pass.

- [x] **Step 5: Commit**

```
git add -A && git commit -m "feat(undo): wire undo/redo to UI with bottom bar buttons + Ctrl+Z/Y

Host mode reads directly from UndoRedoManager via IGameHostSession.
Client mode uses shadow counters for CanUndo/CanRedo. Bottom bar
floated center with undo/redo + divider + zoom controls."
```

---

## Task 11: Screen Transitions + Hover Polish

**Files:**
- Modify: `src/BFGA.App/MainWindow.axaml` (ContentControl → TransitioningContentControl)
- Modify: `src/BFGA.App/Styles/WhiteboardTheme.axaml`
- Modify: `tests/BFGA.App.Tests/MainViewModelTests.cs` (update ContentControl assertion to TransitioningContentControl)

- [x] **Step 1: Add screen transition to MainWindow**

In `src/BFGA.App/MainWindow.axaml`, replace the `ContentControl` with `TransitioningContentControl` and use `PageTransition`:

```xml
<!-- BEFORE (current): -->
<!-- <ContentControl Content="{Binding CurrentScreen}"> -->

<!-- AFTER: -->
<TransitioningContentControl Content="{Binding CurrentScreen}">
    <TransitioningContentControl.PageTransition>
        <CrossFade Duration="0:0:0.3" />
    </TransitioningContentControl.PageTransition>
</TransitioningContentControl>
```

**IMPORTANT:** Avalonia 11 provides cross-fade via `TransitioningContentControl.PageTransition`, NOT `ContentControl.ContentTransition`. The `ContentControl` class does not have a `ContentTransition` property.

**DataTemplates:** The current `MainWindow.axaml` defines DataTemplates in `<Window.DataTemplates>` (lines 12-20), NOT inside the ContentControl. These remain on the `Window` level and work automatically with `TransitioningContentControl` — the control inherits DataTemplate resolution from its parent Window. Do NOT move DataTemplates inside the control or use `ContentTemplate`.

**Test update required:** `tests/BFGA.App.Tests/MainViewModelTests.cs` (line ~117-125) has a test that asserts `ContentControl` in the AXAML. After this change, update the assertion to check for `TransitioningContentControl` instead. If the test uses `FindControl<ContentControl>()`, change to `FindControl<TransitioningContentControl>()` (both are in `Avalonia.Controls` namespace).

- [x] **Step 2: Add transition animations to tool buttons**

In `src/BFGA.App/Styles/WhiteboardTheme.axaml`, add transitions to the tool button style:

```xml
<Style Selector="Button.whiteboard-tool-button">
    <!-- ... existing setters ... -->
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.15" />
            <BrushTransition Property="Foreground" Duration="0:0:0.15" />
        </Transitions>
    </Setter>
</Style>
```

- [x] **Step 3: Add color swatch scale animation**

```xml
<Style Selector="Button.color-swatch:pointerover">
    <Setter Property="RenderTransform" Value="scale(1.15)" />
</Style>
<Style Selector="Button.color-swatch">
    <Setter Property="Transitions">
        <Transitions>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.1" />
        </Transitions>
    </Setter>
</Style>
```

- [x] **Step 4: Add roster avatar scale animation + tooltip theme**

```xml
<Style Selector="Border.roster-avatar:pointerover">
    <Setter Property="RenderTransform" Value="scale(1.1)" />
</Style>
<Style Selector="Border.roster-avatar">
    <Setter Property="Transitions">
        <Transitions>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.15" />
        </Transitions>
    </Setter>
</Style>

<Style Selector="ToolTip">
    <Setter Property="Background" Value="{DynamicResource BgElevated}" />
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}" />
    <Setter Property="FontFamily" Value="{DynamicResource InterFont}" />
    <Setter Property="FontSize" Value="11" />
    <Setter Property="Padding" Value="8,4" />
</Style>
```

This tooltip styling is required for the roster-avatar hover tooltip from Task 6 so it matches the approved dark editorial design instead of default Fluent tooltip chrome.

- [x] **Step 5: Build and run all tests**

Run: `dotnet build BFGA.sln && dotnet test BFGA.sln`
Expected: All tests pass.

- [x] **Step 6: Commit**

```
git add -A && git commit -m "feat(ui): add transitions and hover polish

300ms crossfade screen transition, 150ms tool button transitions,
100ms color swatch scale, 150ms roster avatar scale."
```

---

## Task 12: Final Integration + Verification

**Files:**
- All files from previous tasks

- [x] **Step 1: Full build**

Run: `dotnet build BFGA.sln`
Expected: 0 warnings, 0 errors.

- [x] **Step 2: Full test suite**

Run: `dotnet test BFGA.sln`
Expected: 203+ tests pass (original 203 + new property panel + roster + undo tests).

- [x] **Step 3: Launch app and verify connection screen**

Run: `dotnet run --project src/BFGA.App/BFGA.App.csproj`

Verify:
- Editorial layout with centered card on #0A0A0A background
- Decorative circles and "B F G A" logo visible
- HOST/JOIN tab switcher works
- Form fields change based on mode (HOST shows name+port, JOIN shows name+address+port)
- Single context-aware button changes label per state
- Save/Load buttons visible when disconnected

- [x] **Step 4: Verify board screen**

Start a host session, then verify:
- Toolbar shows grouped tools with dividers
- Active tool has white left border accent
- Hover states on tool buttons
- Property panel appears for Pen/Rectangle/Ellipse, hides for other tools
- Color swatches work
- Width/opacity sliders work
- Player roster shows host avatar
- Bottom bar floated center with undo/redo + zoom
- Screen transition was smooth crossfade

- [x] **Step 5: Verify undo/redo**

- Draw a stroke → Ctrl+Z undoes it → Ctrl+Y redoes it
- Draw multiple → undo multiple → redo
- Undo/redo buttons enable/disable correctly

- [x] **Step 6: Update plan file — mark all tasks complete**

- [x] **Step 7: Final commit if any fixes needed**

```
git add -A && git commit -m "fix: integration fixes from manual verification"
```

---

## Summary

| Task | Description | Est. Complexity |
|------|-------------|----------------|
| 1 | Theme Foundation — Colors.axaml + ThemeColors.cs | Low |
| 2 | Typography — Fonts + Typography.axaml | Low |
| 3 | Connection Screen Editorial Redesign | Medium |
| 4 | Toolbar Redesign — Grouping + Active State | Low-Medium |
| 5 | Property Panel — Color, Width, Opacity | Medium |
| 6 | Player Roster Overlay | Low-Medium |
| 7 | Undo/Redo — Network Protocol | Low |
| 8 | Undo/Redo — UndoRedoManager | Medium |
| 9 | Undo/Redo — Host Integration | Medium-High |
| 10 | Undo/Redo — UI Wiring | Medium |
| 11 | Screen Transitions + Hover Polish | Low |
| 12 | Final Integration + Verification | Low |

**Total:** 12 tasks, incremental with frequent commits, TDD where applicable.
