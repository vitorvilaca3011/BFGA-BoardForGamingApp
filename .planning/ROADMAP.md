# Roadmap: BFGA Laser Pointer Tool

## Overview

Deliver a fully ephemeral laser pointer tool with fading trail, press-and-hold activation, and real-time multiplayer visibility. Build order: network protocol first (architectural correctness), then shared rendering infrastructure, then local tool, then multiplayer integration, then polish. Each phase delivers a verifiable capability increment.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

- [x] **Phase 1: Network Protocol & Host Relay** - LaserPointerOperation type, routing bypass, sequenced delivery on channel 2
- [x] **Phase 2: Trail Buffer & Renderer** - Ring buffer, per-segment fade rendering, smooth animation loop
- [x] **Phase 3: Local Tool Implementation** - Press-and-hold activation, local dot + trail, ping marker, zoom/pan correctness
- [x] **Phase 4: Multiplayer Integration** - Remote laser rendering, per-user colors, simultaneous multi-peer lasers
- [x] **Phase 5: Polish & Configuration** - Color picker, disconnect cleanup, stale laser timeout, edge case handling
- [x] **Phase 6: Host Laser Inbound Rendering** - Host consumes remote laser ops, renders client lasers, verifies multiplayer path (completed 2026-04-17)
- [x] **Phase 7: Verify Rendering And Input Coverage** - Add missing verification for local rendering, trail fade, and input flows (completed 2026-04-17)
- [ ] **Phase 8: Verify Configuration And Traceability Closure** - Verify configuration behavior and close audit traceability drift

## Phase Details

### Phase 1: Network Protocol & Host Relay
**Goal**: Laser operations transmit between peers without touching board state or triggering board clones
**Depends on**: Nothing (first phase)
**Requirements**: RNDR-03
**Success Criteria** (what must be TRUE):
  1. `LaserPointerOperation` serializes/deserializes via MessagePack with Union key 13
  2. Host receives laser op and relays to other peers without calling `ApplyOperationCore` board state logic
  3. Laser ops use `DeliveryMethod.Sequenced` on channel 2 (dedicated, not shared with cursor)
  4. Board state (`_boardState`) is never modified by laser operations — no `LastModified` update, no `CloneBoardState`
**Plans**: 1 plan
Plans:
- [x] 01-01-PLAN.md — LaserPointerOperation type, host relay bypass, dedicated sequenced channel 2

### Phase 2: Trail Buffer & Renderer
**Goal**: Fading trail renders smoothly at 60fps with zero GC pressure
**Depends on**: Phase 1
**Requirements**: RNDR-02, RNDR-05
**Success Criteria** (what must be TRUE):
  1. Trail points stored in fixed-size ring buffer (128 capacity) — old points overwritten, no list growth
  2. Trail renders as per-segment lines with per-point alpha decay (ease-out curve, ~1-2s fade)
  3. Single `SKPaint` instance reused across all segments — no per-frame allocation
  4. Fade animation timer (16ms) runs only while laser is active, stops when no lasers present
**Plans:** 2 plans
Plans:
- [x] 02-01-PLAN.md — LaserTrailBuffer ring buffer + RemoteLaserState model (TDD)
- [x] 02-02-PLAN.md — LaserTrailRenderer + BoardCanvas integration (timer, property, draw pipeline)

### Phase 3: Local Tool Implementation
**Goal**: User can activate laser tool and see their own dot + trail with correct viewport behavior
**Depends on**: Phase 2
**Requirements**: RNDR-01, RNDR-04, INPT-01, INPT-02
**Success Criteria** (what must be TRUE):
  1. Selecting LaserPointer tool and pressing mouse shows colored dot at cursor position
  2. Dragging while pressed leaves a fading trail that decays over ~1-2 seconds
  3. Quick tap (<200ms, <5px movement) produces a pulsing ping marker that fades after ~2-3 seconds
  4. Dot and trail render at correct positions after pan/zoom — scene-space coords with viewport transform
  5. Releasing mouse stops laser emission and trail begins final fade-out
**Plans**: 3 plans
Plans:
- [x] 03-01-PLAN.md — Local laser overlay models and renderer helpers for constant-size dot, trail, and ping
- [x] 03-02-PLAN.md — BoardCanvas, BoardViewport, and BoardView local overlay property plumbing
- [x] 03-03-PLAN.md — BoardView laser gesture lifecycle, quick tap ping, cancellation, and crosshair cursor
**UI hint**: yes

### Phase 4: Multiplayer Integration
**Goal**: All connected peers see each other's laser pointers in real-time with distinct colors
**Depends on**: Phase 3
**Requirements**: MULT-01, MULT-02, MULT-03
**Success Criteria** (what must be TRUE):
  1. Local user's laser dot and trail appear on all connected peers' canvases in real-time
  2. Each peer's laser renders with their distinct user color — no color collisions in 4-player session
  3. Four peers can use laser pointers simultaneously with independent trails rendering correctly
  4. Remote laser rendering does NOT trigger full `BoardDrawOperation.Render()` — isolated overlay only
**Plans**: 3 plans
Plans:
- [x] 04-01-PLAN.md — Publish local laser lifecycle from BoardView into LaserPointerOperation network flow
- [x] 04-02-PLAN.md — Reconcile remote laser color, fade-out semantics, and stale peer cleanup in MainViewModel
- [x] 04-03-PLAN.md — Move laser rendering to dedicated overlay control and isolate redraws from BoardCanvas

