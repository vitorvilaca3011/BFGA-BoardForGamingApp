---
phase: 03-local-tool-implementation
plan: 03
subsystem: ui
tags: [laser-pointer, avalonia, input, cursor, tdd]

requires:
  - phase: 03-02
    provides: Local overlay property plumbing from BoardView into canvas rendering
provides:
  - Local laser press, drag, release, and cancel lifecycle in BoardView without board mutations
  - Quick-tap ping detection and fade-preserving cancel behavior for the local overlay
  - Crosshair cursor mapping and regression tests for laser gesture paths
affects: [phase-04-multiplayer-integration, local-overlay, board-input]

tech-stack:
  added: []
  patterns: [BoardView short-circuits ephemeral tools before BoardToolController, headless-safe source assertion for cursor mapping tests]

key-files:
  created: []
  modified:
    - src/BFGA.App/Views/BoardView.axaml.cs
    - src/BFGA.App/Infrastructure/BoardCursorFactory.cs
    - tests/BFGA.App.Tests/BoardViewPipelineTests.cs

key-decisions:
  - "Keep Laser Pointer gesture handling inside BoardView so ephemeral overlay state never flows through board mutation operations."
  - "Use pointer capture loss, pointer exit, and tool-switch hooks to stop emission while preserving the existing trail buffer for fade-out."
  - "Assert cursor mapping from source text in tests because headless test runs do not provide Avalonia ICursorFactory."

patterns-established:
  - "Ephemeral tool input paths short-circuit before TryHandlePointer when board state must remain unchanged."
  - "Laser completion and cancellation deactivate emission without clearing the trail buffer so render fade owns cleanup."

requirements-completed: [RNDR-01, RNDR-04, INPT-01, INPT-02]

duration: 4 min
completed: 2026-04-16
---

# Phase 03 Plan 03: Local Tool Implementation Summary

**BoardView now drives local laser press-drag-release gestures with quick-tap ping detection, fade-safe cancellation, and crosshair cursor mapping**

## Performance

- **Duration:** 4 min
- **Started:** 2026-04-16T03:20:13Z
- **Completed:** 2026-04-16T03:24:02Z
- **Tasks:** 1
- **Files modified:** 3

## Accomplishments
- Added `BeginLocalLaser`, `UpdateLocalLaser`, `CompleteLocalLaser`, and `CancelLocalLaser` helpers in `BoardView`
- Short-circuited Laser Pointer press, move, and release before `TryHandlePointer` so board elements stay unchanged
- Added quick-tap ping, pointer-exit/capture-loss/tool-switch cancellation, crosshair cursor mapping, and regression coverage

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: add BoardView laser gesture regression tests** - `84e24e9` (test)
2. **Task 1 GREEN: implement local laser pointer gesture** - `dcf5b73` (feat)

## Files Created/Modified
- `src/BFGA.App/Views/BoardView.axaml.cs` - Local laser lifecycle, tap threshold, and cancel hooks on board input pipeline
- `src/BFGA.App/Infrastructure/BoardCursorFactory.cs` - Crosshair cursor mapping for `BoardToolType.LaserPointer`
- `tests/BFGA.App.Tests/BoardViewPipelineTests.cs` - TDD coverage for begin, move, tap, cancel, tool switch, and cursor mapping

## Decisions Made
- Kept local laser gesture state in `BoardView` because Phase 3 scope requires ephemeral visuals without board operations
- Reused existing local trail buffer and only appended changed points to satisfy threat-model DoS mitigation on high-frequency pointer move
- Used source-level cursor assertion in tests because constructing `Cursor(StandardCursorType.Cross)` fails in headless Avalonia test runs

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Headless test environment cannot construct Avalonia standard cursors without `ICursorFactory`; cursor coverage was verified by source assertion while runtime mapping stayed unchanged in production code

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Local laser lifecycle is complete and isolated from board state mutations
- Ready for Phase 4 work to publish and render peer laser updates on top of the same transient overlay model

## Self-Check: PASSED
