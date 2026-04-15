# Architecture

**Analysis Date:** 2026-04-15

## Pattern Overview

**Overall:** Layered architecture with MVVM UI pattern and operation-based networking

**Key Characteristics:**
- 4-project solution: Core → Network → Canvas → App (dependency direction)
- MVVM in App layer using hand-rolled `ViewModelBase` (no CommunityToolkit)
- Operation-based protocol: all board mutations expressed as `BoardOperation` subtypes
- Host-authoritative networking: host validates/applies ops then broadcasts to clients
- MessagePack binary serialization for both file persistence and network protocol
- SkiaSharp rendering via Avalonia's `ICustomDrawOperation` for GPU-accelerated canvas

## Layers

**BFGA.Core (Domain Models):**
- Purpose: Board state, element models, serialization infrastructure
- Location: `src/BFGA.Core/`
- Contains: `BoardState`, `BoardElement` hierarchy, `BoardFileStore`, MessagePack formatters
- Depends on: MessagePack, SkiaSharp (for `SKColor` in models)
- Used by: All other projects

**BFGA.Network (Networking):**
- Purpose: P2P networking protocol, host/client management, undo/redo
- Location: `src/BFGA.Network/`
- Contains: `GameHost`, `GameClient`, `BoardOperation` hierarchy, `UndoRedoManager`, `OperationSerializer`
- Depends on: BFGA.Core, LiteNetLib, MessagePack
- Used by: BFGA.Canvas, BFGA.App

**BFGA.Canvas (Rendering & Tools):**
- Purpose: SkiaSharp-based canvas rendering, drawing tools, hit testing, viewport management
- Location: `src/BFGA.Canvas/`
- Contains: `BoardCanvas`, `BoardViewport`, `BoardToolController`, rendering helpers, tool abstractions
- Depends on: BFGA.Core, BFGA.Network (for `BoardOperation` in `ToolResult`), Avalonia, Avalonia.Skia, SkiaSharp
- Used by: BFGA.App

**BFGA.App (Application Shell):**
- Purpose: Avalonia desktop app, MVVM viewmodels, views, platform services
- Location: `src/BFGA.App/`
- Contains: ViewModels, Views (AXAML), Services, Networking adapters, Converters, Styles
- Depends on: BFGA.Core, BFGA.Network, BFGA.Canvas, Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent
- Used by: End user (executable)

## Project Dependency Graph

```
BFGA.App ──→ BFGA.Core
    │    ──→ BFGA.Network ──→ BFGA.Core
    │    ──→ BFGA.Canvas  ──→ BFGA.Core
    │                      ──→ BFGA.Network
```

- `BFGA.Core` has zero project references (leaf dependency)
- `BFGA.Network` references only `BFGA.Core`
- `BFGA.Canvas` references `BFGA.Core` and `BFGA.Network` (needs `BoardOperation` types for `ToolResult`)
- `BFGA.App` references all three

## Key Abstractions

**BoardElement Hierarchy (Core Domain):**
- Base: `BoardElement` (abstract) — `src/BFGA.Core/Models/BoardElement.cs`
- Subtypes: `StrokeElement`, `ShapeElement`, `TextElement`, `ImageElement`
- MessagePack `[Union]` discriminated — keys 0–3
- Common properties: Id, Position, Size, Rotation, ZIndex, OwnerId, IsLocked

**BoardOperation Hierarchy (Protocol):**
- Base: `BoardOperation` (abstract) — `src/BFGA.Network/Protocol/BoardOperation.cs`
- 13 subtypes: AddElement, UpdateElement, DeleteElement, MoveElement, CursorUpdate, DrawStrokePoint, CancelStroke, RequestFullSync, FullSyncResponse, PeerJoined, PeerLeft, Undo, Redo
- MessagePack `[Union]` discriminated — keys 0–12
- Common properties: Type (enum), SenderId, Timestamp

**IBoardTool Interface:**
- Location: `src/BFGA.Canvas/Tools/IBoardTool.cs`
- Methods: `PointerDown`, `PointerMove`, `PointerUp` accepting `ToolContext` + `ToolInput`, returning `ToolResult`
- Note: Currently NOT used directly. `BoardToolController` handles all tools inline via switch statements.

**Session Abstractions (App Layer):**
- `IGameHostSession` — `src/BFGA.App/Networking/IGameHostSession.cs`
- `IGameClientSession` — `src/BFGA.App/Networking/IGameClientSession.cs`
- `IGameSessionFactory` — `src/BFGA.App/Networking/IGameSessionFactory.cs`
- `NetworkGameSessionFactory` — adapts `GameHost`/`GameClient` via private adapter classes

**Platform Service Interfaces:**
- `IFileDialogService` — `src/BFGA.App/Services/IFileDialogService.cs`
- `IClipboardService` — `src/BFGA.App/Services/IClipboardService.cs`
- Implementations: `AvaloniaFileDialogService`, `AvaloniaClipboardService`

**ViewModelBase:**
- Location: `src/BFGA.App/Infrastructure/ViewModelBase.cs`
- Hand-rolled `INotifyPropertyChanged` with `SetProperty<T>` helper
- No dependency on CommunityToolkit or other MVVM frameworks

## Data Flow

