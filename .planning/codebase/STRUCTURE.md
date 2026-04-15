# Codebase Structure

**Analysis Date:** 2026-04-15

## Directory Layout

```
BFGA-BoardForGamingApp/
├── src/
│   ├── BFGA.Core/              # Domain models, board state, serialization
│   ├── BFGA.Network/           # P2P networking (LiteNetLib), protocol, undo/redo
│   ├── BFGA.Canvas/            # SkiaSharp rendering, tools, viewport
│   └── BFGA.App/               # Avalonia desktop app (MVVM shell)
├── tests/
│   ├── BFGA.Core.Tests/        # Core + Canvas + Network unit tests (xUnit)
│   ├── BFGA.Network.Tests/     # Network protocol + integration tests
│   └── BFGA.App.Tests/         # ViewModel + UI logic tests
├── docs/
│   └── references/             # Design reference docs
├── .planning/                  # GSD planning artifacts
├── BFGA.sln                    # Solution file
├── AGENTS.md                   # Agent coding guidelines
├── runtests.bat                # Quick test runner script
└── .gitignore
```

## Directory Purposes

**`src/BFGA.Core/`:**
- Purpose: Domain model layer — zero UI/network dependencies
- Contains: Board state, element model hierarchy, file persistence, MessagePack serialization setup
- Key files:
  - `BoardState.cs` — Root aggregate: BoardId, BoardName, Elements list, LastModified
  - `BoardFileStore.cs` — Static async Save/Load with atomic write (temp → replace)
  - `MessagePackSetup.cs` — Shared serializer options with custom formatters + DynamicUnionResolver
  - `Models/BoardElement.cs` — Abstract base with Union attributes for StrokeElement, ShapeElement, ImageElement, TextElement
  - `Models/ShapeType.cs` — Enum: Rectangle, Ellipse, Line, Arrow
  - `Serialization/Vector2Formatter.cs` — Custom MessagePack formatter for `System.Numerics.Vector2`
  - `Serialization/SKColorFormatter.cs` — Custom MessagePack formatter for `SkiaSharp.SKColor`

**`src/BFGA.Network/`:**
- Purpose: Host-authoritative networking layer
- Contains: Host/client wrappers around LiteNetLib, protocol operations, undo/redo manager
- Key files:
  - `GameHost.cs` — Server: manages players, validates operations, broadcasts, maintains authoritative board state
  - `GameClient.cs` — Client: connects to host, sends/receives operations
  - `PlayerInfo.cs` — Player display name + assigned color
  - `UndoRedoManager.cs` — Per-user undo stacks with inverse operation tracking (max depth 50)
  - `NetworkMessagePackSetup.cs` — Delegates to Core's MessagePackSetup
  - `Protocol/BoardOperation.cs` — Abstract base + 13 concrete operation types (AddElement, UpdateElement, DeleteElement, MoveElement, CursorUpdate, DrawStrokePoint, CancelStroke, RequestFullSync, FullSyncResponse, PeerJoined, PeerLeft, Undo, Redo)
  - `Protocol/NetworkMessage.cs` — Wrapper for polymorphic serialization
  - `Protocol/OperationSerializer.cs` — Serialize/Deserialize via MessagePack

**`src/BFGA.Canvas/`:**
- Purpose: Rendering engine and drawing tool logic
- Contains: Avalonia controls for canvas/viewport, SkiaSharp rendering helpers, tool controller
- Key files:
  - `BoardCanvas.cs` — Avalonia `Control` subclass, implements `ICustomDrawOperation` for SkiaSharp rendering
  - `BoardViewport.cs` — Hosts `BoardCanvas`, manages zoom/pan with coordinate transforms
  - `Tools/BoardToolController.cs` — Central tool state machine handling all tool types (665 lines)
  - `Tools/IBoardTool.cs` — Tool interface (defined but not actively used; controller handles inline)
  - `Tools/BoardToolType.cs` — Enum: Select, Hand, Pen, Rectangle, Ellipse, Image, Eraser, Arrow, Line, Text, LaserPointer
  - `Tools/ToolResult.cs` — Result record: Handled, BoardChanged, Operations list
  - `Tools/SelectionState.cs` — Selection tracking (single/multi, handle interactions)
  - `Rendering/ElementDrawingHelper.cs` — Draws individual board elements on SKCanvas
  - `Rendering/HitTestHelper.cs` — Point/circle hit testing against board elements
  - `Rendering/ElementBoundsHelper.cs` — Bounding box calculations
  - `Rendering/DotGridHelper.cs` — Infinite dot grid background
  - `Rendering/SelectionOverlayRenderer.cs` — Selection handles + box rendering
  - `Rendering/EraserPreviewRenderer.cs` — Eraser tool preview circle
  - `Rendering/CollaboratorOverlayHelper.cs` — Remote cursor + stroke preview rendering
  - `Rendering/CollaboratorPresenceModels.cs` — `RemoteCursorState`, `RemoteStrokePreviewState` records
  - `Rendering/CoordinateTransformHelper.cs` — Board ↔ screen coordinate conversion
  - `Rendering/ImageDecodeCache.cs` — SKBitmap cache for ImageElements
  - `Rendering/ThemeColors.cs` — Color constants for canvas rendering
  - `Rendering/StrokeSmoothingHelper.cs` — Stroke point smoothing

