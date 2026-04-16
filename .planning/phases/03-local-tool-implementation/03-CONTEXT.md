# Phase 3: Local Tool Implementation - Context

**Gathered:** 2026-04-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement local laser pointer behavior on canvas. User selects Laser Pointer tool, press-and-hold shows own dot and fading trail, quick tap produces ping marker, release or cancellation stops emission while visuals fade out. Multiplayer relay, per-user colors, settings UI, and automatic tool return remain outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Visual Scale
- **D-01:** Laser dot, trail, and ping should keep constant on-screen size at every zoom level, not scale with board zoom.

### Ping Feedback
- **D-02:** Quick tap should render an expanding ring plus center dot.
- **D-03:** Ping should read as lightweight attention marker, not a large callout graphic.

### Cursor Affordance
- **D-04:** Laser tool should use crosshair cursor while selected.

### Exit / Cancel Behavior
- **D-05:** Laser emission cancels immediately on pointer leave, pointer capture loss, or tool switch.
- **D-06:** Cancellation should behave like release for visual cleanup: stop new points immediately and let final fade-out complete.

### OpenCode's Discretion
- Exact dot radius, trail thickness, and ping animation timing within phase requirements.
- Internal state ownership split between `BoardView`, `BoardToolController`, and render helpers.
- Whether local laser state reuses `RemoteLaserState` directly or uses a parallel local-only model with same rendering inputs.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and acceptance
- `.planning/ROADMAP.md` — Phase 3 goal, success criteria, and milestone boundaries.
- `.planning/REQUIREMENTS.md` — `RNDR-01`, `RNDR-04`, `INPT-01`, `INPT-02` define local rendering and input behavior.
- `.planning/PROJECT.md` — product constraints: laser remains ephemeral, architecture must stay within Core → Network → Canvas → App.

### Existing tool and pointer pipeline
- `src/BFGA.App/Views/BoardView.axaml.cs` — pointer capture flow, tool controller sync, screen-to-board conversion, eraser preview pattern.
- `src/BFGA.Canvas/Tools/BoardToolController.cs` — current tool state machine and tool result contract.
- `src/BFGA.App/ViewModels/BoardScreenViewModel.cs` — tool selection state and toolbar bindings.
- `src/BFGA.App/MainWindow.axaml.cs` — existing tool shortcut handling and phase boundary for `L` shortcut staying out of v1.

### Existing laser rendering infrastructure
- `src/BFGA.Canvas/BoardCanvas.cs` — overlay render order, fade timer, laser invalidation path.
- `src/BFGA.Canvas/BoardViewport.cs` — pan/zoom transforms used to keep laser positions correct.
- `src/BFGA.Canvas/Rendering/LaserTrailRenderer.cs` — current trail/dot rendering implementation and fade model.
- `src/BFGA.Canvas/Rendering/RemoteLaserState.cs` — current transient laser state shape available to reuse or mirror.
- `src/BFGA.Network/Protocol/BoardOperation.cs` — `LaserPointerOperation` payload contract already available to local flow.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `BoardView` pointer pipeline: already converts viewport coordinates to scene-space and owns pointer capture, making it natural place to detect press, move, tap, release, and cancel transitions.
- `LaserTrailRenderer`: already draws fading trail and head dot from transient point buffers.
- `RemoteLaserState` + `LaserTrailBuffer`: already model transient laser point history with timestamps and active flag.
- `BoardCanvas` fade timer: already invalidates only while laser visuals remain visible.

### Established Patterns
- Tool behavior currently routes through `BoardToolController` using `ToolResult`, but purely visual overlays like eraser preview are coordinated from `BoardView` rather than persisted in board state.
- Canvas renders overlays after board elements and collaborator cursors, with laser trails already topmost in `BoardCanvas`.
- Pointer interactions depend on scene-space coordinates from `BoardViewport.ScreenToBoard`, so local laser should store scene-space positions and apply viewport transform only at render time.

### Integration Points
- `BoardView.HandlePointerPressed/Moved/Released/Exited` for local laser gesture lifecycle and cancellation policy.
- `BoardCursorFactory.Create` for laser crosshair cursor.
- `BoardCanvas` / render models for exposing local laser state alongside existing remote laser overlay path.
- `MainViewModel.PublishLocalBoardOperation` path for eventually broadcasting local laser operations without touching board state.

</code_context>

<specifics>
## Specific Ideas

- Keep laser visuals visually stable on screen while zoom changes, even though positions remain scene-correct.
- Ping should be "expanding ring + center dot" rather than flash-only.
- Tool should feel intentionally transient: any leave/capture-loss/tool-switch event stops it immediately.

</specifics>

<deferred>
## Deferred Ideas

- `L` keyboard shortcut for laser tool — tracked as `INPT-03` in v2, not part of Phase 3.
- Auto-return to previous tool after release — tracked as `WKFL-01` in v2, not part of Phase 3.
- User-configurable laser color — Phase 5.

</deferred>

---

*Phase: 03-local-tool-implementation*
*Context gathered: 2026-04-15*
