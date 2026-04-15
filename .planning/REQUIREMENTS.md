# Requirements: BFGA Laser Pointer Tool

**Defined:** 2026-04-15
**Core Value:** Real-time collaborative canvas that stays in sync across all connected peers — laser pointer interactions must feel instant and consistent.

## v1 Requirements

### Rendering

- [ ] **RNDR-01**: User sees a colored dot at cursor position when laser tool is active and mouse is pressed
- [ ] **RNDR-02**: Fading trail follows cursor movement, decaying over ~1-2 seconds after each point is drawn
- [ ] **RNDR-03**: Laser rendering is ephemeral — never saved to board state, element list, or JSON file
- [ ] **RNDR-04**: Laser dot and trail render correctly at all zoom levels and pan positions (scene-space coords, viewport transform on render)
- [ ] **RNDR-05**: Trail animation runs at smooth frame rate without degrading canvas performance

### Multiplayer

- [ ] **MULT-01**: Local user's laser dot and trail are visible to all connected peers in real-time via network broadcast
- [ ] **MULT-02**: Each peer's laser renders with a distinct per-user color
- [ ] **MULT-03**: Multiple users can use laser pointers simultaneously — each peer's trail renders independently

### Input

- [ ] **INPT-01**: Laser activates on mouse press and deactivates on mouse release (press-and-hold gesture)
- [ ] **INPT-02**: Quick tap (short click <200ms, <5px movement) produces a pulsing ping marker that fades after ~2-3 seconds

### Configuration

- [ ] **CONF-01**: User can configure their laser pointer color via a color picker or setting

## v2 Requirements

### Input

- **INPT-03**: Keyboard shortcut `L` activates laser tool without toolbar click

### Rendering

- **RNDR-06**: Trail has variable thickness — thicker at head, thinner at tail (comet tail effect using speed-based size mapping)

### Workflow

- **WKFL-01**: After releasing laser, automatically return to the previously active tool

## Out of Scope

| Feature | Reason |
|---------|--------|
| Persistent laser drawings | Laser is ephemeral gesture, not a drawing. Pollutes board state. |
| Laser pointer permissions | All peers can laser. Social norms sufficient for board game context. |
| Sound effects on laser | Annoying in multi-user. 4 players all making sounds = chaos. |
| Laser shape variants | Over-engineering. Dot is universally understood. Zero UX gain. |
| Trail history/replay | Massive storage for ephemeral data. No board game use case. |
| Dot size customization | Premature. Default 4-6px works. Revisit if feedback demands. |
| Snap-to-element highlight | Confuses laser with selection. Use Select tool for element interaction. |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| RNDR-01 | — | Pending |
| RNDR-02 | — | Pending |
| RNDR-03 | — | Pending |
| RNDR-04 | — | Pending |
| RNDR-05 | — | Pending |
| MULT-01 | — | Pending |
| MULT-02 | — | Pending |
| MULT-03 | — | Pending |
| INPT-01 | — | Pending |
| INPT-02 | — | Pending |
| CONF-01 | — | Pending |

**Coverage:**
- v1 requirements: 11 total
- Mapped to phases: 0
- Unmapped: 11 (pending roadmap creation)

---
*Requirements defined: 2026-04-15*
*Last updated: 2026-04-15 after /gsd-new-project*
