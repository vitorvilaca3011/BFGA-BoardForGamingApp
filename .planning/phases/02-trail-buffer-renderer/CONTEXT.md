# Phase 2: Trail Buffer & Renderer — Context

## Goal
Fading laser trail renders smoothly at 60fps with zero GC pressure from point storage.

## Requirements
- **RNDR-02:** Fading trail follows cursor, decaying ~1-2s
- **RNDR-05:** Smooth frame rate, no canvas perf degradation

## Success Criteria
1. Ring buffer 128 capacity, old points overwritten, no list growth
2. Per-segment lines with per-point alpha decay (ease-out, ~1-2s fade)
3. ~~Single SKPaint reused~~ → Follow existing `using var` pattern for consistency
4. Fade animation timer ~16ms runs only while laser active, stops when none present

## Dependencies
- Phase 1 (network protocol) — `LaserPointerOperation` exists at `BoardOperation.cs` L402-428

## Decisions

| # | Area | Decision | Rationale |
|---|------|----------|-----------|
| D1 | SKPaint Strategy | Follow `using var` pattern (no reuse) | Consistency with all existing renderers. SKPaint alloc trivial vs. ring buffer savings. |
| D2 | Timer Location | BoardCanvas owns ~16ms fade timer | Rendering concern. Canvas checks laser state via styled property. ViewModel shouldn't know frame timing. |
| D3 | Timestamp Source | `Environment.TickCount64` | Monotonic, fast, ms precision. Sufficient for 1-2s fade calculation. |
| D4 | Draw Order | After remote cursors (topmost in pipeline) | Laser trails ARE the pointer visualization — should be most visible layer. |

## Architecture — New Components

### LaserTrailBuffer (BFGA.Canvas/Rendering/)
- Fixed-size ring buffer, capacity 128
- Stores `(Vector2 position, long timestampMs)` tuples
- `Add(Vector2, long)` overwrites oldest when full
- `GetPoints()` returns valid points oldest-to-newest for rendering
- `Clear()` resets on laser deactivation
- Zero allocation after construction

### RemoteLaserState (BFGA.Canvas/Rendering/ or Models/)
- Per-peer laser state: `LaserTrailBuffer`, `bool IsActive`, `long LastUpdateMs`
- Stored in `IReadOnlyDictionary<Guid, RemoteLaserState>` on BoardCanvas styled property

### LaserTrailRenderer (BFGA.Canvas/Rendering/)
- Static helper (matches `CollaboratorOverlayHelper` pattern)
- `DrawLaserTrails(SKCanvas, IReadOnlyDictionary<Guid, RemoteLaserState>, ...)` 
- Iterates points, computes alpha from age: `alpha = 1 - (age / decayMs)` with ease-out curve
- Draws `SKCanvas.DrawLine()` per segment with varying alpha
- Uses `using var paint = new SKPaint` per trail (follows convention)
- Skips points with alpha <= 0

### Integration Points

| What | Where | Action |
|------|-------|--------|
| Handle op | `MainViewModel.cs` ~L916 | Add `LaserPointerOperation` case → upsert `RemoteLaserState` |
| Styled property | `BoardCanvas.cs` ~L44 | `RemoteLasersProperty` — `IReadOnlyDictionary<Guid, RemoteLaserState>` |
| Render call | `BoardDrawOperation.Render()` ~L300 | Call `LaserTrailRenderer.DrawLaserTrails()` after cursors |
| Fade timer | `BoardCanvas.cs` | `DispatcherTimer` ~16ms, starts when any laser active, stops when all inactive |
| Invalidation | Timer tick | `InvalidateVisual()` to trigger re-render for fade animation |
| Constructor snapshot | `BoardDrawOperation` ctor ~L219 | Snapshot `RemoteLasers` dictionary (like cursors) |

## Risks
- **Timer lifecycle:** Must reliably stop when no lasers active to avoid idle CPU burn. Guard with check in tick handler.
- **Thread safety:** Dictionary snapshot in `BoardDrawOperation` constructor (UI thread) avoids cross-thread issues — same pattern as cursors.
- **Decay math precision:** `Environment.TickCount64` ms precision is fine for 1-2s decay. No sub-ms accuracy needed.

## Prior Context
- Phase 1 established: `LaserPointerOperation` (Union key 13), channel 2 sequenced, bypass board state
- CursorUpdateOperation pattern at MainViewModel L916 is the template for laser handling
- No existing ring buffer in codebase — build from scratch
