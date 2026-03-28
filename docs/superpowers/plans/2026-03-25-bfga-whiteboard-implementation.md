# BFGA Whiteboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working BFGA desktop MVP: a hostable collaborative infinite canvas with drawing, image embedding, save/load, and live cursor sync.

**Architecture:** A single Avalonia app will own UI, while `BFGA.Core` holds serializable board models and operations, `BFGA.Network` handles LiteNetLib host/client sync, and `BFGA.Canvas` renders the board via SkiaSharp. The host remains authoritative; clients submit operations, receive broadcasts, and can export or re-open board files.

**Tech Stack:** .NET 8+, Avalonia 11+, SkiaSharp, LiteNetLib, MessagePack-CSharp, xUnit

---

## Execution Status

- Task 8 implementation and verification are complete in the current worktree; commit step is still pending because no commit was requested.
- Task 9 implementation and verification are complete in the current worktree; commit step is still pending because no commit was requested.
- Task 10 implementation and verification are complete in the current worktree; commit step is still pending because no commit was requested.
- Task 11 implementation and verification are complete in the current worktree; commit step is still pending because no commit was requested.
- Task 12 implementation and verification are complete in the current worktree; commit step is still pending because no commit was requested.
- Task 13 warning cleanup is complete in the current worktree; commit step is still pending because no commit was requested.

---

## File Map

### New solution and projects
- Create: `BFGA.sln`
- Create: `src/BFGA.Core/BFGA.Core.csproj`
- Create: `src/BFGA.Network/BFGA.Network.csproj`
- Create: `src/BFGA.Canvas/BFGA.Canvas.csproj`
- Create: `src/BFGA.App/BFGA.App.csproj`
- Create: `tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj`
- Create: `tests/BFGA.Network.Tests/BFGA.Network.Tests.csproj`

### Core domain
- Create: `src/BFGA.Core/Models/*.cs`
- Create: `src/BFGA.Core/Operations/*.cs`
- Create: `src/BFGA.Core/Serialization/*.cs`
- Create: `src/BFGA.Core/BoardState.cs`

### Networking
- Create: `src/BFGA.Network/GameHost.cs`
- Create: `src/BFGA.Network/GameClient.cs`
- Create: `src/BFGA.Network/Protocol/*.cs`

### Canvas and tools
- Create: `src/BFGA.Canvas/BoardCanvas.cs`
- Create: `src/BFGA.Canvas/Rendering/*.cs`
- Create: `src/BFGA.Canvas/Tools/*.cs`

### App shell
- Create: `src/BFGA.App/Program.cs`
- Create: `src/BFGA.App/App.axaml`
- Create: `src/BFGA.App/MainWindow.axaml`
- Create: `src/BFGA.App/ViewModels/*.cs`
- Create: `src/BFGA.App/Views/*.axaml`

### Tests
- Create: `tests/BFGA.Core.Tests/*.cs`
- Create: `tests/BFGA.Network.Tests/*.cs`
- Create: `tests/BFGA.App.Tests/*.cs`

---

### Task 1: Create solution and project scaffolding

**Files:**
- Create: `BFGA.sln`
- Create: `src/BFGA.Core/BFGA.Core.csproj`
- Create: `src/BFGA.Network/BFGA.Network.csproj`
- Create: `src/BFGA.Canvas/BFGA.Canvas.csproj`
- Create: `src/BFGA.App/BFGA.App.csproj`
- Create: `tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj`
- Create: `tests/BFGA.Network.Tests/BFGA.Network.Tests.csproj`
- Create: `tests/BFGA.App.Tests/BFGA.App.Tests.csproj`

- [ ] **Step 1: Write failing build expectations**

Create a minimal `.sln` and empty project files that reference each other exactly as the spec defines.

- [ ] **Step 2: Run build command to verify the repo is not yet runnable**

Run: `dotnet build BFGA.sln`

Expected: FAIL because source files and package references are still missing.

- [ ] **Step 3: Add project references and package references**

