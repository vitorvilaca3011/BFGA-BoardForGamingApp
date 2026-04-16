# Phase 05: Polish & Configuration — Research

**Researched:** 2026-04-16
**Phase:** 05-polish-configuration
**Requirements:** CONF-01
**Research level:** Level 1. Existing stack only. No new deps. Focus: persisted presence color, live multiplayer propagation, stale timeout semantics, settings-panel UI contract.

## Question

What needed for Phase 05 plan quality without violating locked decisions from discuss + UI phases?

## Locked User Decisions

- **D-01 / D-02:** Laser color is shared multiplayer presence color. Same identity must drive roster, cursor, remote laser.
- **D-03:** Presence color must stay separate from `SelectedStrokeColor`.
- **D-04 / D-05:** Color config lives in `SettingsPanel` only and persists in `%APPDATA%/BFGA/settings.json`.
- **D-06 / D-07:** Remote silence around 3s behaves like synthetic release: stop emission, then let current trail fade naturally.
- **D-08:** Disconnect cleanup removes remote laser immediately. No ghost trail.
- **D-09 / D-10:** Tool switch away from Laser Pointer must send same inactive/deactivation semantics as release and preserve local fade-out.
- **UI-SPEC:** `LASER COLOR` section, helper copy, 24x24 preview + swatches, 4x4 grid, no toolbar picker, reuse `color-swatch` pattern.

## Current Code State

### What already works

- `SettingsService` already loads/saves JSON with debounced writes. Good persistence anchor.
- `PropertyPanel.axaml` already defines the exact 16-swatch `color-swatch` grid Phase 05 can reuse.
- `MainViewModel.UpsertRemoteLaser(...)` already keeps roster-authoritative remote laser colors for peers after updates/full sync.
- `RemoveRosterEntry(...)` already removes remote laser state immediately on peer leave.
- `BoardView.CancelLocalLaser()` already sends inactive `LaserPointerOperation` and preserves local trail fade-out.
- `LaserOverlayCanvas` already owns laser-only fade timer and can host timeout enforcement without touching board rendering.

### Gaps found

1. **No persisted color preference**
   - `SettingsService` stores grid/language/autosave only.
   - No laser/presence color field in JSON model.

2. **No settings UI for color selection**
   - `SettingsPanel.axaml` has no `LASER COLOR` section.
   - `SettingsPanel.axaml.cs` has no swatch click handlers.

3. **Local laser still tied to drawing color**
   - `BoardView.BeginLocalLaser(...)` uses `_boardScreenViewModel?.SelectedStrokeColor`.
   - Violates D-03.

4. **No live multiplayer color update path**
   - Host assigns `PlayerInfo.AssignedColor` once in `AssignClientId(...)`.
   - No client→host operation exists to request color change after connect.
   - Without new protocol path, "settings change updates roster/cursor/laser immediately" cannot happen.

5. **Host identity propagation gap**
   - `FullSyncResponseOperation.PlayerRoster` contains connected peers from `GameHost._players`, not host-local identity.
   - Remote clients can receive host laser updates with `SenderId = Guid.Empty` but no roster color for that sender unless host explicitly announces it.

6. **No stale timeout enforcement**
   - `RemoteLaserState.LastUpdateMs` exists, but nothing turns stale active lasers inactive after ~3s silence.
   - Current overlay fade timer only redraws/fades existing trail points.

## Recommended Architecture

## 1. Persist a preferred presence color in settings

Add a dedicated persisted field in `SettingsService`.

**Recommended shape**
- `public string LaserPresenceColorHex { get; set; } = "#FFFFFF";`
- `MainViewModel` exposes parsed `SKColor LaserPresenceColor`
- `BoardScreenViewModel` exposes proxy property for settings UI binding

Why:
- Keeps config in existing `settings.json` path per D-05
- Stays separate from drawing color per D-03
- Gives one canonical local preference for host + client runtime

## 2. Use settings panel as only editor

Reuse exact `PropertyPanel.axaml` swatch palette and style.

**Recommended UI shape**
- `LASER COLOR` heading
- 24x24 current-color preview chip
- helper text: `Used for roster, cursor, and laser`
- 4x4 swatch grid using existing 16 colors:
  - `#000000`, `#FFFFFF`, `#FF3B30`, `#FF9500`
  - `#FFCC00`, `#34C759`, `#007AFF`, `#AF52DE`
  - `#5856D6`, `#FF2D55`, `#A2845E`, `#8E8E93`
  - `#636366`, `#48484A`, `#2C2C2E`, `#1C1C1E`
- selected swatch gets 2px `AccentWhite` ring

Do **not** add toolbar picker, floating popup, or second source of truth.

## 3. Add a host-authoritative color update path

Smallest change that satisfies D-01/D-02 without new deps:

### New client→host operation
- Add `UpdatePresenceColorOperation : BoardOperation`
- Payload: `SKColor Color`
- Reliable ordered, same as normal metadata updates

### Host behavior
- Validate sender exists
- Update `_players[senderId].Info.AssignedColor`
- Broadcast `PeerJoinedOperation(senderId, displayName, assignedColor)` as metadata upsert event

Why reuse `PeerJoinedOperation` for broadcast:
- `MainViewModel.ApplyInboundOperation(...)` already treats it as roster upsert
- Avoids second client-side "player profile updated" code path
- Keeps roster/cursor/laser reconciliation on existing metadata event flow

