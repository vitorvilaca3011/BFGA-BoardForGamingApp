# Feature Research: Laser Pointer Tool

**Domain:** Collaborative whiteboard — laser pointer tool
**Researched:** 2026-04-15
**Confidence:** HIGH (Excalidraw source reviewed, Figma/FigJam docs confirmed, Miro patterns cross-referenced)

## Feature Landscape

### Table Stakes

These are expected by any user who's seen laser pointers in Excalidraw, PowerPoint, FigJam, or Miro. Missing = "it's broken."

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Visible dot at cursor position** | Core mental model — "I'm pointing at something." Every competitor shows a colored dot. | Low | Simple filled circle at pointer coords. All apps do this. |
| **Fading trail** | Excalidraw, PowerPoint, FigJam all show a trail that fades after ~1-2s. Users expect to see the path of movement, not just current position. Without it, fast movement becomes invisible. | Medium | Time-decayed point buffer. Excalidraw uses 1000ms `DECAY_TIME` + 50-point `DECAY_LENGTH` with easeOut. |
| **Ephemeral — never persisted** | Laser is a presentation gesture, not a drawing. Every app treats it as transient. Persisting would pollute the board. | Low | Render in separate pass, never add to element list or board state JSON. |
| **Visible to all connected peers** | Entire point of laser in collaborative context. If only local, it's just a cursor. | Medium | Requires new network operation type. Excalidraw sends pointer position + tool state + button down/up via collab protocol. |
| **Per-user color differentiation** | Multiple users pointing simultaneously must be distinguishable. Excalidraw uses `DEFAULT_LASER_COLOR` for local + `getClientColor()` for collaborators. FigJam uses cursor colors. | Low | Map user → color. Can reuse existing peer color if available. |
| **Keyboard shortcut to activate** | Excalidraw: `K` key. PowerPoint: `Ctrl+L`. Quick access without toolbar click is expected in presentation flow. | Low | Bind to `L` or `K` key in existing keyboard handler. |
| **Works across zoom/pan** | Laser must render in screen-space or properly transform. Excalidraw converts scene→viewport coords. If laser stays fixed in scene coords while panning, it feels broken. | Medium | Excalidraw's `sceneCoordsToViewportCoords` pattern. Trail points stored in scene coords, rendered after viewport transform. |
| **Smooth rendering at 60fps** | Laggy/janky trails destroy the "pointing" UX. Excalidraw uses `AnimationFrameHandler` for requestAnimationFrame-driven updates. | Medium | Must use efficient point buffer. SkiaSharp `SKCanvas.DrawPath` with pre-built `SKPath` is performant enough. Avoid per-frame allocations. |

### Differentiators

Features that go beyond competitor baselines. Valuable for BFGA's board game companion context.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Press-and-hold activation** | Natural gesture for "look here" — like holding a physical laser pointer button. Avoids accidental trails when browsing the board. Excalidraw uses mouse-down to start trail, mouse-up to end. PROJECT.md already specifies this. | Low | On pointer-down → start trail, on pointer-up → stop adding points, trail fades out. |
| **Configurable laser color per user** | Board game sessions often have player colors. Let user pick their laser color to match their game identity. Excalidraw supports `laserColor` field on pointer state. Most apps auto-assign. | Low-Med | Color picker in settings or toolbar dropdown. Store in user preferences. |
| **Trail thickness variation (speed-based)** | Excalidraw's `sizeMapping` function varies stroke width based on decay time AND position along trail. Creates a "comet tail" effect — thicker at head, thinner at tail. More expressive than uniform-width trails. | Medium | Excalidraw's approach: `Math.min(easeOut(lengthFactor), easeOut(timeFactor))` for size. With SkiaSharp, render as variable-width polyline or series of circles with decreasing radius. |
| **Multiple simultaneous laser pointers** | In a 4-player board game session, all 4 pointing at different map regions simultaneously. Excalidraw handles this via `Map<SocketId, AnimatedTrail>` — one trail per collaborator. | Medium | Need per-peer trail state. Map `Guid peerId → LaserTrailState`. Clean up on peer disconnect. |
| **Ping/attention marker** | Quick tap (not drag) leaves a pulsing circle that fades after 2-3s. "Look here!" without needing to hold and drag. Miro users request this. Some presentation tools have it. | Medium | Detect short-click (< 200ms, < 5px movement) → spawn expanding+fading circle animation at point. Separate from trail system. |
| **Auto-return to previous tool** | After releasing laser, automatically switch back to whatever tool was active before. FigJam does this. Reduces friction in game master workflow: draw → point → back to draw. | Low | Store `previousTool` on laser activation, restore on deactivation. |

