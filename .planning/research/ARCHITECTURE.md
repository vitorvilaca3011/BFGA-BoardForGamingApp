# Architecture Research

**Domain:** Collaborative whiteboard — laser pointer tool
**Researched:** 2026-04-15
**Confidence:** HIGH

Laser pointer = ephemeral, press-and-hold, dot + fading trail, multiplayer-visible, never persisted. Architectural model: same as `CursorUpdateOperation` + `RemoteStrokePreviewState` combined — transient presence data with streaming points.

## Integration Architecture

### Design Principle: Follow CursorUpdate Pattern, Not Element Pattern

Laser is NOT a board element. It's ephemeral presence data like cursor position. Key insight from codebase:

- `CursorUpdateOperation` → relay-only in `GameHost.ApplyOperationCore()` (no matching case, falls through)
- `IsOperationReliable()` returns `false` for `CursorUpdateOperation` — unreliable channel
- `MainViewModel.ApplyInboundOperation()` short-circuits cursor/stroke ops before `ApplyLocalBoardOperation()`
- Rendered as overlays in `BoardDrawOperation.Render()` — after elements, before nothing

Laser follows this exact pattern: broadcast points via unreliable channel, render as overlay, never touch `BoardState`.

### New Components

| Component | Layer | File Path | Responsibility |
|-----------|-------|-----------|----------------|
| `LaserPointerOperation` | Network | `src/BFGA.Network/Protocol/BoardOperation.cs` | New `BoardOperation` subtype. Fields: `Position` (Vector2), `Color` (SKColor), `IsActive` (bool). Union key 13. |
| `LaserPointerState` | Canvas | `src/BFGA.Canvas/Rendering/LaserPointerModels.cs` | Local trail state model. Fields: `ClientId`, `DisplayName`, `Color`, `Points` (timestamped list for fade calc). |
| `LaserTrailBuffer` | Canvas | `src/BFGA.Canvas/Rendering/LaserTrailBuffer.cs` | Ring buffer of `(Vector2 Position, long TimestampMs)` tuples. Manages point aging, fade calculation, pruning expired points (>2s). Shared by local and remote trails. |
| `LaserPointerRenderer` | Canvas | `src/BFGA.Canvas/Rendering/LaserPointerRenderer.cs` | Static helper (like `CollaboratorOverlayHelper`). Draws dot at head + fading trail with per-point alpha based on age. |
| `LaserPointerTool` | Canvas | `src/BFGA.Canvas/Tools/LaserPointerTool.cs` | Implements `IBoardTool`. First tool to actually use the interface. Manages local trail buffer, returns `ToolResult` with `LaserPointerOperation` list. |
| Remote laser state management | App | `src/BFGA.App/ViewModels/MainViewModel.cs` | New dictionary property `RemoteLaserPointers` + upsert/remove methods (follows `RemoteCursors` pattern exactly). |
| `LaserPointersProperty` | Canvas | `src/BFGA.Canvas/BoardCanvas.cs` | New `StyledProperty<IReadOnlyDictionary<Guid, LaserPointerState>?>` — bound from `MainViewModel.RemoteLaserPointers`. |
| Render call | Canvas | `src/BFGA.Canvas/BoardCanvas.cs` | In `BoardDrawOperation.Render()` — call `LaserPointerRenderer.Draw()` after remote cursors (topmost overlay). |

### Data Flow

**Local path (pointer pressed → trail visible locally):**
```
1. User presses pointer with LaserPointer tool active
2. BoardView.HandlePointerPressed → BoardToolController.HandlePointerDown
3. Controller delegates to LaserPointerTool.PointerDown
4. LaserPointerTool creates LaserTrailBuffer, adds first point
5. Returns ToolResult with LaserPointerOperation(position, color, isActive=true)
6. BoardView.ApplyToolResult dispatches operation via MainViewModel
7. Local trail rendered directly from LaserPointerTool state (no round-trip needed)
```

**Pointer move (streaming):**
```
1. User moves pointer while pressed
2. BoardToolController → LaserPointerTool.PointerMove
3. Adds point to local LaserTrailBuffer
4. Returns ToolResult with LaserPointerOperation(position, color, isActive=true)
5. Operation dispatched to network (unreliable channel)
6. BoardView invalidates canvas → LaserPointerRenderer draws from local buffer
```

**Pointer released:**
```
1. BoardToolController → LaserPointerTool.PointerUp
2. Returns ToolResult with LaserPointerOperation(position, color, isActive=false)
3. Local trail continues fading via timer until all points expire
```

