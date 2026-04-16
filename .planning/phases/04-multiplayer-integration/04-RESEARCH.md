# Phase 04: Multiplayer Integration — Research

**Researched:** 2026-04-16
**Phase:** 04-multiplayer-integration
**Requirements:** MULT-01, MULT-02, MULT-03
**Research level:** Level 1. Existing stack. No new deps. Focus: overlay architecture, roster/color sync, verification.

## Question

What needed for Phase 04 plan quality?

## Locked User Decisions

- **D-01 / D-02:** Remote laser color must reuse `PlayerInfo.AssignedColor` so laser identity matches roster, cursor, stroke preview.
- **D-03 / D-04:** Remote laser rendering must leave `BoardDrawOperation` on board-only path and move laser invalidation to dedicated overlay path.
- **D-05 / D-06:** Release/deactivate stops new points immediately and preserves final fade-out using same fade semantics as local laser.
- **D-07 / D-08:** Use existing host palette assignment. No laser-specific contrast or negotiation layer.

## Current Code State

### What already works

- `LaserPointerOperation` already relays on LiteNetLib sequenced channel 2 without board-state mutation.
- `BoardView` already owns local laser lifecycle and local overlay state.
- `MainViewModel.ApplyInboundOperation()` already routes remote `LaserPointerOperation` into `UpsertRemoteLaser(...)`.
- `LaserTrailRenderer` already renders remote/local trails and local ping using same fade math.

### Gaps found

1. **Full redraw coupling**
   - `BoardCanvas.RemoteLasersProperty`, `LocalLaserProperty`, and `LocalPingProperty` all route into `OnLaserStateChanged()`.
   - `OnLaserStateChanged()` calls `InvalidateVisual()` on `BoardCanvas`.
   - Result: any laser update forces full `BoardDrawOperation.Render()` and re-renders board elements. Violates D-03/D-04.

2. **Remote color drift / roster reconciliation gap**
   - `MainViewModel.UpsertRemoteLaser(...)` sets color only when new state created.
   - `ReconcileRemoteState()` rebuilds cursors and stroke previews, but not lasers.
   - Result: placeholder white laser can persist after full sync; roster color changes do not refresh active laser state. Violates D-01/D-02.

3. **Local gesture does not publish laser ops**
   - `BoardView.BeginLocalLaser/UpdateLocalLaser/CompleteLocalLaser/CancelLocalLaser` only mutate local overlay state.
   - No `PublishLocalBoardOperation(new LaserPointerOperation(...))` call.
   - Result: remote peers never see local laser. Violates MULT-01.

4. **Disconnect/full-sync cleanup incomplete for lasers**
   - `RemoveRosterEntry(...)` removes lasers correctly.
   - `ReconcileRemoteState()` does not filter `RemoteLasers` after roster/full-sync changes.
   - Result: stale remote laser state can survive full sync. Risks ghost trails.

## Architecture Choice

## Recommended overlay shape

**Use dedicated sibling overlay control inside `BoardViewport`.**

### Why

- Avalonia `ICustomDrawOperation` runs per control render pass. If laser state lives on `BoardCanvas`, `InvalidateVisual()` re-runs full board rendering.
- Avalonia supports layered child controls with explicit `ZIndex`; sibling overlay can sit above `BoardCanvas` and redraw independently.
- This matches D-03/D-04 exactly: board elements stay in `BoardCanvas` / `BoardDrawOperation`; laser visuals move to dedicated overlay control with own invalidation/timer.

### Evidence

- Avalonia docs: `ICustomDrawOperation` is control-scoped render logic.
- Avalonia docs: overlapping children render by `ZIndex`; later/higher child sits on top.
- LiteNetLib docs: `DeliveryMethod.Sequenced` keeps latest ordered stream on dedicated channel, good fit for transient pointer updates.

## Recommended file shape

- **New:** `src/BFGA.Canvas/LaserOverlayCanvas.cs`
  - dedicated control for remote lasers, local laser, local ping
  - owns fade timer and custom draw op
  - transparent background, `IsHitTestVisible = false`
- **Modify:** `src/BFGA.Canvas/BoardViewport.cs`
  - host `BoardCanvas` + `LaserOverlayCanvas` in layered container
  - forward `RemoteLasers`, `LocalLaser`, `LocalPing`, `Zoom`, `Pan` to overlay
