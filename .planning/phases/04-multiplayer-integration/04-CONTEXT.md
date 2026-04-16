# Phase 4: Multiplayer Integration - Context

**Gathered:** 2026-04-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Integrate laser pointer into multiplayer presence flow so connected peers see each other's live laser dot and trail with distinct colors, including simultaneous use by multiple peers. This phase covers remote relay, remote render path, per-peer color application, and render isolation for laser overlays. User-configurable laser colors, stale timeout policy, and disconnect polish beyond immediate cleanup remain outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Laser Color Source
- **D-01:** Remote laser color should reuse existing host-assigned `PlayerInfo.AssignedColor`; no separate laser-only color system in Phase 4.
- **D-02:** Color identity should stay consistent across cursor, stroke preview, roster, and laser presence for same peer.

### Overlay Isolation
- **D-03:** Remote laser rendering must move to dedicated overlay path so laser updates do not trigger full board redraw.
- **D-04:** Board element rendering should remain on existing `BoardDrawOperation` path; laser overlay should invalidate independently on laser activity only.

### Remote Release Behavior
- **D-05:** When remote peer releases or deactivates laser, peers should stop adding new points immediately but allow existing trail to fade out naturally.
- **D-06:** Remote fade-out behavior should match local fade-out behavior from Phase 3 for visual consistency.

### Multi-Peer Color Policy
- **D-07:** Existing host palette is acceptable for Phase 4 four-peer sessions; no stricter laser-specific contrast algorithm in this phase.
- **D-08:** Phase 4 should assume palette uniqueness comes from current host assignment rules rather than extra negotiation or collision recovery.

### OpenCode's Discretion
- Exact overlay implementation shape, such as separate sibling control vs additional draw operation, as long as remote laser invalidation stays isolated from full board rendering.
- Exact property plumbing between `MainViewModel`, `BoardView`, `BoardViewport`, and canvas/overlay controls.
- Test split between App, Canvas, and Network test projects.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` — Phase 4 goal and success criteria, especially real-time remote visibility, distinct colors, simultaneous peers, and isolated overlay rendering.
- `.planning/REQUIREMENTS.md` — `MULT-01`, `MULT-02`, `MULT-03` define multiplayer visibility, distinct per-user color, and simultaneous independent trails.
- `.planning/PROJECT.md` — milestone constraints: transient-only laser state, existing 4-layer architecture, and performance sensitivity.
- `.planning/STATE.md` — carry-forward decisions from Phases 1-3, plus open concern about overlay isolation strategy.

### Prior phase decisions
- `.planning/phases/03-local-tool-implementation/03-CONTEXT.md` — local/remote split, constant on-screen size, cancellation semantics, and top-level integration points.
- `.planning/phases/02-trail-buffer-renderer/CONTEXT.md` — ring buffer, timer ownership in `BoardCanvas`, fade timing, and topmost laser layering decisions.

### Existing multiplayer and laser code
- `src/BFGA.App/ViewModels/MainViewModel.cs` — inbound op routing, roster reconciliation, `RemoteLasers` state, and current remote laser upsert logic.
- `src/BFGA.App/Views/BoardView.axaml` — current binding path from view to viewport for board and overlay state.
- `src/BFGA.App/Views/BoardView.axaml.cs` — pointer/local laser lifecycle and current board view integration boundary.
- `src/BFGA.Canvas/BoardViewport.cs` — styled-property plumbing from view to render controls.
- `src/BFGA.Canvas/BoardCanvas.cs` — current full board render path, overlay invalidation behavior, and laser fade timer hook.
- `src/BFGA.Canvas/Rendering/LaserTrailRenderer.cs` — remote/local trail drawing, fade logic, and visibility helpers.
- `src/BFGA.Canvas/Rendering/RemoteLaserState.cs` — transient per-peer laser model shape.
- `src/BFGA.Network/GameHost.cs` — host relay bypass for `LaserPointerOperation`, assigned player palette, and peer join/leave handling.
- `src/BFGA.Network/PlayerInfo.cs` — canonical assigned-color contract shared across multiplayer presence.
- `src/BFGA.Network/Protocol/BoardOperation.cs` — `LaserPointerOperation` payload contract already used for laser relay.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainViewModel.UpsertRemoteLaser(...)` already creates and updates `RemoteLaserState` per sender, using roster-derived color fallback.
- `GameHost.PlayerColors` plus `AssignClientId(...)` already provide per-peer color assignment without new protocol work.
- `LaserTrailRenderer` already supports remote trail drawing and fade visibility checks.
- `BoardView` + `BoardViewport` already pass remote laser dictionaries through Avalonia styled properties.

### Established Patterns
- Presence features live outside board state and are rebuilt from immutable dictionaries on `MainViewModel`.
- Host remains authoritative for player identity/color assignment, while transient presence uses lightweight non-board operations.
- Board rendering currently snapshots styled properties in `BoardDrawOperation`; full invalidation happens whenever `BoardCanvas` overlay properties change.
- Local laser state stays separate from remote laser state; Phase 4 should preserve that split instead of merging both paths.

### Integration Points
- `MainViewModel.ApplyInboundOperation(...)`, `UpsertRemoteLaser(...)`, `RemoveRosterEntry(...)`, and `ReconcileRemoteState()` for remote laser lifecycle.
- `GameHost.BroadcastOperation(...)` and peer join/leave flow for color ownership and disconnect propagation.
- `BoardView.axaml` / `BoardViewport` / `BoardCanvas` for replacing current full-canvas laser invalidation with isolated overlay invalidation.
- Rendering tests around collaborator overlays and board view pipeline as likely verification points for isolated redraw behavior.

</code_context>

<specifics>
## Specific Ideas

- Remote lasers should visually match local laser semantics: active head while pressed, final trail fade after release.
- Laser presence should use same player color identity already visible in roster/cursor systems rather than inventing second multiplayer color language.
- Isolation goal is strict: remote laser churn must not force full board element redraw during multiplayer pointing.

</specifics>

<deferred>
## Deferred Ideas

- User-configurable laser color picker — Phase 5.
- Laser-specific palette/contrast tuning beyond current host assignment — reconsider only if Phase 4 testing shows readability issues.
- Stale remote laser timeout policy — Phase 5.

</deferred>

---

*Phase: 04-multiplayer-integration*
*Context gathered: 2026-04-16*
