# Project Research Summary

**Project:** BFGA — Laser Pointer Tool (Milestone 2)
**Domain:** Collaborative whiteboard — ephemeral laser pointer with fading trail
**Researched:** 2026-04-15
**Confidence:** HIGH

## Executive Summary

BFGA needs a laser pointer tool: dot + fading trail, press-and-hold activation, multiplayer-visible, configurable color, fully ephemeral. This is a well-understood feature — Excalidraw, FigJam, PowerPoint all ship it. The core pattern is: circular buffer of timestamped points → per-segment alpha rendering → network broadcast as presence data. No new dependencies needed; everything builds on existing SkiaSharp rendering, LiteNetLib networking, and Avalonia UI patterns already in the codebase. The `BoardToolType.LaserPointer` enum and toolbar button already exist with zero behavior — this is pure implementation.

The recommended approach follows the `CursorUpdateOperation` precedent exactly: laser operations are ephemeral presence data that bypass board state entirely. Network messages use `DeliveryMethod.Sequenced` on a dedicated channel (channel 2) — resolving the ordering problem that `Unreliable` would cause for trail points while avoiding the latency of `ReliableOrdered`. Trail rendering uses per-segment `DrawLine` calls with computed alpha (NOT `DrawPath` which gives uniform alpha). A 128-entry circular buffer of `LaserTrailPoint` structs ensures zero GC pressure. A `DispatcherTimer` at 16ms drives fade animation only while any laser is active.

The critical risk is performance: laser fade animation at 60fps must NOT trigger full `BoardDrawOperation.Render()` cycles that re-render all board elements. The solution is rendering laser as an isolated overlay — either a separate `ICustomDrawOperation` or at minimum ensuring the laser render path is cheap and isolated from element rendering. Second major risk: laser operations accidentally flowing through the `ApplyOperationCore` → `CloneBoardState` pipeline, which would trigger full board clones at mouse-movement frequency. Both risks are avoided by following the cursor update bypass pattern rigorously.

## Key Findings

### Recommended Stack

No new packages. All capabilities exist in current stack (.NET 9, Avalonia 11.3, SkiaSharp 3.x, LiteNetLib 2.1, MessagePack 2.5). See [STACK.md](STACK.md) for full details.

**Core technologies (all existing):**
- **SkiaSharp `SKCanvas.DrawLine`**: Per-segment trail rendering with per-point alpha — `SKColor.WithAlpha(byte)` on reused `SKPaint`
- **`DispatcherTimer` (16ms)**: Drives fade animation + point pruning, only while laser active — consistent with existing timer patterns
- **LiteNetLib `Sequenced` on channel 2**: Drops stale packets, delivers latest in order — better than `Unreliable` (out-of-order jitter) or `ReliableOrdered` (unnecessary latency)
- **MessagePack `[Union]`**: New `LaserPointerOperation` (key 13) extending `BoardOperation` — existing serialization pattern
- **Circular buffer (`LaserTrailPoint[128]`)**: Fixed-size, zero-alloc, O(1) add/prune, cache-friendly — replaces `List<T>` growth/GC issues

### Expected Features

See [FEATURES.md](FEATURES.md) for competitor analysis and dependency graph.

**Must have (table stakes):**
- Visible dot at cursor position
- Fading trail (~1-1.5s, ease-out alpha curve)
- Ephemeral — never persisted to board state
- Visible to all connected peers in real-time
- Per-user color differentiation
- Keyboard shortcut (`L` key)
- Works correctly across zoom/pan transforms
- Smooth 60fps rendering

**Should have (differentiators):**
- Press-and-hold activation (PROJECT.md specifies this)
- Configurable laser color per user
- Multiple simultaneous laser pointers (4-player sessions)
- Auto-return to previous tool on release

**Defer (v2+):**
- Trail thickness variation (speed-based "comet tail")
- Ping/attention marker (tap-to-pulse)
- Name labels near laser dots