**Network path (remote peer → local render):**
```
1. Remote peer sends LaserPointerOperation (unreliable channel)
2. GameHost receives → ValidateOperation passes → ApplyOperationCore (no-op, falls through) → BroadcastOperation
3. Local client receives in ApplyInboundOperation → case LaserPointerOperation
4. MainViewModel.UpsertRemoteLaserPointer() updates RemoteLaserPointers dictionary
5. BoardCanvas.RemoteLaserPointers property change → InvalidateVisual()
6. BoardDrawOperation snapshots state → LaserPointerRenderer draws remote trails
```

**Fade/expiry (timer-driven):**
```
1. DispatcherTimer at ~16ms (60fps) or 33ms (30fps) ticks
2. For local trail: prune expired points from LaserTrailBuffer, invalidate if changed
3. For remote trails: prune expired remote laser states, update RemoteLaserPointers
4. When all points expired + isActive=false → remove laser state entirely
```

### Integration Points with Existing Code

| Existing Code | Integration | Notes |
|---------------|-------------|-------|
| `BoardOperation.cs` (Network) | Add `LaserPointerOperation` class + Union key 13 + `OperationType.LaserPointer = 13` | Extends existing MessagePack union hierarchy |
| `GameHost.IsOperationReliable()` | Add `LaserPointerOperation` to unreliable list: `operation is not (CursorUpdateOperation or LaserPointerOperation)` | Laser = high-frequency ephemeral, must be unreliable |
| `GameClient.SendOperation()` | Add `LaserPointerOperation` to unreliable channel check (same pattern as `CursorUpdateOperation`) | Line ~162 |
| `GameHost.ApplyOperationCore()` | No change needed — laser op has no matching case, falls through harmlessly (same as CursorUpdate) | Verify no side effects from `_boardState.LastModified` update |
| `MainViewModel.ApplyInboundOperation()` | Add `case LaserPointerOperation` before the `ApplyLocalBoardOperation` call (short-circuit, like CursorUpdate) | Around line 916 |
| `MainViewModel` properties | Add `RemoteLaserPointers` dictionary property + `UpsertRemoteLaserPointer` / `RemoveRemoteLaserPointer` methods | Follow `RemoteCursors` pattern exactly |
| `BoardToolController.HandlePointerDown/Move/Up` | Add `case BoardToolType.LaserPointer` → delegate to `LaserPointerTool` instance | First tool to use `IBoardTool` interface properly, or inline like others |
| `BoardCanvas.cs` | Add `LaserPointersProperty` styled property + snapshot in `BoardDrawOperation` ctor + render call | After remote cursors in render order |
| `BoardView.axaml` | Bind `LaserPointers="{Binding RemoteLaserPointers}"` on BoardCanvas | Same pattern as RemoteCursors binding |
| `BoardView.axaml.cs` | Add local laser trail rendering (from tool state, not network) + fade timer | Local trail needs separate render path from remote |
| `BoardToolType.cs` | Already has `LaserPointer` — no change needed | Enum value exists |
| `ToolBar.axaml` | Already has laser pointer button — no change needed | Button exists, binds to command |
| `BoardScreenViewModel.cs` | Already has `LaserPointerToolCommand` — no change needed | Command exists |

### Key Design Decision: Local vs Remote Trail State

**Local trail**: Managed by `LaserPointerTool` directly. Rendered from tool state, not from network round-trip. This gives zero-latency local feedback.

**Remote trails**: Managed by `MainViewModel.RemoteLaserPointers` dictionary. Updated via `LaserPointerOperation` inbound. Rendered via `BoardCanvas` styled property.

Both use same `LaserTrailBuffer` class for point aging/fade calculation. Different ownership, same rendering logic.

### LaserTrailBuffer Design

```
LaserTrailBuffer:
  - _points: List<(Vector2 Position, long TimestampMs)>
  - FadeDurationMs: 1500 (configurable, 1-2s range)
  - AddPoint(Vector2 position): adds with current timestamp
  - PruneExpired(): removes points older than FadeDurationMs
  - GetRenderPoints(): returns points with computed alpha (1.0 at newest → 0.0 at oldest)
  - IsEmpty: true when no active points
  - Clear(): removes all points
```

### Fade Timer Strategy

Two options examined:

**Option A: Per-frame timer on BoardView** — A `DispatcherTimer` at ~30fps that prunes expired laser points and invalidates canvas. Simple, works for both local + remote trails.

**Option B: Avalonia animation system** — Use `DispatcherTimer` or `CompositionAnimator`. More complex, no real benefit for point pruning.

