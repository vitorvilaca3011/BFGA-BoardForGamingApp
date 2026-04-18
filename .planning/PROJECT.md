# BFGA — Board For Gaming App

## What This Is

A collaborative digital whiteboard and board-game companion built with Avalonia UI and SkiaSharp. It supports drawing, shapes, text, images, multiplayer sync, and a shipped real-time laser pointer with fading trail, ping behavior, host visibility, and configurable presence color.

## Core Value

Real-time collaborative canvas that stays in sync across all connected peers — drawing, shapes, and pointer interactions must feel instant and consistent.

## Requirements

### Validated

- [x] Canvas with pen drawing, shapes (rect, ellipse, line, arrow), text, and image placement
- [x] Selection, move, resize, and delete of elements
- [x] Undo/redo with operation-based history
- [x] Host/client multiplayer with host-authoritative state sync
- [x] Autosave and file-based board persistence (JSON)
- [x] Eraser tool
- [x] Pan and zoom (Hand tool + scroll)
- [x] Laser pointer tool — v1.0 (dot, fading trail, quick-tap ping, multiplayer visibility, configurable color)

### Active

- [ ] Keyboard shortcut `L` activates laser tool without toolbar click
- [ ] Laser trail uses variable thickness for comet-tail effect
- [ ] Releasing laser returns to previously active tool

### Out of Scope

- Persistent laser drawings — laser is ephemeral only, never saved to board state
- Laser pointer permissions/restrictions — any connected peer can use it
- Audio/video chat integration — separate concern, not this milestone

## Context

- Brownfield project with established architecture: Core -> Network -> Canvas -> App layers
- Current codebase size: ~17k LOC across C# and AXAML
- Shipped milestone `v1.0` added a full multiplayer-safe laser pointer stack across protocol, rendering, input, verification, and audit workflows
- Remaining carry-forward technical follow-ups from audit:
  - verify `ApplyOperationCore` fall-through does not update `_boardState.LastModified` for laser paths
  - verify `ChannelsCount = 3` has no side effects on existing cursor/board channels
- Remaining planning work: define next milestone from archived v2 requirements and any new user feedback

## Constraints

- **Tech stack**: .NET 9, Avalonia 11.3, SkiaSharp 3.x, LiteNetLib — must use existing stack
- **Architecture**: Must follow existing 4-layer pattern (Core -> Network -> Canvas -> App)
- **Performance**: Laser trail rendering must not degrade canvas FPS; use efficient point buffer
- **Network**: Laser state is transient — never persisted, only broadcast to active peers

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Dot + fading trail | More expressive than dot-only; shows movement path | Verified in v1.0 |
| Press-and-hold activation | Natural gesture; avoids accidental laser when browsing | Verified in v1.0 |
| Configurable color | Users want distinct laser colors in multiplayer | Verified in v1.0 |
| Visible to all peers | Core use case is pointing things out to others | Verified in v1.0 |
| ~1-2s fade time | Long enough to see trail, short enough to stay clean | Verified in v1.0 |
| Dedicated overlay for lasers | Keep transient laser redraws off board-render path | Verified in v1.0 |
| Host-authoritative presence color identity | Keep roster, cursor, and laser color in sync across peers | Verified in v1.0 |

## Current State

- Shipped `v1.0` on 2026-04-18.
- All v1 laser-pointer requirements are verified and archived in milestone history.
- Milestone audit is requirement-clean with only non-blocking technical debt follow-ups.

## Next Milestone Goals

- Define `v1.1` scope and fresh requirements with `/gsd-new-milestone`
- Decide whether to tackle carry-forward protocol technical debt first or ship new laser UX improvements
- Re-evaluate v2 laser enhancements against real usage feedback

---
*Last updated: 2026-04-18 after v1.0 milestone*