**Anti-features (deliberately excluded):**
- Persistent laser drawings
- Laser permissions/restrictions
- Sound effects
- Shape customization
- Snap-to-element highlighting

### Architecture Approach

Laser follows the `CursorUpdateOperation` pattern: ephemeral presence data with overlay rendering, completely bypassing board state and element pipeline. Five new files, six modifications to existing files. Local trail renders directly from tool state (zero network latency), remote trails reconstruct from inbound operations. See [ARCHITECTURE.md](ARCHITECTURE.md) for full component diagram and data flow.

**Major components:**
1. **`LaserPointerOperation`** (Network) — MessagePack-serialized op with Position, Color, IsActive. Union key 13.
2. **`LaserTrailBuffer`** (Canvas/Rendering) — Ring buffer of 128 `(Vector2, long)` structs. Timestamp-based aging, pruning, fade alpha computation.
3. **`LaserPointerRenderer`** (Canvas/Rendering) — Static helper drawing dot + fading trail. Per-segment `DrawLine` with computed alpha. Reuses single `SKPaint`.
4. **`LaserPointerTool`** (Canvas/Tools) — `IBoardTool` implementation. Owns local trail buffer, emits `LaserPointerOperation` on pointer events. First real user of `IBoardTool` interface.
5. **Remote laser state** (App/ViewModel) — `RemoteLaserPointers` dictionary on `MainViewModel`, upsert/remove methods, wired into `ApplyInboundOperation` short-circuit path.

### Critical Pitfalls

See [PITFALLS.md](PITFALLS.md) for full list with warning signs and phase mappings.

1. **Full board re-render on every fade tick** — Laser fade at 60fps triggers `BoardDrawOperation.Render()` for ALL elements. **Avoid:** Isolate laser overlay rendering; don't `InvalidateVisual()` the entire `BoardCanvas` for laser fade.
2. **Board clone triggered by laser ops** — Laser ops flowing through `ApplyOperationCore` → `CloneBoardState` = catastrophic. **Avoid:** Bypass board state pipeline entirely; handle laser ops as pure relay in host, short-circuit in `ApplyInboundOperation`.
3. **Out-of-order trail points on remote peers** — `Unreliable` delivery causes jitter. **Avoid:** Use `Sequenced` on dedicated channel 2; bump `ChannelsCount` to 3.
4. **Unbounded point buffer growth** — No pruning = 18K points after 5 min. **Avoid:** Fixed-size ring buffer (128 capacity), prune before render.
5. **SKPaint allocation per frame** — `new SKPaint()` 60x/sec = native malloc + GC pressure. **Avoid:** Create one `SKPaint`, mutate `.Color` per segment. Cache across frames.

## Implications for Roadmap

Based on dependency analysis across all 4 research files, suggested 5-phase structure:

### Phase 1: Network Protocol + Host Relay
**Rationale:** Everything depends on `LaserPointerOperation` type existing. Network routing decisions (bypass board state, sequenced delivery) must be correct from start — Pitfalls #2 and #3 are architectural and unfixable later.
**Delivers:** `LaserPointerOperation` type, `OperationType.LaserPointer = 13`, `IsOperationReliable()` exclusion, `GameHost` early-return in `ApplyOperationCore`, `GameClient` sequenced channel routing, `ChannelsCount = 3`.
**Addresses:** Network operation type (table stake), sequenced delivery
**Avoids:** Pitfall #2 (board clone), Pitfall #3 (out-of-order delivery)

### Phase 2: Trail Buffer + Renderer
**Rationale:** Rendering components have zero network dependency but are needed by both tool (Phase 3) and remote state integration (Phase 4). Building renderer before tool allows visual validation of the fade algorithm.
**Delivers:** `LaserTrailBuffer` (ring buffer), `LaserTrailPoint` struct, `LaserPointerRenderer` (dot + fading trail), fade alpha computation with ease-out curve.
**Addresses:** Fading trail (table stake), smooth 60fps rendering (table stake)
**Avoids:** Pitfall #4 (unbounded buffer), Pitfall #5 (SKPaint allocation)