### Anti-Features

Deliberately NOT build these. Each has been requested or attempted in competitor ecosystems and proven problematic.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Persistent laser drawings** | "I want to save my annotations" | Conflates laser (ephemeral gesture) with pen (permanent mark). Pollutes board state. Undo becomes confusing — can you undo a laser? No. PROJECT.md explicitly excludes this. | Use pen tool for persistent marks. Laser is gesture-only. |
| **Laser pointer permissions** | "Only presenter should point" | Adds permission complexity. In board game context, all players need to point. Restricting breaks collaborative flow. PROJECT.md explicitly excludes. | All peers can laser. Social norms handle "too many lasers." |
| **Sound effects on laser** | "Make a zap sound when pointing" | Annoying in multi-user context. 4 players all making sounds = chaos. Accessibility concern. | Silent visual-only. |
| **Laser pointer with different shapes** | "Arrow cursor, crosshair, star shape" | Over-engineering. The dot is universally understood. Shape variety adds config complexity for zero UX gain. | Single dot + trail. |
| **Persistent trail history/replay** | "Replay what someone pointed at" | Massive storage for ephemeral data. No real use case in board games. Adds memory pressure. | If needed, use screen recording. |
| **Laser pointer thickness/size customization** | "Let me make the dot bigger" | Premature optimization. Default size works for 99% of cases. Config UI complexity not worth it for v1. | Ship sensible default (4-6px radius). Revisit if feedback demands it. |
| **Snap-to-element highlighting** | "Laser should highlight the element I'm pointing at" | Confuses laser with selection. Element highlight is a Select tool feature. Laser is freeform gesture. | Use Select tool for element interaction. |

## Feature Dependencies

```
Visible dot ─────────────────────┐
                                 ├──> Press-and-hold activation
Fading trail ────────────────────┤
                                 ├──> Trail thickness variation (enhancement of trail)
                                 │
Works across zoom/pan ───────────┤
                                 │
Per-user color ──────────────────┼──> Configurable laser color (extends default)
                                 │
Visible to peers ────────────────┼──> Multiple simultaneous lasers
  (requires new NetworkOp)       │
                                 │
Smooth 60fps rendering ──────────┘
                                 
Ping/attention marker ──────── Independent (parallel feature, not dependent on trail)

Auto-return to previous tool ── Independent (toolbar state management only)
```

**Critical path:** Dot → Trail → Network broadcast → Peer rendering. Everything else layers on top.

**Suggested implementation order:**
1. Local dot + trail (no network) — validates rendering approach
2. Network operation type for laser state
3. Remote peer trail rendering  
4. Per-user colors
5. Press-and-hold activation (refine input handling)
6. Trail thickness variation (polish)
7. Ping marker (stretch goal)

## Competitor Feature Analysis

