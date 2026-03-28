# BFGA UI Polish & Fixes Design Spec

**Date**: 2026-03-28
**Status**: Approved

## Overview

Seven targeted fixes and improvements to the BFGA board application based on manual testing feedback. Covers: DLL lock on close, window chrome, toolbar remake, property panel text, background grid visibility, zoom bar, and settings panel.

## 1. Window & Custom Title Bar

### Problem
- Maximizing the window causes it to become invisible (rendering bug).
- Standard OS chrome doesn't match the dark minimalist aesthetic.

### Solution
- Fixed window size: **1400x900**, `CanResize="False"`.
- Remove OS title bar via `ExtendClientAreaToDecorationsHint="True"` + `ExtendClientAreaTitleBarHeightHint="40"`.
- Custom title bar (40px height, `BgBase` background):
  - Left: "BFGA" label (`InterMediumFont`, 14px, `TextSecondary`)
  - Right: Gear icon button (opens Settings) | Minimize button | Close button
- Close button: red hover (`#C42B1C`).
- Drag-to-move: The title bar Border wraps in a Panel with `Background="Transparent"` so it receives pointer events. Avalonia's `ExtendClientAreaToDecorationsHint` automatically makes the extended area draggable; the custom buttons opt out via `WindowChrome.IsHitTestVisibleInChrome="True"`.
- No maximize button, no resize grip.

### Settings Visibility Ownership
The gear button lives in `MainWindow.axaml` (title bar), but the settings panel renders as an overlay inside `BoardScreen.axaml`. The gear button sets `MainViewModel.IsSettingsOpen` (bool property on MainViewModel, accessible from both MainWindow and BoardScreen via DataContext chain). Settings is only available on the board screen — the gear button is hidden or disabled on the connection screen.

### Files Changed
- `MainWindow.axaml`: Add window properties, replace Grid with DockPanel containing custom title bar + content area.
- `MainWindow.axaml.cs`: Add minimize/close handlers, gear button visibility binding.
- `MainViewModel.cs`: Add `IsSettingsOpen` property.
- `WhiteboardTheme.axaml`: Title bar styles.

## 2. Floating Horizontal Toolbar (Excalidraw-style)

### Problem
- Current vertical sidebar is non-standard and tool icons are unclear.
- Tools don't visually communicate their function.

### Solution
Move toolbar from vertical left-dock to **horizontal floating bar, top-center** of the board canvas.

#### Tool Groups (left to right, separated by 1px dividers)

| Group | Tools | Shortcuts |
|-------|-------|-----------|
| Navigation | Select (cursor icon), Hand (pan icon) | V, H |
| Shapes | Rectangle, Ellipse, Arrow (new), Line (new) | R, E, A, L |
| Freehand | Pen | P |
| Text & Media | Text (new), Image | T, I |
| Utility | Eraser, Laser Pointer (new) | X, - (no shortcut for Laser) |