### Phase 3: Local Tool Implementation
**Rationale:** Needs both operation type (Phase 1) and trail buffer (Phase 2). Local tool produces immediate visual feedback without network roundtrip — critical UX requirement.
**Delivers:** `LaserPointerTool` (IBoardTool), `BoardToolController` case for LaserPointer, press-and-hold activation, local trail rendering from tool state, `LaserPointerOperation` emission on pointer events, keyboard shortcut (`L`).
**Addresses:** Press-and-hold (differentiator), local visual feedback, tool activation
**Avoids:** Anti-pattern: network roundtrip for local trail, controller bloat (delegate to tool class)

### Phase 4: Remote State + Full Integration
**Rationale:** Full multiplayer integration requires all preceding components. Wires network ↔ ViewModel ↔ Canvas for remote laser rendering.
**Delivers:** `MainViewModel.RemoteLaserPointers` + `ApplyInboundOperation` case, `BoardCanvas.LaserPointersProperty`, `BoardDrawOperation` snapshot + render call, `BoardView` bindings, fade `DispatcherTimer` (16ms, start/stop lifecycle).
**Addresses:** Visible to all peers (table stake), per-user color (table stake), multiple simultaneous lasers (differentiator)
**Avoids:** Pitfall #1 (full board re-render — laser overlay isolation critical here)

### Phase 5: Polish + Edge Cases
**Rationale:** All core functionality works. Polish addresses UX pitfalls and robustness.
**Delivers:** Cleanup on peer disconnect, cleanup on tool switch, deactivation signal (IsActive=false) via reliable delivery, zoom-independent dot size, auto-return to previous tool, configurable laser color, timeout for stale remote lasers (3s), ease-out alpha curve tuning.
**Addresses:** Remaining differentiators, UX pitfalls
**Avoids:** Ghost trails, frozen remote lasers, zoom-scaling issues

### Phase Ordering Rationale

- **Network first** because routing decisions are architectural — wrong routing (through board state pipeline) causes catastrophic perf, unfixable without rewiring.
- **Buffer+Renderer before Tool** because renderer is shared by local + remote paths. Building it standalone allows unit testing fade math without input event complexity.
- **Local tool before remote integration** because local UX validation is faster (no multiplayer setup needed) and proves the render pipeline works.
- **Remote integration last** (before polish) because it touches the most files (ViewModel, Canvas, View bindings) and has highest integration risk.
- **Polish separate** because edge cases (disconnect cleanup, tool switch, zoom scaling) are independent and testable after core flow works.

### Research Flags

**Phases needing deeper research during planning:**
- **Phase 1 (Network):** Verify `GameHost.ApplyOperationCore` fall-through behavior — does it update `_boardState.LastModified`? Need explicit early-return. Verify `ChannelsCount` bump to 3 has no side effects.
- **Phase 4 (Integration):** Laser overlay isolation strategy needs prototyping. Options: separate `ICustomDrawOperation`, `CompositionCustomVisualHandler`, or scoped invalidation. Perf testing with 200+ elements required.

**Phases with standard patterns (skip research):**
- **Phase 2 (Buffer+Renderer):** Ring buffer is textbook. SkiaSharp `DrawLine` + alpha mutation well-documented.
- **Phase 3 (Tool):** Follows `IBoardTool` interface. Pointer event handling matches existing tool patterns.
- **Phase 5 (Polish):** All items straightforward — cleanup handlers, timeout logic, color config.

## Conflict Resolutions

Researchers had minor disagreements, resolved as follows:

| Topic | Stack Says | Architecture Says | Pitfalls Says | Resolution |
|-------|-----------|-------------------|---------------|------------|
| **Network delivery** | Sequenced on channel 1 (shared with cursor) | Unreliable (like CursorUpdate) | Sequenced on channel 2 (dedicated) | **Sequenced on channel 2.** Unreliable causes out-of-order jitter (Pitfall #3). Dedicated channel prevents laser/cursor interference. Bump `ChannelsCount` to 3. |
| **Timer interval** | 16ms (60fps) | 33ms (30fps) | 30Hz sufficient for overlay | **16ms for smooth fade.** 33ms acceptable fallback if perf constrained. Fade animation quality noticeable at 30fps — 60fps preferred. |
| **Render approach** | Per-segment `DrawLine` | Agrees | Warns about full board re-render | **Per-segment `DrawLine` with isolated overlay.** Consensus on technique; Pitfalls adds critical constraint about overlay isolation. |
| **Build order** | Data→Render→Network→Polish | Network→Buffer+Render→Tool→Integration | N/A | **Network first.** Routing decisions are architectural. Wrong routing = catastrophic perf (Pitfall #2). |

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All technologies already in codebase. APIs verified via Context7 (SkiaSharp, LiteNetLib, Avalonia). Zero new dependencies. |
| Features | HIGH | Excalidraw source directly reviewed (PR #6739). FigJam/PowerPoint/Miro cross-referenced. Feature landscape well-mapped. |
| Architecture | HIGH | Based on direct codebase reading. `CursorUpdateOperation` is exact precedent. File paths, integration points, data flow all verified. |
| Pitfalls | HIGH | Grounded in known codebase concerns (CONCERNS.md). Performance traps verified against SkiaSharp allocation patterns and existing rendering pipeline. |

**Overall confidence:** HIGH — Well-scoped brownfield feature with direct codebase precedent (`CursorUpdateOperation`). No unknowns in the technology stack. All four research files cross-reference consistently.

### Gaps to Address

- **Overlay isolation strategy:** Research identified the NEED to isolate laser rendering from full board re-render but didn't prototype the solution. Options (separate `ICustomDrawOperation`, `CompositionCustomVisualHandler`, scoped invalidation) need evaluation during Phase 4 planning. May need `/gsd-research-phase`.
- **`ApplyOperationCore` fall-through side effects:** Architecture assumes laser ops fall through harmlessly (like CursorUpdate), but Pitfalls flags that `_boardState.LastModified` gets updated. Need explicit `case LaserPointerOperation: return;` — verify during Phase 1.
- **Deactivation reliability:** Architecture says `IsActive=false` on unreliable channel. Pitfalls recommends reliable delivery for stop signal to prevent ghost trails. Resolution: send reliable deactivation + 3s timeout safety net. Decide during Phase 1 planning.
- **Network throttling:** Stack says 60/sec, Architecture says 30-40/sec, Pitfalls says 20Hz batched. Start at 60Hz with `Sequenced` (auto-drops stale), add client-side throttling if bandwidth issues observed with 4+ peers.
- **Zoom-independent rendering:** Trail points in board coords, but dot/trail width must be screen-space. Transform logic needs detail during Phase 2 implementation.

## Sources

### Primary (HIGH confidence)
- Context7 `/mono/skiasharp` — `SKPaint`, `DrawLine`, `SKColor.WithAlpha`, paint mutation patterns
- Context7 `/revenantx/litenetlib` — `DeliveryMethod.Sequenced`, channel semantics, `ChannelsCount` config
- Context7 `/avaloniaui/avalonia-docs` — `DispatcherTimer`, `InvalidateVisual()`, `CompositionCustomVisualHandler`, `ICustomDrawOperation`
- Excalidraw source: `laser-trails.ts`, `animated-trail.ts`, PR #6739 — gold standard reference implementation
- BFGA codebase direct inspection — `CursorUpdateOperation`, `BoardDrawOperation`, `CollaboratorOverlayHelper`, `GameHost`, `GameClient`, `MainViewModel`

### Secondary (MEDIUM confidence)
- FigJam cursor/collaboration features — indirect laser pointer reference
- PowerPoint laser pointer behavior — well-known product, specific docs URL returned 404

### Tertiary (LOW confidence)
- Miro laser pointer — community confirms feature exists in premium tier, help article behind login wall

---
*Research completed: 2026-04-15*
*Ready for roadmap: yes*