**`src/BFGA.App/`:**
- Purpose: Application shell — MVVM pattern with Avalonia
- Subdirectories:
  - `ViewModels/` — MVVM view models
  - `Views/` — AXAML views + code-behind
  - `Services/` — Platform service interfaces and implementations
  - `Networking/` — Session abstractions and factory
  - `Infrastructure/` — MVVM infrastructure (ViewModelBase, RelayCommand)
  - `Converters/` — Avalonia value converters
  - `Helpers/` — UI utility helpers
  - `Styles/` — AXAML theme resources
  - `Assets/` — Fonts and icon geometries
- Key files:
  - `Program.cs` — App entry point
  - `App.axaml.cs` — Avalonia application class
  - `MainWindow.axaml.cs` — Window with keyboard shortcut handling (256 lines)
  - `ViewModels/MainViewModel.cs` — Central app ViewModel (1373 lines) — connection state machine, board operations, undo/redo, autosave, collaborator state
  - `ViewModels/BoardScreenViewModel.cs` — Tool selection, drawing properties, font settings
  - `ViewModels/ConnectionScreenViewModel.cs` — Thin wrapper around MainViewModel
  - `Services/SettingsService.cs` — JSON settings persistence at `%APPDATA%/BFGA/settings.json`

**`tests/BFGA.Core.Tests/`:**
- Purpose: Tests for Core, Canvas, and some Network functionality
- Note: References Core, Canvas, AND Network projects
- Key test files:
  - `BoardFileStoreTests.cs` — Save/load round-trip tests
  - `BoardStateTests.cs` — Board state manipulation
  - `BoardToolControllerTests.cs` — Tool interaction tests
  - `SerializationTests.cs` — MessagePack serialization round-trips
  - `CanvasMathTests.cs` — Coordinate transform math
  - `CollaboratorOverlayTests.cs` — Remote presence rendering
  - `DotGridRenderingTests.cs` — Dot grid drawing
  - `PointerToToolTests.cs` — Pointer event → tool interaction

**`tests/BFGA.Network.Tests/`:**
- Purpose: Network protocol and host/client tests
- Key test files:
  - `NetworkTests.cs` — Host-client integration tests
  - `ProtocolTests.cs` — Serialization round-trips for operations
  - `UndoRedoManagerTests.cs` — Undo/redo stack logic

**`tests/BFGA.App.Tests/`:**
- Purpose: ViewModel and UI logic tests
- Key test files:
  - `MainViewModelTests.cs` — Connection state machine, board operations
  - `BoardScreenViewModelTests.cs` — Tool selection, property binding
  - `MainWindowShortcutTests.cs` — Keyboard shortcut handling
  - `BoardViewPipelineTests.cs` — Board view rendering pipeline
  - `BoardScreenLayoutTests.cs` — Layout verification
  - `StartupSmokeTests.cs` — App startup verification
  - `PropertyPanelTests.cs` — Property panel visibility/binding
  - `RosterOverlayTests.cs` — Player roster UI

## Key File Locations

**Entry Points:**
- `src/BFGA.App/Program.cs` — Application main()
- `src/BFGA.App/App.axaml.cs` — Avalonia app initialization
- `src/BFGA.App/MainWindow.axaml.cs` — Main window creation + keyboard handling

**Configuration:**
- `src/BFGA.App/app.manifest` — Windows application manifest
- `src/BFGA.App/Styles/WhiteboardTheme.axaml` — Main theme
- `src/BFGA.App/Styles/Colors.axaml` — Color palette
- `src/BFGA.App/Styles/Typography.axaml` — Typography styles
- `src/BFGA.App/Assets/ToolIcons.axaml` — SVG path geometries for toolbar icons

**Core Logic:**
- `src/BFGA.Core/BoardState.cs` — Root state object
- `src/BFGA.Core/Models/BoardElement.cs` — Element hierarchy root
- `src/BFGA.Network/Protocol/BoardOperation.cs` — Protocol operation hierarchy
- `src/BFGA.Network/GameHost.cs` — Authoritative host (785 lines)
- `src/BFGA.Canvas/Tools/BoardToolController.cs` — Tool state machine (665 lines)
- `src/BFGA.App/ViewModels/MainViewModel.cs` — App state machine (1373 lines)

**Testing:**
- `tests/BFGA.Core.Tests/` — Core + Canvas tests
- `tests/BFGA.Network.Tests/` — Protocol + networking tests
- `tests/BFGA.App.Tests/` — ViewModel + UI tests

