---
phase: 05-polish-configuration
plan: 01
subsystem: ui
tags: [avalonia, settings, skia, persistence, xunit]
requires:
  - phase: 04-multiplayer-integration
    provides: remote laser identity, roster color reconciliation, overlay isolation
provides:
  - persisted preferred presence color in settings.json
  - runtime LaserPresenceColor property for later plans
  - settings-panel-only laser color swatch editor with preview
affects: [phase-05-02, phase-05-03, settings, multiplayer-presence]
tech-stack:
  added: []
  patterns: [persisted presence color separate from stroke color, settings-panel-only color editing, viewmodel proxy for settings bindings]
key-files:
  created: []
  modified:
    - src/BFGA.App/Services/SettingsService.cs
    - src/BFGA.App/ViewModels/MainViewModel.cs
    - src/BFGA.App/ViewModels/BoardScreenViewModel.cs
    - src/BFGA.App/Views/SettingsPanel.axaml
    - src/BFGA.App/Views/SettingsPanel.axaml.cs
    - tests/BFGA.App.Tests/MainViewModelTests.cs
    - tests/BFGA.App.Tests/BoardScreenViewModelTests.cs
    - tests/BFGA.App.Tests/PropertyPanelTests.cs
    - src/BFGA.App/Converters/SelectedLaserSwatchBorderConverter.cs
    - src/BFGA.App/Converters/SelectedLaserSwatchThicknessConverter.cs
key-decisions:
  - "Persist preferred presence color as LaserPresenceColorHex in settings.json with #FFFFFF fallback for missing or invalid values."
  - "Expose LaserPresenceColor on MainViewModel and BoardScreenViewModel without coupling it to SelectedStrokeColor."
  - "Keep color editing in SettingsPanel only, using the approved 16-swatch grid and current-color preview."
patterns-established:
  - "Presence color settings flow: SettingsService string -> MainViewModel SKColor parse/fallback -> BoardScreenViewModel proxy -> SettingsPanel swatches."
  - "Selected swatch visuals use converter-driven 2px AccentWhite ring while preserving existing color-swatch sizing."
requirements-completed: [CONF-01]
duration: 5min
completed: 2026-04-16
---

# Phase 05 Plan 01: Persist preferred presence color and settings swatch UI Summary

**Persistent shared presence color contract with settings-panel-only 16-swatch laser editor and restart-safe settings storage**

## Performance

- **Duration:** 5 min
- **Started:** 2026-04-16T22:36:29Z
- **Completed:** 2026-04-16T22:41:08Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments
- Added persisted `LaserPresenceColorHex` setting and runtime `LaserPresenceColor` surface.
- Kept presence color independent from `SelectedStrokeColor` and covered that contract with tests.
- Added `LASER COLOR` settings section with preview chip, approved helper copy, 16 swatches, and click handling.

## task Commits

Each task was committed atomically:

1. **task 1: add persisted preferred presence-color contract and viewmodel surface** - `bc1a931` (feat)
2. **task 2: add settings-panel-only LASER COLOR swatch UI** - `55924af` (feat)

**Plan metadata:** pending

## Files Created/Modified
- `src/BFGA.App/Services/SettingsService.cs` - persisted `LaserPresenceColorHex` load/save contract.
- `src/BFGA.App/ViewModels/MainViewModel.cs` - runtime `LaserPresenceColor` property with parse fallback and debounced persistence.
- `src/BFGA.App/ViewModels/BoardScreenViewModel.cs` - proxy property and property-changed relay for settings binding.
- `src/BFGA.App/Views/SettingsPanel.axaml` - `LASER COLOR` section, preview chip, approved swatch grid.
- `src/BFGA.App/Views/SettingsPanel.axaml.cs` - swatch click handler parsing button tags into `SKColor`.
- `src/BFGA.App/Converters/SelectedLaserSwatchBorderConverter.cs` - selected-state border brush for swatches.
- `src/BFGA.App/Converters/SelectedLaserSwatchThicknessConverter.cs` - selected-state 2px ring logic for current swatch.
- `tests/BFGA.App.Tests/MainViewModelTests.cs` - persistence/load regression tests for preferred presence color.
- `tests/BFGA.App.Tests/BoardScreenViewModelTests.cs` - proxy and stroke-color isolation tests.
- `tests/BFGA.App.Tests/PropertyPanelTests.cs` - settings panel markup and swatch click tests.

## Decisions Made
- Used one persisted presence color setting instead of reusing drawing color, preserving locked Phase 3-4 separation.
- Added parse fallback in `MainViewModel` for invalid/missing color text to satisfy threat mitigation T-05-01.
- Implemented selected-swatch ring with converter-driven `BorderThickness` so UI stays 24x24 with no duplicate editor.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Known Stubs

- `src/BFGA.App/Views/SettingsPanel.axaml:179` - `Language` placeholder section remains disabled; pre-existing, out of scope for this plan.
- `src/BFGA.App/Views/SettingsPanel.axaml:193` - `Default Image Folder` placeholder section remains disabled; pre-existing, out of scope for this plan.
- `src/BFGA.App/Views/SettingsPanel.axaml:206` - `Autosave` placeholder section remains disabled; pre-existing, out of scope for this plan.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 05-02 can now propagate one persisted shared presence color through host-authoritative roster/cursor/laser metadata.
- Phase 05-03 can switch local laser rendering from stroke color to `LaserPresenceColor` without new persistence work.

## Self-Check: PASSED

- FOUND: `.planning/phases/05-polish-configuration/05-01-SUMMARY.md`
- FOUND: `bc1a931`
- FOUND: `55924af`

---
*Phase: 05-polish-configuration*
*Completed: 2026-04-16*