**Recommendation: Option A.** Single `DispatcherTimer` at 33ms. Only runs while any laser trail is active (start on first point, stop when all trails empty). Prunes local + remote buffers, invalidates canvas if changed.

### Network Throttling

`CursorUpdateOperation` sends at ~60fps (every pointer move). Laser should throttle to ~30-40 updates/sec to reduce bandwidth. Two points of throttling:

1. **Client-side**: `LaserPointerTool.PointerMove` — only emit `LaserPointerOperation` if >25ms since last send. Still add every point to local buffer for smooth rendering.
2. **Deduplication**: skip if position unchanged from last sent point (mouse paused).

## Suggested Build Order

Dependencies flow bottom-up through layers. Each step is testable independently.

### Phase 1: Core Data Types (Network layer)
1. **`LaserPointerOperation`** in `BoardOperation.cs` — Add `OperationType.LaserPointer = 13`, new class with Union key 13, fields: `Position`, `Color`, `IsActive`
2. **Protocol tests** in `tests/BFGA.Network.Tests/ProtocolTests.cs` — Round-trip serialization test
3. **`GameHost` relay** — Add to `IsOperationReliable()`, verify pass-through in `ApplyOperationCore`
4. **`GameClient` channel** — Add unreliable channel handling for `LaserPointerOperation`

*Why first:* Everything else depends on the network operation type existing.

### Phase 2: Trail Buffer + Renderer (Canvas layer)
5. **`LaserTrailBuffer`** in `src/BFGA.Canvas/Rendering/` — Point buffer with timestamp-based aging
6. **`LaserPointerState`** model in `src/BFGA.Canvas/Rendering/LaserPointerModels.cs`
7. **`LaserPointerRenderer`** in `src/BFGA.Canvas/Rendering/` — SkiaSharp drawing: dot + fading trail
8. **Unit tests** — Buffer pruning, fade alpha calculation, renderer output

*Why second:* Renderer is needed by both tool (local) and canvas (remote). No network dependency.

### Phase 3: Tool Implementation (Canvas layer)
9. **`LaserPointerTool`** in `src/BFGA.Canvas/Tools/` — Implements `IBoardTool` or standalone class. Owns local `LaserTrailBuffer`. PointerDown/Move/Up produce `LaserPointerOperation`.
10. **`BoardToolController` integration** — Add `case BoardToolType.LaserPointer` in HandlePointerDown/Move/Up
11. **Tool tests** — Press-and-hold gesture, operation emission, throttling

*Why third:* Needs both the operation type (Phase 1) and the trail buffer (Phase 2).

### Phase 4: Remote State + Rendering Integration (App + Canvas layers)
12. **`MainViewModel` remote state** — `RemoteLaserPointers` property, upsert/remove methods, wire into `ApplyInboundOperation`
13. **`BoardCanvas` property** — `LaserPointersProperty` styled property, snapshot in `BoardDrawOperation`, render call
14. **`BoardView` bindings** — Bind `LaserPointers` on BoardCanvas, bind local laser overlay
15. **Fade timer** — `DispatcherTimer` for point expiry

*Why fourth:* Full integration requires all preceding components.

### Phase 5: Polish
16. **Configurable color** — Wire `BoardScreenViewModel.StrokeColor` to laser color, or add dedicated laser color setting
17. **Cleanup on peer disconnect** — Remove laser state in `RemoveRosterEntry` path
18. **Edge cases** — Tool switch while laser active, window focus loss, very fast mouse movement

## Anti-Patterns to Avoid

### Anti-Pattern 1: Laser as BoardElement
**Why bad:** Laser is ephemeral. Adding it to `BoardState.Elements` means it gets serialized, saved, synced on full-sync, included in undo/redo. All wrong.
**Instead:** Treat as presence overlay (like cursors). Separate state, separate render pass.

### Anti-Pattern 2: Reliable delivery for laser ops
**Why bad:** Laser updates are high-frequency (~30/sec per user). Reliable ordered delivery adds latency and congestion. Lost points are invisible — the trail is continuously updated.
**Instead:** Unreliable channel (same as `CursorUpdateOperation`). Accept occasional dropped points.

### Anti-Pattern 3: Timer-based trail without timestamps
**Why bad:** If you track trail age by "frames since added" instead of wall-clock timestamps, frame rate changes cause inconsistent fade behavior.
**Instead:** Store `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` per point. Compute fade alpha from `(now - pointTime) / fadeDurationMs`.

### Anti-Pattern 4: Blocking BoardToolController growth
**Why bad:** Controller is 665 lines with inline switch handling for all tools. Adding laser inline makes it worse.
**Instead:** Use `LaserPointerTool` as standalone class. Controller delegates to it. First step toward using `IBoardTool` interface as intended.

