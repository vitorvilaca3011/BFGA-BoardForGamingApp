# BFGA — Board For Gaming App

## What This Is

A collaborative digital whiteboard/board game companion built with Avalonia UI and SkiaSharp. Users draw, place shapes/images/text on a shared canvas with real-time multiplayer via LiteNetLib P2P networking. Designed for tabletop RPG and board game sessions.

## Core Value

Real-time collaborative canvas that stays in sync across all connected peers — drawing, shapes, and pointer interactions must feel instant and consistent.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. Inferred from existing codebase. -->

- [x] Canvas with pen drawing, shapes (rect, ellipse, line, arrow), text, and image placement
- [x] Selection, move, resize, and delete of elements
- [x] Undo/redo with operation-based history
- [x] Host/client multiplayer with host-authoritative state sync
- [x] Autosave and file-based board persistence (JSON)
- [x] Eraser tool
- [x] Pan and zoom (Hand tool + scroll)

### Active

<!-- Current scope. Building toward these. -->

- [ ] Laser pointer tool — dot at cursor + fading trail (~1-2s), press-and-hold activation, configurable color, visible to all peers in real-time

### Out of Scope

- Persistent laser drawings — laser is ephemeral only, never saved to board state
- Laser pointer permissions/restrictions — any connected peer can use it
- Audio/video chat integration — separate concern, not this milestone

## Context

- Brownfield project with established architecture: Core → Network → Canvas → App layers
- Existing tool infrastructure: `BoardToolType` enum, `BoardToolController` with per-tool handlers, toolbar AXAML bindings
- LaserPointer enum value and toolbar button already exist — zero behavior implemented
- Network protocol uses `BoardOperation` with MessagePack serialization; new op type needed for laser state
- SkiaSharp renders all canvas content; laser rendering will need custom draw pass (ephemeral, not part of element list)
- Known concerns: BoardToolController is already large; laser code should be well-isolated

## Constraints

- **Tech stack**: .NET 9, Avalonia 11.3, SkiaSharp 3.x, LiteNetLib — must use existing stack
- **Architecture**: Must follow existing 4-layer pattern (Core → Network → Canvas → App)
- **Performance**: Laser trail rendering must not degrade canvas FPS; use efficient point buffer
- **Network**: Laser state is transient — never persisted, only broadcast to active peers

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Dot + fading trail | More expressive than dot-only; shows movement path | — Pending |
| Press-and-hold activation | Natural gesture; avoids accidental laser when browsing | Verified in Phase 07 |
| Configurable color | Users want distinct laser colors in multiplayer | — Pending |
| Visible to all peers | Core use case is pointing things out to others | Verified in Phases 04 and 06 |
| ~1-2s fade time | Long enough to see trail, short enough to stay clean | Verified in Phase 07 |

## Current State

- Phase 07 complete: rendering and input claims from Phases 02-03 now have current verification artifacts and milestone traceability.
- `RNDR-01`, `RNDR-02`, `RNDR-04`, `RNDR-05`, `INPT-01`, and `INPT-02` are now verified and closed.
- Next gap-closure target: Phase 08 configuration verification and final traceability cleanup for `CONF-01`.

---
*Last updated: 2026-04-17 after Phase 07 completion*