Add the dependencies needed for Avalonia, SkiaSharp, LiteNetLib, MessagePack, and xUnit.

- [ ] **Step 4: Run the created project builds again**

Run: `dotnet build src/BFGA.Core/BFGA.Core.csproj && dotnet build src/BFGA.Network/BFGA.Network.csproj && dotnet build src/BFGA.Canvas/BFGA.Canvas.csproj && dotnet build tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj && dotnet build tests/BFGA.Network.Tests/BFGA.Network.Tests.csproj`

Expected: PASS after all library/test projects exist and compile; the full solution build is deferred until the app shell is added.

- [ ] **Step 5: Commit**

```bash
git add BFGA.sln src tests
git commit -m "feat: scaffold BFGA solution"
```

### Task 2: Implement core board model and serialization

**Files:**
- Create: `src/BFGA.Core/BoardState.cs`
- Create: `src/BFGA.Core/Models/BoardElement.cs`
- Create: `src/BFGA.Core/Models/StrokeElement.cs`
- Create: `src/BFGA.Core/Models/ShapeElement.cs`
- Create: `src/BFGA.Core/Models/ImageElement.cs`
- Create: `src/BFGA.Core/Models/TextElement.cs`
- Create: `src/BFGA.Core/Operations/*.cs`
- Create: `src/BFGA.Core/Serialization/*.cs`
- Create: `tests/BFGA.Core.Tests/BoardStateTests.cs`
- Create: `tests/BFGA.Core.Tests/SerializationTests.cs`

- [ ] **Step 1: Write failing tests for model round-trip**

Add tests that serialize/deserialize `BoardState` with each element type, including embedded image bytes.

- [ ] **Step 2: Run tests to confirm failure**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj`

Expected: FAIL because models/formatters are not implemented.

- [ ] **Step 3: Implement the model classes and MessagePack formatters**

Use explicit, versioned MessagePack contracts so `.bfga` files stay stable as the app grows.

- [ ] **Step 4: Run core tests**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.Core tests/BFGA.Core.Tests
git commit -m "feat: add board core models"
```

### Task 3: Build file save/load for `.bfga`

**Files:**
- Modify: `src/BFGA.Core/Serialization/*.cs`
- Create: `src/BFGA.Core/BoardFileStore.cs`
- Create: `tests/BFGA.Core.Tests/BoardFileStoreTests.cs`

- [ ] **Step 1: Write failing persistence tests**

Test save/load to a temp `.bfga` file and verify elements, IDs, and embedded images survive the round-trip.

- [ ] **Step 2: Run tests and verify failure**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter BoardFileStoreTests`

Expected: FAIL.

- [ ] **Step 3: Implement board file persistence**

Write and read `BoardState` using MessagePack binary payloads.

- [ ] **Step 4: Run targeted tests**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter BoardFileStoreTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.Core tests/BFGA.Core.Tests
git commit -m "feat: add board file persistence"
```

### Task 4: Implement networking protocol and host/client wrapper

**Files:**
- Create: `src/BFGA.Network/Protocol/*.cs`
- Create: `src/BFGA.Network/GameHost.cs`
- Create: `src/BFGA.Network/GameClient.cs`
- Create: `tests/BFGA.Network.Tests/*.cs`
- Create: `tests/BFGA.App.Tests/*.cs`

- [ ] **Step 1: Write failing protocol tests**

Test message serialization for `AddElement`, `UpdateElement`, `MoveElement`, `DeleteElement`, `DrawStrokePoint`, `CancelStroke`, `CursorUpdate`, `PeerJoined`, `PeerLeft`, `RequestFullSync`, and `FullSyncResponse`.

- [ ] **Step 2: Run networking tests**

Run: `dotnet test tests/BFGA.Network.Tests/BFGA.Network.Tests.csproj`

Expected: FAIL.

- [ ] **Step 3: Assign protocol channels and implement host/client wrappers**