### Anti-Pattern 5: Network round-trip for local trail
**Why bad:** Sending laser point to host, waiting for broadcast back, then rendering = visible latency on local user's laser.
**Instead:** Render local trail directly from `LaserPointerTool` state. Network ops are fire-and-forget for remote visibility only.

### Anti-Pattern 6: Skipping `IsActive=false` on release
**Why bad:** If user releases mouse and the deactivation packet is dropped (unreliable channel), remote peers see frozen laser forever.
**Instead:** Send `IsActive=false` on pointer-up via RELIABLE delivery (just the stop signal). Also implement timeout: if no laser update received for 3s, auto-remove remote state.

### Anti-Pattern 7: Modifying `_boardState.LastModified` for laser
**Why bad:** `GameHost.ApplyOperationCore()` always sets `_boardState.LastModified = DateTime.UtcNow` at the end. Laser ops falling through the switch still trigger this. This could cause unnecessary autosave triggers.
**Instead:** Add explicit `case LaserPointerOperation: return;` before the switch fall-through in `ApplyOperationCore`, or guard `LastModified` update to only fire when actual state changed.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                     BFGA.App                            │
│                                                          │
│  BoardView.axaml.cs          MainViewModel               │
│  ┌──────────────┐    ┌─────────────────────────┐        │
│  │ Pointer      │    │ RemoteLaserPointers      │        │
│  │ Events       │───>│ (Dict<Guid, LaserState>) │        │
│  │              │    │                           │        │
│  │ Fade Timer   │    │ UpsertRemoteLaser()      │        │
│  │ (33ms tick)  │    │ RemoveRemoteLaser()      │        │
│  └──────┬───────┘    └────────┬────────────────┘        │
│         │                     │                          │
│         │ binds               │ binds                    │
│         ▼                     ▼                          │
│  ┌──────────────────────────────────────────────┐       │
│  │              BoardCanvas                       │       │
│  │  LaserPointersProperty ──> BoardDrawOperation  │       │
│  │                            └─> LaserRenderer   │       │
│  └──────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────┘
                         │
┌────────────────────────┼────────────────────────────────┐
│                  BFGA.Canvas                             │
│                        │                                  │
│  Tools/                │         Rendering/               │
│  ┌─────────────────┐   │   ┌──────────────────────┐     │
│  │ LaserPointerTool│   │   │ LaserTrailBuffer      │     │
│  │ (IBoardTool)    │───┼──>│ (timestamped points)  │     │
│  │                 │   │   ├──────────────────────┤     │
│  │ Local buffer ●──┼───┘   │ LaserPointerRenderer  │     │
│  │ Throttle (25ms) │       │ (dot + fading trail)  │     │
│  └────────┬────────┘       └──────────────────────┘     │
│           │                                               │
│  ┌────────┴─────────┐                                    │
│  │BoardToolController│                                    │
│  │ case LaserPointer │                                    │
│  │ → delegate to tool│                                    │
│  └──────────────────┘                                    │
└─────────────────────────────────────────────────────────┘
                         │
┌────────────────────────┼────────────────────────────────┐
│                  BFGA.Network                            │
│                        │                                  │
│  Protocol/             │                                  │
│  ┌─────────────────────┴──┐   ┌──────────────────┐      │
│  │ LaserPointerOperation  │   │ GameHost           │      │
│  │ Union key 13           │   │ IsOperationReliable│      │
│  │ Position, Color,       │──>│ → false (unreliable)│     │
│  │ IsActive               │   │ ApplyOperationCore │      │
│  └────────────────────────┘   │ → early return     │      │
│                               │ BroadcastOperation │      │
│                               └──────────────────┘      │
│                                                           │
│  ┌──────────────────┐                                    │
│  │ GameClient        │                                    │
│  │ → unreliable chan │                                    │
│  └──────────────────┘                                    │
└─────────────────────────────────────────────────────────┘
```

## Sources

- Codebase analysis of existing architecture (HIGH confidence — direct code reading)
- `CursorUpdateOperation` pattern as architectural precedent for ephemeral presence data
- `RemoteStrokePreviewState` pattern as precedent for streaming point data with overlay rendering
- `CollaboratorOverlayHelper` as renderer pattern for presence overlays
- `IBoardTool` interface exists but unused — laser is natural first adopter
- `IsOperationReliable()` in GameHost establishes unreliable channel pattern
- `GameClient.SendOperation()` has explicit unreliable routing for `CursorUpdateOperation`