## Naming Conventions

**Files:**
- C# classes: PascalCase matching class name (e.g., `BoardElement.cs`, `GameHost.cs`)
- Avalonia views: PascalCase with `.axaml` + `.axaml.cs` pair (e.g., `BoardView.axaml`)
- Tests: `{ClassUnderTest}Tests.cs` (e.g., `BoardFileStoreTests.cs`)
- Enums: PascalCase in own file (e.g., `ShapeType.cs`, `BoardToolType.cs`)

**Directories:**
- PascalCase for C# namespaces (e.g., `ViewModels/`, `Rendering/`, `Protocol/`)

**Namespaces:**
- `BFGA.Core` — domain models
- `BFGA.Core.Models` — element types
- `BFGA.Core.Serialization` — custom formatters
- `BFGA.Network` — host/client
- `BFGA.Network.Protocol` — operations
- `BFGA.Canvas` — canvas controls
- `BFGA.Canvas.Tools` — tool abstractions
- `BFGA.Canvas.Rendering` — rendering helpers
- `BFGA.App` — app root
- `BFGA.App.ViewModels` — MVVM viewmodels
- `BFGA.App.Views` — Avalonia views
- `BFGA.App.Services` — platform services
- `BFGA.App.Networking` — session abstractions
- `BFGA.App.Infrastructure` — MVVM plumbing
- `BFGA.App.Converters` — value converters
- `BFGA.App.Helpers` — UI helpers

## Where to Add New Code

**New Board Element Type:**
- Model: `src/BFGA.Core/Models/{Name}Element.cs` (extend `BoardElement`, add `[Union]` key in `BoardElement.cs`)
- Rendering: `src/BFGA.Canvas/Rendering/ElementDrawingHelper.cs` (add draw case)
- Hit testing: `src/BFGA.Canvas/Rendering/HitTestHelper.cs` (add bounds case)
- Bounds: `src/BFGA.Canvas/Rendering/ElementBoundsHelper.cs`
- Tests: `tests/BFGA.Core.Tests/SerializationTests.cs` (round-trip), `tests/BFGA.Core.Tests/BoardToolControllerTests.cs`

**New Drawing Tool:**
- Enum: Add to `src/BFGA.Canvas/Tools/BoardToolType.cs`
- Logic: Add cases in `src/BFGA.Canvas/Tools/BoardToolController.cs` (HandlePointerDown/Move/Up)
- UI: Add tool button in `src/BFGA.App/Views/ToolBar.axaml`, command in `src/BFGA.App/ViewModels/BoardScreenViewModel.cs`
- Shortcut: Add key in `src/BFGA.App/MainWindow.axaml.cs` `TryHandleToolShortcut()`
- Icon: Add geometry in `src/BFGA.App/Assets/ToolIcons.axaml`

**New Network Operation:**
- Operation class: Add to `src/BFGA.Network/Protocol/BoardOperation.cs` (extend `BoardOperation`, add `[Union]` key)
- Enum: Add to `OperationType` in same file
- Host handling: Add case in `src/BFGA.Network/GameHost.cs` `HandleOperation()` / `ApplyOperationCore()`
- Client handling: Add case in `src/BFGA.App/ViewModels/MainViewModel.cs` `ApplyInboundOperation()`
- Tests: `tests/BFGA.Network.Tests/ProtocolTests.cs`

**New View/Screen:**
- View: `src/BFGA.App/Views/{Name}.axaml` + `{Name}.axaml.cs`
- ViewModel: `src/BFGA.App/ViewModels/{Name}ViewModel.cs`
- Wire up in `MainViewModel.CurrentScreen` or parent view's DataTemplate

**New Platform Service:**
- Interface: `src/BFGA.App/Services/I{Name}Service.cs`
- Implementation: `src/BFGA.App/Services/Avalonia{Name}Service.cs`
- Inject via `MainWindow` constructor into `MainViewModel`

**New Custom Formatter (Serialization):**
- Formatter: `src/BFGA.Core/Serialization/{Type}Formatter.cs`
- Register in: `src/BFGA.Core/MessagePackSetup.cs` formatters array

## Special Directories

**`.planning/`:**
- Purpose: GSD planning and codebase analysis docs
- Generated: Yes (by GSD commands)
- Committed: Yes

**`docs/references/`:**
- Purpose: Design reference documents
- Key files: `avalonia-implementation-guide.md`, `whiteboard-ui-patterns.md`
- Generated: No
- Committed: No (in .gitignore)

**`bin/` and `obj/` (per project):**
- Purpose: Build output
- Generated: Yes
- Committed: No (in .gitignore)

**`.opencode/`, `.superpowers/`, `.worktrees/`:**
- Purpose: Tool-specific config/workspace
- Generated: Yes
- Committed: No (in .gitignore)

---

*Structure analysis: 2026-04-15*