Use channel 0 for all reliable ordered operations and channel 1 for cursor updates, then wrap LiteNetLib connection setup, send/receive dispatch, host validation, and roster updates.

- [ ] **Step 4: Run networking tests again**

Run: `dotnet test tests/BFGA.Network.Tests/BFGA.Network.Tests.csproj`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.Network tests/BFGA.Network.Tests
git commit -m "feat: add networking protocol"
```

### Task 5: Create the board canvas control

**Files:**
- Create: `src/BFGA.Canvas/BoardCanvas.cs`
- Create: `src/BFGA.Canvas/Rendering/*.cs`
- Create: `tests/BFGA.Core.Tests/CanvasMathTests.cs`

- [ ] **Step 1: Write failing geometry/render tests**

Test coordinate transforms, hit-testing math, and stroke smoothing helpers.

- [ ] **Step 2: Run tests to confirm failure**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter CanvasMathTests`

Expected: FAIL.

- [ ] **Step 3: Implement the control and renderers**

Render board elements on SkiaSharp canvas in world coordinates, and expose the canvas through a PanAndZoom host so zoom/pan transforms work on the infinite board.

- [ ] **Step 4: Re-run tests**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter CanvasMathTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.Canvas tests/BFGA.Core.Tests
git commit -m "feat: add board canvas rendering"
```

### Task 6: Implement the MVP tool system

**Files:**
- Create: `src/BFGA.Canvas/Tools/SelectTool.cs`
- Create: `src/BFGA.Canvas/Tools/PenTool.cs`
- Create: `src/BFGA.Canvas/Tools/ShapeTool.cs`
- Create: `src/BFGA.Canvas/Tools/ImageTool.cs`
- Create: `src/BFGA.Canvas/Tools/EraserTool.cs`

- [ ] **Step 1: Write tool behavior tests**

Test selection, pen stroke creation, shape creation, image insertion, deletion, resize handles, rotate handles, and drag-box multi-select.

- [ ] **Step 2: Run tests and verify they fail**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter Tool`

Expected: FAIL.

- [ ] **Step 3: Implement the tool handlers**

Connect mouse/clipboard/drag-drop input to board operations, including selection handles, rotation handles, and marquee selection.

- [ ] **Step 4: Re-run tests**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter Tool`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.Canvas tests/BFGA.Core.Tests
git commit -m "feat: add whiteboard tools"
```

### Task 7: Build the Avalonia app shell and host/join flows

**Files:**
- Create: `src/BFGA.App/Program.cs`
- Create: `src/BFGA.App/App.axaml`
- Create: `src/BFGA.App/MainWindow.axaml`
- Create: `src/BFGA.App/ViewModels/MainViewModel.cs`
- Create: `src/BFGA.App/Views/BoardView.axaml`
- Create: `src/BFGA.App/Views/ConnectionView.axaml`

- [ ] **Step 1: Write failing UI startup tests or smoke checks**

Add a minimal startup smoke test or app-launch validation in the app project.

- [ ] **Step 2: Run the app/startup validation**

Run: `dotnet run --project src/BFGA.App/BFGA.App.csproj`

Expected: FAIL until the shell and dependencies are wired.

- [ ] **Step 3: Implement host/join UI flow and pan/zoom board shell**

Allow setting display name, choosing host vs join, entering host IP/port, and loading/saving boards. Wrap `BoardCanvas` in `Avalonia.Controls.PanAndZoom.ZoomBorder` so the main board shell supports wheel zoom and pan interactions from the start.

- [ ] **Step 4: Run the app again**

Run: `dotnet run --project src/BFGA.App/BFGA.App.csproj`

Expected: PASS and show the main board window.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.App
git commit -m "feat: add Avalonia app shell"
```

### Task 8: Separate connection screen and board screen with ViewModel-based navigation

> **Reference:** `docs/references/avalonia-implementation-guide.md` §1 (Navigation / Screen Management), `docs/references/whiteboard-ui-patterns.md` §BFGA Lobby Layout

**Status:** Steps 1-5 completed in the current worktree. Step 6 remains pending until a commit is explicitly requested.

**Context:** The current `MainWindow.axaml` shows the connection panel and the board in a single view. This task splits them into two distinct screens: a **ConnectionScreen** (lobby for host/join) and a **BoardScreen** (the whiteboard workspace). Navigation uses a `ContentControl` bound to `MainViewModel.CurrentScreen` with `DataTemplate` switching — no frame/page-based routing.

**Files:**
- Create: `src/BFGA.App/ViewModels/ConnectionScreenViewModel.cs`
- Create: `src/BFGA.App/ViewModels/BoardScreenViewModel.cs`
- Create: `src/BFGA.App/Views/ConnectionScreen.axaml` (+ code-behind)
- Create: `src/BFGA.App/Views/BoardScreen.axaml` (+ code-behind)
- Modify: `src/BFGA.App/ViewModels/MainViewModel.cs` — extract connection logic to `ConnectionScreenViewModel`, add `CurrentScreen` property, add navigation methods
- Modify: `src/BFGA.App/MainWindow.axaml` — replace inline layout with `ContentControl` + `DataTemplate` switching
- Modify: `tests/BFGA.App.Tests/MainViewModelTests.cs` — update existing tests, add navigation tests

- [x] **Step 1: Write failing navigation tests**

Test that `MainViewModel` starts with `CurrentScreen` as `ConnectionScreenViewModel`, and that after a successful host start or join connect, `CurrentScreen` switches to `BoardScreenViewModel`. Test that disconnecting returns to `ConnectionScreenViewModel`.

- [x] **Step 2: Run tests to confirm failure**

Run: `dotnet test tests/BFGA.App.Tests/BFGA.App.Tests.csproj`

Expected: FAIL because `ConnectionScreenViewModel`, `BoardScreenViewModel`, and `CurrentScreen` do not exist.

- [x] **Step 3: Implement screen ViewModels and navigation**

Implemented thin wrapper screen view models that expose `MainViewModel`, derived `CurrentScreen` from `ConnectionState`, switched `MainWindow.axaml` to `ContentControl` + `DataTemplate` navigation, and rebound `ConnectionScreen` / `BoardScreen` directly into the existing `ConnectionView` / `BoardView` so live board and collaboration updates continue to flow through Avalonia bindings.

```xml
<ContentControl Content="{Binding CurrentScreen}">
  <ContentControl.DataTemplates>
    <DataTemplate DataType="vm:ConnectionScreenViewModel">
      <views:ConnectionScreen />
    </DataTemplate>
    <DataTemplate DataType="vm:BoardScreenViewModel">
      <views:BoardScreen />
    </DataTemplate>
  </ContentControl.DataTemplates>
</ContentControl>
```

- [x] **Step 4: Run all tests**

Run: `dotnet test tests/BFGA.App.Tests/BFGA.App.Tests.csproj`

Expected: PASS.

- [x] **Step 5: Build verification**

Run: `dotnet build BFGA.sln`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/BFGA.App tests/BFGA.App.Tests
git commit -m "feat: separate connection and board screens with VM navigation"
```

---

### Task 9: Board screen layout — left toolbar, bottom bar, dot-grid background

> **Reference:** `docs/references/whiteboard-ui-patterns.md` §Tool Palette, §Canvas Background, §BFGA Board Layout. `docs/references/avalonia-implementation-guide.md` §6 (Layout), §4 (Styling), §10 (SVG Icons)

**Context:** The board screen should follow the Excalidraw/whiteboard pattern: a clean canvas with a left vertical toolbar, a bottom zoom/status bar, and a dot-grid background rendered behind board elements. Tools are icon buttons with keyboard shortcuts. The canvas occupies maximum space.

**Status:** Steps 1-6 completed in the current worktree. Step 7 remains pending until a commit is explicitly requested.

**Implemented notes:** `BoardScreen.axaml` now uses a DockPanel-style shell with dedicated `ToolBar` and `BottomBar` views, `ToolIcons.axaml` and `WhiteboardTheme.axaml` are merged through `App.axaml`, `MainWindow.axaml.cs` handles plain-key tool shortcuts only (modified chords ignored), `BoardCanvas` draws a dot-grid bounded to the visible board-space clip, and `BoardViewport.ZoomBorder` enforces the same `0.2 .. 3.0` zoom range used by the bottom-bar slider.

**Files:**
- Modify: `src/BFGA.App/Views/BoardScreen.axaml` — DockPanel layout with left toolbar, bottom bar, center canvas
- Create: `src/BFGA.App/Views/ToolBar.axaml` (+ code-behind) — vertical tool palette (48px wide)
- Create: `src/BFGA.App/Views/BottomBar.axaml` (+ code-behind) — zoom controls, connection status
- Create: `src/BFGA.App/Styles/WhiteboardTheme.axaml` — shared style resources (semi-transparent panels, rounded corners, shadows, colors)
- Create: `src/BFGA.App/Assets/ToolIcons.axaml` — StreamGeometry icon resources for tools (Select, Hand, Pen, Rectangle, Ellipse, Image, Eraser)
- Modify: `src/BFGA.Canvas/Rendering/ElementDrawingHelper.cs` — add dot-grid background rendering
- Modify: `src/BFGA.Canvas/BoardCanvas.cs` — render dot grid before elements
- Create: `tests/BFGA.Core.Tests/DotGridRenderingTests.cs` — test dot grid spacing/culling math

- [x] **Step 1: Write failing dot-grid tests**

Test that the dot-grid helper calculates correct visible dot positions for a given viewport bounds and zoom level. Test that dots are culled outside viewport. Dot spacing: 24px in board coords, dot radius: 1px screen, color: `#E0E0E0`.

- [x] **Step 2: Run tests to verify failure**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter DotGrid`

Expected: FAIL.

- [x] **Step 3: Implement dot-grid rendering**

Add a static method `DrawDotGrid(SKCanvas canvas, SKRect visibleBoardBounds, float scale)` to `ElementDrawingHelper` (or a new `BackgroundRenderer` helper). Call it in `BoardCanvas` before drawing elements.

- [x] **Step 4: Run dot-grid tests**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter DotGrid`

Expected: PASS.

- [x] **Step 5: Implement board screen AXAML layout**

Build the `BoardScreen.axaml` with:
- `DockPanel`: left `ToolBar` (48px, semi-transparent, rounded corners, tool icons as `PathIcon`), bottom `BottomBar` (32px, zoom slider/buttons, connection status indicator), center `BoardViewport`.
- `WhiteboardTheme.axaml` with shared style resources: `ToolBarBackground` (White, 95% opacity), `ToolButtonSize` (36x36), `PanelCornerRadius` (8), panel shadow.
- `ToolIcons.axaml` with `StreamGeometry` resources for each tool.
- Keyboard shortcuts per whiteboard-ui-patterns.md: V=Select, H=Hand, P=Pen, R=Rectangle, E=Ellipse, I=Image, X=Eraser.

- [x] **Step 6: Build and visual verification**

Run: `dotnet build BFGA.sln`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/BFGA.App src/BFGA.Canvas tests/BFGA.Core.Tests
git commit -m "feat: board screen layout with toolbar, bottom bar, and dot grid"
```

---

### Task 10: Wire pointer input from BoardScreen through tools to network dispatch

> **Reference:** `docs/references/avalonia-implementation-guide.md` §3 (Input Handling), §8 (Pan and Zoom coordinates). `docs/references/whiteboard-ui-patterns.md` §Canvas Behavior

**Context:** Currently `BoardToolController` exists but has no pointer event wiring from the UI. This task connects pointer events on the `BoardViewport`/`BoardScreen` to `BoardToolController`, transforms screen coords to board coords, and dispatches resulting operations through the network layer (host broadcast / client send) that was set up in the earlier board-sync work.

**Status:** Steps 1-4 completed in the current worktree. Step 5 remains pending until a commit is explicitly requested.

**Implemented notes:** Runtime pointer handling now lives in `BoardView.axaml.cs`, which maps selected toolbar tools into `BoardToolController` (including rectangle/ellipse via `ShapeType`) and publishes emitted operations on gesture completion. `MainViewModel.Board` remains the shell preview board, while host authority is updated only through explicit host APIs in `GameHost` / `IGameHostSession` (`ReplaceBoardState`, `TryApplyLocalOperation`, `BroadcastFullSync`). Host-mode load now resyncs already-connected clients, `BoardToolController.SetBoard()` preserves valid selection across shell board replacement, and `tests/BFGA.Core.Tests` disables parallelization to avoid PanAndZoom constructor collisions during full-suite runs.

**Files:**
- Modify: `src/BFGA.Canvas/BoardViewport.cs` — add `OnPointerPressed`, `OnPointerMoved`, `OnPointerReleased` overrides, transform coords via `ZoomBorder`, delegate to `BoardToolController`
- Modify: `src/BFGA.App/ViewModels/BoardScreenViewModel.cs` — expose `BoardToolController`, connect tool results to `DispatchLocalBoardOperation`
- Modify: `src/BFGA.Canvas/Tools/BoardToolController.cs` — ensure `ToolResult` includes the `BoardOperation` that was applied (not just `BoardChanged` flag)
- Create: `tests/BFGA.Core.Tests/PointerToToolTests.cs` — test coord transform → tool dispatch → operation result pipeline

- [x] **Step 1: Write failing pointer-to-operation tests**

Test that simulated pointer events at known screen coords, after a known zoom/pan transform, produce the expected board-coord operations from `BoardToolController`. For example: pen tool pointer-down at screen (100, 100) with zoom 2x and pan offset (50, 50) should create a `StrokeElement` starting at board coord (25, 25).

- [x] **Step 2: Run tests to verify failure**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter PointerToTool`

Expected: FAIL.

- [x] **Step 3: Implement pointer wiring**

Add runtime pointer handling that captures/release pointer during board gestures, transforms canvas points into board-space using the stable-origin seam, and calls `BoardToolController.HandlePointerDown/Move/Up`. Ensure `ToolResult` carries emitted `BoardOperation` values so host/client publish paths can commit them without double-applying. In host mode, commit through authoritative host APIs and resync the shell board; in joined mode, preserve optimistic local shell mutation plus send-only publish.

- [x] **Step 4: Run tests**

Run: `dotnet test BFGA.sln` and `dotnet build BFGA.sln`

Expected: PASS, except for the known `NU1603` / `NU1902` MessagePack warnings that remain deferred to Task 13.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.Canvas src/BFGA.App tests/BFGA.Core.Tests
git commit -m "feat: wire pointer input through tools to network dispatch"
```

---

### Task 11: Remote cursor and stroke preview rendering

> **Reference:** `docs/references/whiteboard-ui-patterns.md` §Collaboration Overlays. `docs/references/avalonia-implementation-guide.md` §2 (Custom Drawing)

**Context:** The earlier Task 8 board-sync work already added roster/cursor/preview state tracking in `MainViewModel` and a render seam from `MainViewModel` → `BoardView` → `BoardViewport` → `BoardCanvas`. Most of Task 11 is already present. The remaining Task 11 slice is to align the rendered collaboration overlays with the intended whiteboard UI: draw remote cursors as colored arrows with name labels, keep in-progress remote stroke previews at reduced opacity, and add focused overlay rendering tests. Collaboration state remains in `MainViewModel` for now because that is where the network/session event flow already lives; moving ownership into `BoardScreenViewModel` would be churn without improving the current boundaries.

**Status:** Steps 1-4 completed in the current worktree. Step 5 remains pending until a commit is explicitly requested.

**Implemented notes:** `BoardCanvas` now renders remote cursors as colored arrows with name labels and uses `CollaboratorOverlayHelper` for shared geometry/color/drawing seams. Remote stroke previews render at 60% opacity, focused bitmap-based overlay render tests were added in `tests/BFGA.Core.Tests/CollaboratorOverlayTests.cs`, and `MainViewModel` now reconciles placeholder overlay metadata when roster/full-sync data arrives while reusing preview point lists on the hot update path.

**Files:**
- Modify: `src/BFGA.Canvas/BoardCanvas.cs` — align remote cursor glyph and preview opacity with the intended overlay design
- Create or Modify: `src/BFGA.Canvas/Rendering/CollaboratorOverlayRenderer.cs` — only if needed to isolate overlay drawing for tests; avoid this if `BoardCanvas`-local helpers stay small
- Create: `tests/BFGA.Core.Tests/CollaboratorOverlayTests.cs` — focused cursor geometry/preview-opacity coverage
- Keep: existing app-level overlay propagation tests in `tests/BFGA.App.Tests/MainViewModelTests.cs`

- [x] **Step 1: Write failing overlay rendering tests**

Test that `CollaboratorOverlayRenderer.DrawCursors` produces the expected draw calls for a given set of remote cursors with positions, names, and colors. Test that preview strokes are drawn with 60% opacity.

- [x] **Step 2: Run tests to verify failure**

Run: `dotnet test tests/BFGA.Core.Tests/BFGA.Core.Tests.csproj --filter Collaborator`

Expected: FAIL.

- [x] **Step 3: Implement overlay renderer**

Draw each remote cursor as a small colored arrow (rotated 315° triangle) at the board position, with the user's display name rendered as a small rounded-rect label below/right. Draw each preview stroke using `StrokeSmoothingHelper` at 60% alpha of the user's assigned color.

- [x] **Step 4: Run tests**

Run: `dotnet test BFGA.sln` and `dotnet build BFGA.sln`

Expected: PASS, except for the known `NU1603` / `NU1902` MessagePack warnings that remain deferred to Task 13.

- [ ] **Step 5: Commit**

```bash
git add src/BFGA.Canvas src/BFGA.App tests
git commit -m "feat: render remote cursors and stroke previews"
```

---

### Task 12: Export, autosave, and final verification

> **Reference:** `docs/references/avalonia-implementation-guide.md` §7 (Data Binding / MVVM)

**Context:** Save/load already lives in `MainViewModel` and uses `AvaloniaFileDialogService` plus `BoardFileStore`. The low-risk Task 12 path is to extend those existing seams instead of moving file/export behavior into `BoardScreenViewModel`. Client export should request a fresh full sync before saving. Host autosave should be timer-driven during hosting and trigger one final save on window close.

**Status:** Steps 1-4 completed in the current worktree. Step 5 remains pending until a commit is explicitly requested.

**Implemented notes:** Client save now requests `RequestFullSync()` and waits for the refreshed board before prompting/writing, with timeout/disconnect cleanup so the command does not hang forever. Host autosave runs on a 60-second timer, writes to `Documents\BFGA`, and catches save failures into shell status instead of surfacing timer/shutdown exceptions. Window close routes through `CloseDataContext -> CloseAsync()` for one final host autosave before disposal, and focused Task 12 regressions now cover post-sync file contents, timeout/disconnect cleanup, autosave path behavior, and save-failure handling.

**Files:**
- Modify: `src/BFGA.App/ViewModels/MainViewModel.cs` — add export command, autosave timer, and client pre-export full-sync path
- Modify: `src/BFGA.App/MainWindow.axaml.cs` — hook the window-closing seam for final host autosave/export-safe shutdown
- Modify: `src/BFGA.App/Networking/IGameClientSession.cs` — expose `RequestFullSync()` to the app layer
- Modify: `src/BFGA.App/Networking/NetworkGameSessionFactory.cs` — forward `RequestFullSync()` to `GameClient`
- Keep: `src/BFGA.Core/BoardFileStore.cs` — reuse existing atomic save/load implementation; no change expected
- Modify: `tests/BFGA.App.Tests/MainViewModelTests.cs` — add export/autosave regressions first

- [x] **Step 1: Write export tests**

Test that a joined client requests `RequestFullSync()` before export/save, then writes the freshest board snapshot to the selected `.bfga` path. Test that host autosave starts when hosting, stops when hosting stops, and saves on the timer/closing path.

- [x] **Step 2: Run the export tests and verify they fail**

Run: `dotnet test tests/BFGA.App.Tests/BFGA.App.Tests.csproj --filter Export`

Expected: FAIL until export is implemented.

- [x] **Step 3: Implement export and autosave**

Add a client export/save action through the existing `MainViewModel` save seam: request `RequestFullSync()` before saving when `Client` is active, keep host saves authoritative via `Host.BoardState`, and add a 60-second `DispatcherTimer` autosave that runs only while hosting. Hook the window closing seam for one final host save before disposal.

- [x] **Step 4: Run the full verification suite**

Run: `dotnet test && dotnet build BFGA.sln`

Expected: PASS, except for the known `NU1603` / `NU1902` MessagePack warnings that remain deferred to Task 13.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: add export and host autosave"
```

### Task 13: Fix build warnings

> **Context:** The solution currently emits NuGet warnings for MessagePack: NU1603 (version resolution) and NU1902 (known moderate vulnerability). These should be resolved before final release.

**Status:** Steps 1-4 completed in the current worktree. Step 5 remains pending until a commit is explicitly requested.

**Implemented notes:** `MessagePack` is now pinned to `2.5.198` in `BFGA.Core` and `BFGA.Network`, removing the prior `NU1603` / `NU1902` package warnings. The warning cleanup also removed the listed nullable and xUnit analyzer warnings with minimal targeted changes, while preserving the intended app-layer non-blocking connect semantics and keeping the concrete `BFGA.Network.GameClient.ConnectAsync(...)` awaitable for direct network consumers. Fresh verification now shows `dotnet test BFGA.sln` passing and `dotnet build BFGA.sln` succeeding with `0 Warning(s)` / `0 Error(s)`.

**Files:**
- Modify: `src/BFGA.Network/BFGA.Network.csproj` — update MessagePack package version or suppress warnings with justification
- Modify: `src/BFGA.Core/BFGA.Core.csproj` — if MessagePack is referenced here, apply same fix

- [x] **Step 1: Audit current warnings**

Run: `dotnet build BFGA.sln 2>&1 | grep -i warning`

Document all warnings and their sources.

- [x] **Step 2: Research resolution options**

For NU1603 (version resolution): Update to explicit version or add `NuGetLockFilePath` workaround.
For NU1902 (vulnerability): Evaluate upgrade path to MessagePack 2.5+ or add `NuGetAudit` suppression with documented justification.

- [x] **Step 3: Apply fixes**

Update package versions or add appropriate suppressions with comments explaining why.

- [x] **Step 4: Verify clean build**

Run: `dotnet build BFGA.sln`

Expected: `dotnet build BFGA.sln` succeeds with `0 Warning(s)` / `0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "fix: resolve NuGet warnings for MessagePack"
```

---

## Verification Checklist

- [ ] `dotnet test`
- [ ] `dotnet build BFGA.sln` — no warnings
- [ ] Launch app — see connection screen first
- [ ] Host a board — navigates to board screen with dot grid, left toolbar, bottom bar
- [ ] Join from another instance — navigates to board screen after sync
- [ ] Draw a stroke with pen tool — appears on both host and client
- [ ] See remote cursor with name label on the other instance
- [ ] Switch tools via keyboard shortcuts (V, P, R, etc.)
- [ ] Drag-drop an image and confirm it persists after save/load
- [ ] Export a board from a client and reopen it in host mode
- [ ] Disconnect — returns to connection screen
