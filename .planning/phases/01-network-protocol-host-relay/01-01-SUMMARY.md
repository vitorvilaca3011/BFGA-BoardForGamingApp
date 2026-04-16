---
phase: 01-network-protocol-host-relay
plan: 01
subsystem: network
tags: [messagepack, litenetlib, udp, serialization, protocol, laser-pointer]

# Dependency graph
requires: []
provides:
  - LaserPointerOperation type with MessagePack Union key 13
  - Host relay bypass for laser ops (no board state mutation)
  - Dedicated sequenced channel 2 for laser pointer delivery
  - ChannelsCount=3 in both GameHost and GameClient
affects: [02-laser-trail-model, 03-local-tool-host, 04-remote-rendering, 05-multiplayer-full-flow]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Ephemeral operation bypass pattern: early-return in HandleOperation/TryApplyLocalOperation before ApplyOperationCore"
    - "Dedicated channel per delivery semantic: Reliable=0, Unreliable=1, Sequenced=2"

key-files:
  created: []
  modified:
    - src/BFGA.Network/Protocol/BoardOperation.cs
    - src/BFGA.Network/GameHost.cs
    - src/BFGA.Network/GameClient.cs
    - tests/BFGA.Network.Tests/ProtocolTests.cs
    - tests/BFGA.Network.Tests/NetworkTests.cs

key-decisions:
  - "Used DeliveryMethod.Sequenced on channel 2 for laser ops — ordered without reliability overhead"
  - "LaserPointerOperation early-returns in both HandleOperation (network path) and TryApplyLocalOperation (local host path)"
  - "IsActive bool field enables fade-out signaling when laser released"

patterns-established:
  - "Ephemeral op bypass: check type before ApplyOperationCore, relay only, never touch _boardState"
  - "Channel routing in BroadcastOperation and SendOperation: type-based switch instead of bool reliable"

requirements-completed: [RNDR-03]

# Metrics
duration: 7min
completed: 2026-04-16
---

# Phase 01 Plan 01: Network Protocol & Host Relay Summary

**LaserPointerOperation with Union key 13, host relay bypass on dedicated sequenced channel 2, board state isolation proven by tests**

## Performance

- **Duration:** 7 min
- **Started:** 2026-04-16T00:46:48Z
- **Completed:** 2026-04-16T00:54:01Z
- **Tasks:** 2 (TDD — 4 commits total: 2 test + 2 feat)
- **Files modified:** 5

## Accomplishments
- LaserPointerOperation type with Position (Vector2) and IsActive (bool) fields, MessagePack Union key 13
- Host relay bypass: laser ops early-return in HandleOperation and TryApplyLocalOperation, never touch ApplyOperationCore
- Dedicated sequenced channel 2 with DeliveryMethod.Sequenced in both GameHost and GameClient
- Board state isolation proven: LastModified and Elements unchanged after laser ops
- 9 new tests (3 protocol serialization + 6 network relay/isolation)

## Task Commits

Each task was committed atomically (TDD RED→GREEN):

1. **Task 1: Define LaserPointerOperation type with MessagePack serialization**
   - `cd09de9` (test) — RED: 3 failing serialization tests
   - `387d4d7` (feat) — GREEN: LaserPointerOperation type, enum value, Union attribute
2. **Task 2: Implement host relay bypass, dedicated channel 2, board state isolation**
   - `5da65a0` (test) — RED: 6 failing relay/isolation tests
   - `326362a` (feat) — GREEN: SequencedChannel, ChannelsCount=3, HandleOperation bypass, BroadcastOperation routing

## Files Created/Modified
- `src/BFGA.Network/Protocol/BoardOperation.cs` — LaserPointerOperation class, OperationType.LaserPointer=13, Union(13)
- `src/BFGA.Network/GameHost.cs` — SequencedChannel=2, ChannelsCount=3, HandleOperation laser bypass, BroadcastOperation channel routing, IsOperationReliable update, TryApplyLocalOperation bypass
- `src/BFGA.Network/GameClient.cs` — SequencedChannel=2, ChannelsCount=3, SendOperation channel routing
- `tests/BFGA.Network.Tests/ProtocolTests.cs` — 3 new tests (serialization round-trip, OperationSerializer, polymorphic array)
- `tests/BFGA.Network.Tests/NetworkTests.cs` — 6 new tests (board state isolation, channel count, reliable flag, event firing, full client-host round-trip)

## Decisions Made
- Used DeliveryMethod.Sequenced (not Unreliable) for laser channel — preserves order which matters for trail rendering, drops stale packets automatically
- Early-return pattern in both HandleOperation AND TryApplyLocalOperation — handles both remote peer and local host laser paths
- IsActive bool field on LaserPointerOperation — enables peers to distinguish active pointing from release/fade-out

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- LaserPointerOperation type ready for use in Phase 02 (trail model) and Phase 03 (local tool)
- Network protocol complete: serialization, host relay, channel routing all tested
- No blockers for next phase

---
*Phase: 01-network-protocol-host-relay*
*Completed: 2026-04-16*

## Self-Check: PASSED
