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

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Network Protocol & Host Relay | 1/1 | Complete | Yes |
| 2. Trail Buffer & Renderer | 2/2 | Complete | Yes |
| 3. Local Tool Implementation | 3/3 | Complete | Yes |
| 4. Multiplayer Integration | 3/3 | Complete | Yes |
| 5. Polish & Configuration | 3/3 | Complete | Yes |