**Board Mutation (Host Mode):**
1. User interacts with canvas → `BoardToolController.HandlePointerDown/Move/Up()`
2. Tool returns `ToolResult` with `BoardOperation` list
3. `BoardView` code-behind dispatches ops → `MainViewModel.DispatchLocalBoardOperation()`
4. `MainViewModel` calls `Host.TryApplyLocalOperation()` → validates + applies + pushes to undo stack
5. `Host.BroadcastOperation()` sends to all clients via LiteNetLib
6. `MainViewModel.SyncBoardFromHost()` clones board state → triggers binding update
7. `BoardCanvas.Board` property change → `InvalidateVisual()` → SkiaSharp re-render

**Board Mutation (Client Mode):**
1. Same steps 1-2 as host mode
2. `MainViewModel.DispatchLocalBoardOperation()` applies locally AND sends to host via `Client.SendOperation()`
3. Host validates, applies, pushes undo, broadcasts back to all clients
4. Client receives broadcast via `OnClientOperationReceived` → `ApplyInboundOperation()`
5. Board state updated, UI refreshes

**Network Polling:**
- `DispatcherTimer` at 50ms interval calls `PollNetwork()` on UI thread
- `Host.PollEvents()` / `Client.PollEvents()` → LiteNetLib processes queued packets
- Events fire synchronously on UI thread (no marshaling needed)

**Rendering Pipeline:**
- `BoardCanvas.Render()` creates `BoardDrawOperation` (implements `ICustomDrawOperation`)
- Avalonia calls `BoardDrawOperation.Render()` on render thread with `ISkiaSharpApiLeaseFeature`
- Gets raw `SKCanvas` → draws background, dot grid, sorted elements, selection overlay, eraser preview, remote cursors
- State is snapshot on UI thread in `BoardDrawOperation` constructor to avoid cross-thread issues

**File Persistence:**
- `BoardFileStore.SaveAsync()` / `LoadAsync()` — `src/BFGA.Core/BoardFileStore.cs`
- Format: MessagePack binary (`.bfga` extension)
- Atomic write: temp file → File.Replace
- Autosave: `DispatcherTimer` at 1-minute interval → `%USERPROFILE%/Documents/BFGA/{boardname}.bfga`

**State Management:**
- `MainViewModel` owns canonical `BoardState` instance
- In host mode: `GameHost` has authoritative copy; MainViewModel clones after each mutation
- In client mode: MainViewModel applies ops locally (optimistic) and also sends to host
- Immutable state updates via MessagePack clone: `Serialize → Deserialize` pattern
- Remote presence (cursors, stroke previews) managed as immutable dictionaries on MainViewModel

## Entry Points

**Application Entry:**
- Location: `src/BFGA.App/Program.cs`
- `Main()` → `BuildAvaloniaApp()` → `StartWithClassicDesktopLifetime()`
- `App.OnFrameworkInitializationCompleted()` creates `MainWindow`
- `MainWindow` constructor creates `MainViewModel` with platform service implementations

**Screen Navigation:**
- `MainViewModel.CurrentScreen` property returns either `ConnectionScreenViewModel` or `BoardScreenViewModel`
- Switches based on `ConnectionState` (Hosting/Connected → Board, else → Connection)
- Views bound via DataTemplates in AXAML

## Error Handling

**Strategy:** Status bar text + catch-and-display at command boundaries

**Patterns:**
- `AsyncRelayCommand` wraps async operations with try/catch → `HandleShellError()` updates `StatusText`
- Network errors: `Debug.WriteLine` for logging, malformed packets silently dropped
- `BoardFileStore`: validates arguments, wraps deserialization errors in `InvalidOperationException`
- Host operation validation: rejects invalid operations (missing elements, host-only ops from clients)
- Errors do NOT propagate to user as dialogs — status bar only

## Cross-Cutting Concerns

**Logging:**
- `Debug.WriteLine` in network layer (host/client message tracing)
- `BoardDebugLogger` — optional file-based debug logging, enabled via `BFGA_BOARD_DEBUG_LOG=1` env var
- No structured logging framework

**Validation:**
- `GameHost.ValidateOperation()` — rejects host-only ops from clients, checks element existence for updates/moves/deletes
- `BoardFileStore` — argument validation on save/load
- No input validation framework (e.g., FluentValidation)

**Serialization:**
- MessagePack with `DynamicUnionResolver` for polymorphic types
- Custom formatters: `Vector2Formatter`, `SKColorFormatter` in `src/BFGA.Core/Serialization/`
- Two setup classes: `MessagePackSetup` (Core), `NetworkMessagePackSetup` (Network, delegates to Core)
- `UntrustedData` security mode enabled

**Undo/Redo:**
- `UndoRedoManager` — `src/BFGA.Network/UndoRedoManager.cs`
- Per-user undo stacks (max 50 depth)
- Inverse operations computed at apply time
- Host-only: clients send `UndoOperation`/`RedoOperation` requests, host resolves to concrete ops

**Settings Persistence:**
- `SettingsService` — `src/BFGA.App/Services/SettingsService.cs`
- JSON file at `%APPDATA%/BFGA/settings.json`
- Debounced save (500ms) on property changes

**Caching:**
- `ImageDecodeCache` in `src/BFGA.Canvas/Rendering/ImageDecodeCache.cs` — caches decoded SKBitmap for ImageElements
- No network-level or state caching

---

*Architecture analysis: 2026-04-15*