## 4. Announce host-local presence identity explicitly

Because host local user is not inside `_players`, remote clients need a synthetic host metadata announcement.

**Recommended app-level rule**
- When host starts or host changes preferred presence color, `MainViewModel` broadcasts:
  - `new PeerJoinedOperation(Guid.Empty, DisplayName, LaserPresenceColor)`
- When `PeerJoined` fires for a newly connected client, host rebroadcasts that same pseudo-join so the newcomer learns host identity

Why:
- No interface expansion needed on `IGameHostSession`
- Remote clients already accept `PeerJoinedOperation` as roster upsert
- Fixes host laser/cursor color drift for `SenderId = Guid.Empty`

## 5. Sync client preferred color after full sync / on setting change

Client does not need handshake changes.

**Recommended rule**
- After `FullSyncResponseOperation`, compare local roster-assigned color for `_localClientId` with `LaserPresenceColor`
- If different, send `UpdatePresenceColorOperation(LaserPresenceColor)`
- Also send same operation whenever user changes color while connected as client

This makes preferred color converge immediately after connect without modifying LiteNetLib connection payloads.

## 6. Move stale-timeout enforcement into `LaserOverlayCanvas`

UI-SPEC allows timeout enforcement in overlay/timer path.

**Recommended rule**
- On overlay fade-timer tick, scan `RemoteLasers`
- If `state.IsActive` and `now - state.LastUpdateMs >= 3000`:
  - set `state.IsActive = false`
  - refresh last trail point timestamp to `now` so stale timeout produces one normal fade window instead of disappearing instantly from already-old timestamps
- Do not remove dictionary entry there; let normal fade visibility drive redraw idle stop

Why overlay:
- no extra app timer
- same laser-only invalidation loop already exists
- preserves board redraw isolation from Phase 4

## 7. Keep disconnect cleanup in App layer

`RemoveRosterEntry(...)` already removes remote lasers immediately. Keep this behavior.

Need regression test only:
- peer leave clears `RemoteLasers[peerId]` same tick
- no overlay fade path should resurrect removed state

## Pitfalls To Avoid

1. **Do not bind laser color to `SelectedStrokeColor`**
2. **Do not add toolbar color picker**
3. **Do not create a second laser-only identity separate from roster/cursor colors**
4. **Do not put stale timeout on full board timer or board redraw path**
5. **Do not clear trail immediately on stale timeout**
6. **Do not trust client-provided broadcast metadata directly** — host must own assigned color for peer-visible state

## Test Strategy

### App tests
- `tests/BFGA.App.Tests/MainViewModelTests.cs`
  - settings load/save for preferred color
  - client full-sync sends color update when preferred differs
  - host broadcasts pseudo-join metadata for local host identity
  - inbound `PeerJoinedOperation` recolors roster/cursor/remote laser immediately
  - peer-left still removes remote laser immediately
- `tests/BFGA.App.Tests/BoardScreenViewModelTests.cs`
  - proxy property and notifications for settings-panel swatch selection
- `tests/BFGA.App.Tests/BoardViewPipelineTests.cs`
  - local laser starts with `LaserPresenceColor`, not `SelectedStrokeColor`
  - tool-switch cancel still publishes inactive op and preserves trail

### Network tests
- `tests/BFGA.Network.Tests/ProtocolTests.cs`
  - `UpdatePresenceColorOperation` MessagePack round-trip
- `tests/BFGA.Network.Tests/NetworkTests.cs`
  - host receives update op, mutates player roster color, broadcasts metadata upsert

### Canvas tests
- `tests/BFGA.Canvas.Tests/LaserOverlayCanvasTests.cs`
  - stale active remote laser flips inactive after ~3000ms
  - last point timestamp refresh creates normal fade window after timeout

## Validation Architecture

### Quick command

`dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~BoardScreenViewModelTests|FullyQualifiedName~BoardViewPipelineTests|FullyQualifiedName~MainViewModelTests" --no-restore -v q`

### Full command

`dotnet test --no-restore -v q`

### Sampling contract

- after settings contract task: run `BoardScreenViewModelTests` + `MainViewModelTests`
- after network propagation task: run `ProtocolTests` + `NetworkTests` + `MainViewModelTests`
- after local source / stale-timeout task: run `BoardViewPipelineTests` + `LaserOverlayCanvasTests`
- after final wave: full suite

## Recommended Plan Split

### Wave 1
1. **05-01** — settings contract, persistence, settings-panel UI for preferred presence color

### Wave 2
2. **05-02** — host-authoritative presence color propagation across multiplayer identity
3. **05-03** — local laser color source swap + stale timeout / release semantics hardening

Reason:
- `05-01` creates contracts consumed by both later plans
- `05-02` owns network/app metadata files
- `05-03` owns BoardView/overlay files
- no same-wave file conflicts after `05-01`

## Research Conclusion

Phase 05 feasible in 3 plans. No new libraries needed.

Best path:
- persist preferred presence color in `settings.json`
- edit it only from `SettingsPanel`
- propagate it live through host-authoritative metadata updates
- make local laser use preferred presence color instead of stroke color
- enforce stale remote timeout in overlay timer with synthetic-release semantics

This covers all locked decisions at full fidelity, including no toolbar picker, persistent settings-panel source of truth, shared presence identity, immediate disconnect cleanup, and ~3s stale timeout with natural fade.