#### Visual Style
- Background: `BgElevated` (#111111), 1px `BorderDefault` border, 8px radius.
- Buttons: 32x32px, 6px radius.
- Active tool: `#1a1a2a` background + `TextPrimary` icon.
- Hover: `BgOverlay` background + `TextPrimary` icon.
- Icons: Lucide-style outline SVG paths as StreamGeometry (no emojis, no icon fonts).
- Transitions: 150ms `BrushTransition` on Background/Foreground.

#### New Enum Values
Add to `BoardToolType`: `Arrow`, `Line`, `Text`, `LaserPointer`. Remove unused `Shape`.

#### Removing Shape Enum Value
`Shape` is currently used as an internal routing target: `BoardView.axaml.cs` remaps Rectangle/Ellipse to `Shape` when forwarding to `BoardToolController`, which handles the `Shape` case. After removing `Shape`:
- `BoardToolController` must handle `Rectangle` and `Ellipse` directly (replace the single `Shape` case with two cases that delegate to the existing shape-drawing logic within `BoardToolController`).
- `BoardView.axaml.cs` tool-remapping logic is removed — it forwards `Rectangle`/`Ellipse` as-is.
- Tests referencing `BoardToolType.Shape` (in `BoardToolControllerTests` and `MainViewModelTests`) must be updated to use the specific tool types.

#### New Tool Behavior (Placeholder)
Arrow, Line, Text, and Laser Pointer are **inert placeholders** in this slice:
- They appear in the toolbar and can be selected (active highlight, keyboard shortcut).
- `BoardToolController` treats unknown/unhandled tool types as no-op (no canvas interaction).
- No canvas handlers, no element creation — just tool switching UI.
- This is intentional. Canvas interaction for these tools is future work.

#### Property Panel Visibility Updates
- Show panel for: Pen, Rectangle, Ellipse, Arrow, Line.
- Show fill section for: Rectangle, Ellipse.
- Text tool: no property panel initially (future work).

### Files Changed
- `BoardToolType.cs`: Add Arrow, Line, Text, LaserPointer; remove Shape.
- `ToolBar.axaml`: Rewrite as horizontal floating bar.
- `ToolIcons.axaml`: Add new StreamGeometry paths for all tools (Lucide-style outlines).
- `BoardScreenViewModel.cs`: Add commands/properties for new tools.
- `MainWindow.axaml.cs`: Add keyboard shortcuts A, L, T.
- `BoardScreen.axaml`: Move toolbar from DockPanel.Left to Grid overlay at top-center.
- `WhiteboardTheme.axaml`: Update toolbar styles for horizontal layout.
- `BoardView.axaml.cs`: Remove Shape-remapping logic, forward tool types directly.
- `BoardToolController.cs`: Replace `Shape` case with explicit `Rectangle`/`Ellipse` cases; no-op for Arrow/Line/Text/LaserPointer.

## 3. Background Grid Visibility Fix

### Problem
Dot grid uses `ThemeColors.DotGrid` (#1F1F1F) on `BgSurface` (#0D0D0D) — only ~7% contrast, nearly invisible.

### Solution
- Change `ThemeColors.DotGrid` to opaque white: `new SKColor(0xFF, 0xFF, 0xFF)` — base RGB only, no alpha.
- Keep 24px spacing, 1.25f radius.
- Make opacity configurable via Settings (0-30% range, default 10%).

### Settings-to-Canvas Data Flow
`SettingsService` loads/saves settings JSON. `MainViewModel` holds a `SettingsService` instance and exposes `GridOpacity` (float, 0.0-0.3 in model, displayed as 0-30% in UI — multiply by 100 for display). `BoardCanvas.cs` currently reads `ThemeColors.DotGrid` statically. Change: `BoardCanvas` accepts a `DotGridOpacity` property (float, 0.0-0.3). At render time, `BoardCanvas` creates the dot paint color by combining `ThemeColors.DotGrid` RGB with alpha derived from `DotGridOpacity` (i.e., `new SKColor(ThemeColors.DotGrid.Red, ThemeColors.DotGrid.Green, ThemeColors.DotGrid.Blue, (byte)(DotGridOpacity * 255))`). This makes `DotGridOpacity` the single source of truth for grid visibility. `BoardScreen.axaml` binds `BoardView.DotGridOpacity` to `MainViewModel.GridOpacity`. When the user changes the slider in SettingsPanel, `SettingsService` persists, `MainViewModel.GridOpacity` fires PropertyChanged, binding updates `BoardCanvas.DotGridOpacity`, which calls `InvalidateVisual()` in its setter. Same pattern for any future settings that affect the canvas.

### Files Changed
- `ThemeColors.cs`: Change DotGrid to opaque white `new SKColor(0xFF, 0xFF, 0xFF)`.
- `BoardCanvas.cs`: Add `DotGridOpacity` property, combine with `ThemeColors.DotGrid` RGB at render time. Call `InvalidateVisual()` on change.
- `BoardViewport.cs`: Forward `DotGridOpacity` property to `BoardCanvas`.
- `BoardView.axaml` / `.axaml.cs`: Add `DotGridOpacity` dependency property, bind through to viewport.

## 4. Property Panel Text Boldness

### Problem
Section headers (STROKE, WIDTH, OPACITY, FILL) use light font, hard to read.

### Solution
- Section headers: `InterMediumFont`, `TextSecondary` (#B0B0B0).
- No other changes — colors, sliders, swatches are approved as-is.

### Files Changed
- `PropertyPanel.axaml`: Update TextBlock FontFamily/Foreground for section headers.

## 5. Zoom Bar Fix

### Problem
Undo/redo icons (unicode arrows) look small and misplaced. Zoom +/- are text characters instead of proper icons.

### Solution
- Replace unicode undo/redo (unicode arrows) with proper SVG PathIcon (Lucide `undo-2`/`redo-2` style).
- Replace text minus/plus with SVG PathIcon.
- All buttons: 28x28px minimum touch target.
- Zoom label: `InterMediumFont` for readability.
- Keep same layout: horizontal, centered bottom, floating bar.

### Files Changed
- `BottomBar.axaml`: Replace Content strings with PathIcon elements.
- `ToolIcons.axaml`: Add undo/redo/zoom SVG paths.

## 6. Settings Overlay Panel

### Trigger
Gear icon in custom title bar. Gear button is only visible/enabled on the board screen (hidden on connection screen). Toggles `MainViewModel.IsSettingsOpen`.

### Panel Design
Floating overlay inside `BoardScreen.axaml`, right-aligned, same visual style as property panel:
- Background: `BgElevated`, 1px `BorderDefault` border, 12px radius.
- Header: "SETTINGS" in `InterMediumFont` + close (X) button.
- Visibility bound to `MainViewModel.IsSettingsOpen`.

### Settings Items
| Setting | Control | Default | Runtime Effect |
|---------|---------|---------|----------------|
| Grid Opacity | Slider 0-30% | 10% | Updates `MainViewModel.GridOpacity` -> canvas redraws |
| Language | Dropdown | English | UI-only placeholder, no runtime effect in this slice |
| Default Image Folder | Text field + Browse | (empty) | UI-only placeholder, no runtime effect in this slice |
| Autosave | Toggle + interval (30s/60s/120s) | On, 60s | UI-only placeholder, autosave logic exists but wiring is future work |

### Persistence
Settings stored as JSON file in app data folder (`Environment.SpecialFolder.ApplicationData`/BFGA/settings.json). `SettingsService` handles load (on app startup in `MainViewModel` constructor) and save (on each setting change, debounced 500ms).

### Files Changed
- New: `SettingsService.cs` (in `src/BFGA.App/Services/`) — JSON load/save + debounced persist.
- New: `SettingsPanel.axaml` / `.axaml.cs` (in `src/BFGA.App/Views/`) — overlay UI.
- `MainViewModel.cs`: Add `SettingsService` field, `IsSettingsOpen` property, `GridOpacity` property.
- `BoardScreen.axaml`: Add SettingsPanel overlay (right-aligned, same layer as PropertyPanel).

## 7. DLL Lock on Close Fix

### Problem
`CloseAsync().GetAwaiter().GetResult()` in `OnClosed` blocks the UI thread. If async operations try to dispatch back to UI, this deadlocks — process stays alive with DLLs locked.

### Solution: Async Closing with Cancellation

**Shutdown sequence** (in `MainWindow`):

1. Subscribe to `Closing` event in constructor.
2. On first `Closing` call: set `e.Cancel = true` to prevent immediate close, start async cleanup on a background thread.
3. Async cleanup method (runs on `Task.Run` to avoid UI-thread deadlock):
   a. Create `CancellationTokenSource` with 3-second timeout.
   b. Call `MainViewModel.CloseAsync(cancellationToken)` which does in order:
      - Stop `_pollTimer` (synchronous, immediate).
      - If client connected: `Client?.Dispose()` (existing API, synchronous, fast).
      - If host running: `Host?.Dispose()` + existing `StopHostAsync()` wrapped in `Task.Run` with the cancellation token.
      - Dispose remaining resources (same as current `Dispose()` method).
   c. On completion (or timeout via `OperationCanceledException`): dispatch back to UI thread and call `Close()` again. The second `Closing` call is detected via a `_isClosing` flag and allowed through.
4. If cleanup times out: log warning, proceed with `Close()` anyway — the OS will reclaim resources on process exit.

**Why `Task.Run`**: Current `IGameHostSession`/`IGameClientSession` APIs use `Dispose()` for cleanup (no `Disconnect()` method). Wrapping cleanup in `Task.Run` moves it off the UI thread, making the 3-second `CancellationTokenSource` timeout actually enforceable via `Task.WhenAny(cleanupTask, Task.Delay(timeout))`.

**Key change to `MainViewModel.CloseAsync`**: Accept `CancellationToken` parameter (default `CancellationToken.None` for backward compat). Check token between cleanup steps.

### Files Changed
- `MainWindow.axaml.cs`: Replace `OnClosed` override with `Closing` event handler + `_isClosing` flag + async cleanup.
- `MainViewModel.cs`: Add `CancellationToken` parameter to `CloseAsync`, pass through to host/client cleanup.

## Testing Strategy

Per user direction, skip heavy test additions — these are primarily UI changes. Verify via:
- Manual testing: launch app, test each tool switch, settings, zoom, close/reopen.
- Build verification: `dotnet build` must succeed.
- Existing tests: `dotnet test` must not regress (248 tests pass).

## Out of Scope
- Canvas interaction logic for new tools (Arrow, Line, Text, Laser) — only tool switching and UI.
- Internationalization implementation — Language dropdown is a placeholder.
- Autosave implementation details — just the settings toggle; save logic exists from prior work.