- **Modify:** `src/BFGA.Canvas/BoardCanvas.cs`
  - remove laser properties, laser timer, and laser draw calls
  - keep board-only rendering path

## Data Flow Recommendation

### Local send path

`BoardView` should publish `LaserPointerOperation` on:

- `BeginLocalLaser(...)` → active op with current board point
- `UpdateLocalLaser(...)` → active op only when point changed
- `CompleteLocalLaser(...)` → inactive op at final point
- `CancelLocalLaser()` → inactive op at last known head point if local laser exists

Use `MainViewModel.PublishLocalBoardOperation(...)` so host/client modes keep using existing protocol path.

### Remote receive path

`MainViewModel.UpsertRemoteLaser(...)` should:

- ignore local client placeholder states
- always resolve color from current roster `PlayerInfo.AssignedColor` per D-01/D-02
- set `IsActive` and `LastUpdateMs`
- append point only when `operation.IsActive == true`
- preserve trail on inactive message so fade owns cleanup per D-05/D-06

### Full-sync / roster reconciliation path

`MainViewModel.ReconcileRemoteState()` should also rebuild lasers:

- drop sender IDs not in roster
- drop local client ID
- preserve `Trail`, `IsActive`, `LastUpdateMs`
- refresh `Color` from reconciled roster

## Multi-Peer Behavior

Existing dictionary shape already supports simultaneous use:

- key = `SenderId`
- value = per-peer `RemoteLaserState`

Need tests proving:

- 4 peers can coexist in dictionary
- each keeps independent trail buffer
- each uses roster-assigned color
- inactive update for one peer does not affect others

## Pitfalls To Avoid

1. **Do not add color to `LaserPointerOperation` payload**
   - color already authoritative in roster / `PlayerInfo.AssignedColor`
   - payload color would violate D-01/D-02 and enable spoofed visual identity

2. **Do not keep remote laser rendering in `BoardCanvas`**
   - any property change there triggers full board invalidation

3. **Do not clear trail on inactive update**
   - release/cancel must fade naturally per D-05/D-06

4. **Do not create new palette logic**
   - phase explicitly relies on host-assigned existing palette per D-07/D-08

5. **Do not route laser through board mutation helpers**
   - must stay transient; no `_boardState` changes, no clone path

## Test Strategy

### App tests

- `tests/BFGA.App.Tests/BoardViewPipelineTests.cs`
  - verify local gesture publishes active/inactive `LaserPointerOperation`
- `tests/BFGA.App.Tests/MainViewModelTests.cs`
  - verify remote upsert uses roster color
  - verify inactive op preserves trail but stops emission
  - verify full sync reconciles laser color and removes stale peers
  - verify peer leave removes remote laser
  - verify multiple remote peers retain independent laser state

### Canvas tests

- `tests/BFGA.Canvas.Tests/LaserOverlayCanvasTests.cs`
  - verify overlay fade timer starts/stops from overlay visibility only
- `tests/BFGA.Canvas.Tests/BoardViewportOverlayTests.cs`
  - verify viewport hosts dedicated overlay above board canvas
  - verify laser updates target overlay, not `BoardCanvas` render generation

## Validation Architecture

### Quick command

`dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~BoardViewPipelineTests|FullyQualifiedName~MainViewModelTests" --no-restore -v q`

### Full command

`dotnet test --no-restore -v q`

### Sampling contract

- after local-send task: run BoardView pipeline tests
- after remote-state task: run MainViewModel tests
- after overlay tasks: run Canvas tests, then full suite

## Recommended Plan Split

Three parallel Wave 1 plans.

1. **04-01** — publish local laser ops from `BoardView` gesture path
2. **04-02** — reconcile remote laser lifecycle/color in `MainViewModel`
3. **04-03** — move laser rendering to dedicated overlay control and isolate invalidation

This split keeps file ownership separate:

- `BoardView.axaml.cs` only in 04-01
- `MainViewModel.cs` only in 04-02
- `BoardCanvas.cs` / `BoardViewport.cs` / new overlay control only in 04-03

## Research Conclusion

Phase feasible without new deps.

Best path:

- keep LiteNetLib sequenced channel 2
- publish local laser ops from `BoardView`
- reconcile colors and stale peers in `MainViewModel`
- move laser rendering to sibling overlay control in `BoardViewport`

This covers MULT-01/02/03 and all locked decisions at full fidelity.