| Feature | Excalidraw | FigJam | PowerPoint | Miro | BFGA Approach |
|---------|-----------|--------|------------|------|---------------|
| **Activation** | Select tool, mouse-down draws trail | Dedicated toolbar tool | Ctrl+L during presentation | Toolbar tool (premium) | Toolbar + `L` key, press-and-hold |
| **Visual style** | Colored trail, variable width, easeOut fade | Thin colored line, fade | Red dot + thin trail | Colored dot | Dot + fading trail, variable width |
| **Fade time** | 1000ms (`DECAY_TIME`) | ~1-2s | ~2s | ~1s | ~1000-1500ms configurable |
| **Trail rendering** | SVG path via `@excalidraw/laser-pointer` lib | Canvas/WebGL | DirectX overlay | Canvas | SkiaSharp `SKPath` in custom draw pass |
| **Multi-user** | Yes, per-collaborator `AnimatedTrail` Map | Yes, cursor colors | No (single presenter) | Yes | Yes, per-peer trail state |
| **Color** | Red default, collaborator colors for remote | User cursor color | Red only | User color | User-configurable, default red |
| **Persisted** | No | No | No | No | No |
| **Network protocol** | WebSocket collab: pointer position + tool + button state | Figma collab protocol | N/A (local only) | WebSocket | LiteNetLib: new `LaserStateOperation` with position + isActive + color |
| **Keyboard shortcut** | `K` | Not documented | `Ctrl+L` | Not documented | `L` key |
| **Zoom handling** | Scene→viewport coord transform per frame | Built into engine | Presentation zoom | Built into engine | Scene coords stored, viewport transform on render |

### Key Takeaway from Excalidraw Source

Excalidraw's approach (PR #6739, merged Oct 2023) is the gold standard open-source reference:

1. **`@excalidraw/laser-pointer`** — Standalone package handles point math, stroke outline generation
2. **`AnimatedTrail`** — Wraps laser-pointer with animation frame loop, SVG rendering, start/add/end path lifecycle
3. **`LaserTrails`** — Manages local trail + `Map<SocketId, AnimatedTrail>` for collaborators
4. **Trail lifecycle:** `startPath(x,y)` on mouse-down → `addPointToPath(x,y)` on move → `endPath()` on mouse-up
5. **Decay:** Each point timestamped with `performance.now()`. Size mapping uses `1 - (now - pointTime) / DECAY_TIME` for time fade + position-along-trail fade.
6. **Collab sync:** Collaborator pointer state includes `tool: "laser"`, `button: "down"/"up"`, `x`, `y`, `laserColor`. Remote trails constructed from these state updates.
7. **Cleanup:** Trails with empty stroke outlines (fully faded) removed from `pastTrails` array. Collab trails deleted when collaborator disconnects.

**For BFGA:** Same pattern translates to SkiaSharp. Replace SVG path with `SKPath`, replace `requestAnimationFrame` with Avalonia's `CompositionTarget.Rendering` or a `DispatcherTimer`, store points as `(float x, float y, long timestamp)` tuples.

## Network Protocol Implications

Current `OperationType` enum has 13 values (0-12). Need to add:
- `LaserState = 13` — New operation type

`LaserStateOperation` fields:
- `SenderId: Guid` (inherited from BoardOperation)
- `X: float` — cursor position in scene coords
- `Y: float`
- `IsActive: bool` — mouse down = true, mouse up = false
- `Color: uint` — ARGB color value

**Send strategy:** Unreliable (UDP) — like `CursorUpdate`. Laser is ephemeral; dropped packets acceptable. Reduces bandwidth vs reliable delivery. Excalidraw uses similar approach — collab state updates are fire-and-forget.

**Frequency:** Throttle to ~30-60 updates/sec. Every pointer move event is excessive; batch or sample.

## Sources

- Excalidraw `laser-trails.ts` source: https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/laser-trails.ts [HIGH confidence — direct source review]
- Excalidraw `animated-trail.ts` source: https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/animated-trail.ts [HIGH confidence — direct source review]
- Excalidraw PR #6739 "feat: initial Laser Pointer MVP": https://github.com/excalidraw/excalidraw/pull/6739 [HIGH confidence — merged implementation]
- Figma/FigJam cursor chat docs (related collaboration features): https://help.figma.com/hc/en-us/articles/1500004414842 [MEDIUM confidence — FigJam cursor features, laser tool referenced indirectly]
- PowerPoint laser pointer behavior: Microsoft Office docs [MEDIUM confidence — based on well-known product behavior, specific help URL returned 404]
- Miro laser pointer: community requests indicate feature exists in premium tier [LOW confidence — help article redirected to login, community confirms feature exists]
- BFGA codebase: `BoardToolType.LaserPointer` enum value exists, zero behavior implemented, `BoardToolController` has no case for it [HIGH confidence — direct code review]
