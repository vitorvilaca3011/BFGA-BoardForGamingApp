# Phase 5: Polish & Configuration - Context

**Gathered:** 2026-04-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Polish laser pointer behavior after multiplayer integration by adding user-configurable laser color and closing lifecycle edge cases: disconnect cleanup, stale remote timeout, and switch-away deactivation. New tool shortcuts, auto-return workflow, and laser-specific contrast systems remain outside this phase.

</domain>

<decisions>
## Implementation Decisions

### Laser Color Ownership
- **D-01:** Laser color should be a shared presence color, not a separate laser-only identity.
- **D-02:** Changing the user's laser color should update the same multiplayer color identity already used for roster, cursor, and remote laser presence.
- **D-03:** Laser color must not be tied to `SelectedStrokeColor`; drawing color and persistent laser preference are separate concerns.

### Color Picker Placement
- **D-04:** v1 color configuration should live in the existing settings panel only, not in the toolbar.
- **D-05:** The settings panel should be the persistent source of truth for laser color so the preference survives restart via `settings.json`.

### Stale Remote Timeout
- **D-06:** If a remote laser receives no updates for about 3 seconds, force it inactive.
- **D-07:** Timeout cleanup should preserve the existing visual release semantics: stop new points immediately, then let the current trail fade naturally.

### Lifecycle Cleanup
- **D-08:** Peer disconnect should remove that peer's remote laser immediately with no ghost trail left behind.
- **D-09:** Switching away from Laser Pointer must send the same deactivation signal already used for cancel/release and clean up local active state.
- **D-10:** Tool-switch cleanup should keep the Phase 3 rule: cancellation behaves like release for visual cleanup, so any remaining local trail finishes its final fade-out.

### OpenCode's Discretion
- Exact picker control shape inside the settings panel (swatches, picker, or hybrid) as long as it is persistent and clearly scoped to laser color.
- Where stale-timeout enforcement runs (`MainViewModel`, overlay invalidation loop, or similar) as long as timeout semantics stay locked.
- Exact test split across App and Canvas projects.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and acceptance
- `.planning/ROADMAP.md` — Phase 5 goal and success criteria for color picker, disconnect cleanup, stale timeout, and tool-switch deactivation.
- `.planning/REQUIREMENTS.md` — `CONF-01` defines configurable laser color; earlier v1 laser requirements still constrain render and multiplayer behavior carried into polish.
- `.planning/PROJECT.md` — product constraints: laser stays ephemeral, real-time, and inside existing 4-layer architecture.
- `.planning/STATE.md` — accumulated decisions from Phases 1-4 that Phase 5 must preserve.

### Prior phase decisions to carry forward
- `.planning/phases/03-local-tool-implementation/03-CONTEXT.md` — cancellation semantics, constant on-screen laser sizing, and local tool-switch behavior baseline.
- `.planning/phases/04-multiplayer-integration/04-CONTEXT.md` — shared multiplayer color identity, remote fade-out semantics, and overlay isolation rules.

### Existing settings and laser integration points
- `src/BFGA.App/Services/SettingsService.cs` — persisted settings model and debounced save path for adding laser color preference.
- `src/BFGA.App/Views/SettingsPanel.axaml` — current settings UI and natural insertion point for Phase 5 color configuration.
- `src/BFGA.App/ViewModels/MainViewModel.cs` — remote laser lifecycle, roster/disconnect cleanup, and current color reconciliation.
- `src/BFGA.App/ViewModels/BoardScreenViewModel.cs` — tool selection and current property-panel visibility rules.
- `src/BFGA.App/Views/BoardView.axaml.cs` — local laser begin/update/release/cancel flow and current dependency on `SelectedStrokeColor`.
- `src/BFGA.Canvas/Rendering/RemoteLaserState.cs` — remote laser transient state, including `LastUpdateMs` available for stale timeout.
- `src/BFGA.Canvas/Rendering/LocalLaserState.cs` — local laser transient state and reset semantics.
- `src/BFGA.Canvas/LaserOverlayCanvas.cs` — isolated overlay invalidation loop already suitable for laser-only timing work.
- `src/BFGA.Canvas/Rendering/LaserTrailRenderer.cs` — existing fade logic that timeout and cancel behavior must reuse rather than replace.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SettingsService`: already persists JSON settings with debounced writes; Phase 5 can add laser color without inventing a second persistence path.
- `SettingsPanel.axaml`: already hosts real settings controls plus placeholders, making it the smallest consistent place for a laser color picker.
- `MainViewModel.RemoveRosterEntry(...)`: already removes remote lasers on peer leave, so disconnect cleanup mainly needs verification and edge-case hardening.
- `RemoteLaserState.LastUpdateMs`: already tracked on inbound updates, enabling stale timeout without protocol changes.
- `LaserOverlayCanvas` fade timer and `LaserTrailRenderer`: already own laser-only animation/redraw behavior, preserving overlay isolation from board rendering.

### Established Patterns
- Multiplayer presence state lives outside board state and is rebuilt from immutable dictionaries in `MainViewModel`.
- Shared user color identity currently comes from `PlayerInfo.AssignedColor`; remote lasers are recolored from roster state on update/rebuild.
- Local laser lifecycle is coordinated in `BoardView`, not in `BoardToolController`, because it is transient overlay behavior rather than persisted board editing.
- Visual cleanup semantics already distinguish between "stop emission now" and "let rendered trail fade out naturally".

### Integration Points
- `BoardView.HandleBoardScreenPropertyChanged(...)` and laser cancel helpers for switch-away deactivation.
- `BeginLocalLaser(...)` in `BoardView` for swapping from stroke color to persisted laser preference.
- `MainViewModel.UpsertRemoteLaser(...)`, `RemoveRosterEntry(...)`, and `ReconcileRemoteState()` for stale/disconnect cleanup.
- `SettingsService` + settings bindings for persisting and exposing chosen laser color to UI and runtime.

</code_context>

<specifics>
## Specific Ideas

- Keep laser color as part of the player's shared presence identity so collaborators see one consistent color language across roster, cursor, and laser.
- Keep color configuration low-friction in v1: settings panel only, no toolbar popup work.
- Timeout should feel like a synthetic release, not a hard delete.

</specifics>

<deferred>
## Deferred Ideas

- Toolbar quick color picker for laser — possible later UX enhancement, not needed in Phase 5.
- Laser-only color identity separate from roster/cursor colors — deferred unless shared presence color proves problematic.

</deferred>

---

*Phase: 05-polish-configuration*
*Context gathered: 2026-04-16*