### Phase 5: Polish & Configuration
**Goal**: User can customize laser color and edge cases (disconnect, tool switch, stale state) are handled
**Depends on**: Phase 4
**Requirements**: CONF-01
**Success Criteria** (what must be TRUE):
  1. User can pick their laser pointer color via color picker UI in settings or toolbar
  2. Peer disconnect cleans up their laser trail immediately — no ghost trails persist
  3. Stale remote lasers time out after ~3 seconds if no updates received
  4. Switching away from laser tool sends deactivation signal and cleans up local state
**Plans**: 3 plans
Plans:
- [x] 05-01-PLAN.md — Persist preferred presence color and add settings-panel-only LASER COLOR swatch UI
- [x] 05-02-PLAN.md — Propagate preferred presence color through host-authoritative roster/cursor/laser metadata
- [x] 05-03-PLAN.md — Use preferred presence color for local laser and enforce stale remote timeout release semantics
**UI hint**: yes

### Phase 6: Host Laser Inbound Rendering
**Goal**: Host UI consumes inbound remote laser operations so multiplayer visibility requirements hold for every peer
**Depends on**: Phase 4
**Requirements**: MULT-01, MULT-02, MULT-03
**Gap Closure**: Closes milestone audit integration blocker and broken flow for `Client laser visible on host`
**Success Criteria** (what must be TRUE):
  1. Host session exposes inbound laser operations from remote clients to the app layer
  2. Host `MainViewModel` applies inbound remote laser updates through the existing remote laser path
  3. Host screen renders client laser dots and trails with correct per-user colors
  4. Regression coverage exists for `client sends laser -> host UI renders remote laser`
  5. Phase 04 multiplayer behavior is backed by a `VERIFICATION.md` artifact
**Plans**: 2 plans

Plans:
- [x] 06-01-PLAN.md — expose host inbound laser operation contract and wire MainViewModel host remote-laser handling
- [x] 06-02-PLAN.md — create Phase 04 multiplayer verification artifact with host-visibility closure evidence

### Phase 7: Verify Rendering And Input Coverage
**Goal**: Rendering and input behavior claimed by phases 02-03 is verified and traceable at milestone level
**Depends on**: Phase 3
**Requirements**: RNDR-01, RNDR-02, RNDR-04, RNDR-05, INPT-01, INPT-02
**Gap Closure**: Closes orphaned requirements caused by missing verification artifacts for phases 02-03
**Success Criteria** (what must be TRUE):
  1. Phase 02 has a `VERIFICATION.md` covering trail fade and performance behavior
  2. Phase 03 has a `VERIFICATION.md` covering local laser rendering, gesture lifecycle, and ping behavior
  3. Requirement evidence is strong enough for milestone audit to mark rendering and input requirements satisfied
  4. `REQUIREMENTS.md` traceability reflects the verification closure work
**Plans**: 3 plans

Plans:
- [x] 07-01-PLAN.md — refresh Phase 02 verification artifact for trail fade and redraw/performance evidence
- [x] 07-02-PLAN.md — refresh Phase 03 verification artifact for local rendering and input evidence
- [x] 07-03-PLAN.md — update requirements traceability and counts after Phase 02-03 verification closure

### Phase 8: Verify Configuration And Traceability Closure
**Goal**: Configuration behavior and planning traceability are corrected so the milestone can pass re-audit
**Depends on**: Phase 5
**Requirements**: CONF-01
**Gap Closure**: Closes Phase 05 verification gap and roadmap-to-requirements traceability drift identified by the milestone audit
**Success Criteria** (what must be TRUE):
  1. Phase 05 has a `VERIFICATION.md` covering configurable laser color and cleanup behavior
  2. `REQUIREMENTS.md` accurately reflects satisfied and pending v1 requirements after gap work lands
  3. Coverage counts in `REQUIREMENTS.md` match the traceability table
  4. Milestone re-audit no longer fails on missing verification artifacts or stale requirement bookkeeping
**Plans**: 4 plans

Plans:
- [x] 08-01-PLAN.md — create Phase 05 verification artifact from existing CONF-01 evidence
- [x] 08-02-PLAN.md — close requirements ledger drift and rerun milestone audit
- [x] 08-03-PLAN.md — fix host presence-color metadata upsert regression blocking CONF-01 evidence
- [x] 08-04-PLAN.md — rerun Phase 08 verification and resolve stale deferred regression note (completed 2026-04-18)

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Network Protocol & Host Relay | 1/1 | Complete | Yes |
| 2. Trail Buffer & Renderer | 2/2 | Complete | Yes |
| 3. Local Tool Implementation | 3/3 | Complete | Yes |
| 4. Multiplayer Integration | 3/3 | Complete | Yes |
| 5. Polish & Configuration | 3/3 | Complete | Yes |
| 6. Host Laser Inbound Rendering | 2/2 | Complete   | 2026-04-17 |
| 7. Verify Rendering And Input Coverage | 3/3 | Complete | 2026-04-17 |
| 8. Verify Configuration And Traceability Closure | 2/4 | In Progress | 2026-04-18 |
