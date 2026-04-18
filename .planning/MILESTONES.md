# Milestones

## v1.0 Laser Pointer Tool (Shipped: 2026-04-18)

**Phases completed:** 8 phases, 21 plans, 31 tasks

**Key accomplishments:**

- LaserPointerOperation with Union key 13, host relay bypass on dedicated sequenced channel 2, board state isolation proven by tests
- LaserTrailBuffer ring buffer (128 cap, zero-alloc) and RemoteLaserState per-peer model with 6 passing unit tests
- LaserTrailRenderer with ease-out alpha decay, BoardCanvas fade timer at 16ms, and full MainViewModel→Canvas data pipeline for remote laser trails
- Local-only laser overlay primitives with zoom-stable dot/trail sizing, 2.4s ping fade math, and deterministic renderer tests
- Local laser and ping overlay state now flows from BoardView into BoardCanvas with shared fade-timer visibility checks and topmost local draw order
- BoardView now drives local laser press-drag-release gestures with quick-tap ping detection, fade-safe cancellation, and crosshair cursor mapping
- BoardView now streams laser lifecycle updates over LaserPointerOperation while preserving local ping UX and fade-safe release semantics
- Roster-authoritative remote laser color refresh with fade-safe inactive updates, stale peer cleanup, and multi-peer isolation in MainViewModel
- Dedicated laser overlay control now renders remote/local laser visuals above board content while `BoardCanvas` stays on a board-only redraw path
- Persistent shared presence color contract with settings-panel-only 16-swatch laser editor and restart-safe settings storage
- Host-authoritative preferred presence color sync across roster, cursor, host identity, and remote laser state
- Persisted shared presence color now drives local laser and ping visuals, while stale remote lasers self-release in overlay after 3 seconds and keep normal fade-out semantics
- Host UI now receives remote laser operations through host session adapter and reuses existing remote laser pipeline without double-applying board mutations.
- Phase 04 multiplayer now has auditable verification evidence for host visibility, per-peer color identity, simultaneous trail isolation, and overlay-only redraw behavior.
- Phase 02 trail-fade and redraw-isolation evidence now maps RNDR-02 and RNDR-05 to exact canvas tests, source methods, and rerunnable commands
- Phase 03 local rendering and input evidence now ties requirement closure to exact BoardView and renderer tests plus explicit manual-only visual notes
- Requirements ledger now closes six rendering/input requirements from refreshed Phase 02-03 verification evidence while keeping configuration closure isolated to Phase 08.
- Audit-ready Phase 05 verification artifact for configurable laser color, host-authoritative color identity, and cleanup semantics
- CONF-01 ledger closure and refreshed v1.0 milestone audit now reflect complete verification coverage instead of stale bookkeeping gaps.
- Deterministic CONF-01 host presence-color regression evidence using isolated settings-backed MainViewModel tests.
- Passed Phase 08 verification report for CONF-01 using current focused regression and full-suite green evidence.

---
