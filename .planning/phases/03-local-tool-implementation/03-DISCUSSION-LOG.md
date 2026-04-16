# Phase 3: Local Tool Implementation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves alternatives considered.

**Date:** 2026-04-15
**Phase:** 03-local-tool-implementation
**Areas discussed:** Visual Scale, Ping Feedback, Cursor Affordance, Exit / Cancel Behavior

---

## Visual Scale

| Option | Description | Selected |
|--------|-------------|----------|
| Screen-size constant | Dot, trail, and ping keep same visible size regardless of zoom | ✓ |
| Zoom-scaled | Laser visuals scale with board zoom because they live in scene space | |

**User's choice:** Screen-size constant
**Notes:** Positions still follow scene-space coordinates; only visual size should stay stable on screen.

---

## Ping Feedback

| Option | Description | Selected |
|--------|-------------|----------|
| Expanding ring + center dot | Pulse marker with ring expansion and center anchor point | ✓ |
| Dot flash | Minimal pulse without ring | |
| Large attention marker | Bigger visual callout | |

**User's choice:** Expanding ring + center dot
**Notes:** Ping should read clearly without becoming heavy UI noise.

---

## Cursor Affordance

| Option | Description | Selected |
|--------|-------------|----------|
| Crosshair cursor | Reuse standard crosshair affordance while laser tool selected | ✓ |
| Custom laser cursor | Add bespoke cursor artwork for laser mode | |
| Default arrow | No special affordance until press | |

**User's choice:** Crosshair cursor
**Notes:** Existing cursor factory already uses crosshair for several drawing tools, so this stays consistent.

---

## Exit / Cancel Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Cancel immediately on leave/capture loss/tool switch | Stop emitting instantly on any interruption | ✓ |
| Keep active until release | Continue laser if pointer leaves canvas/window while pressed | |

**User's choice:** Cancel immediately on pointer leave, capture loss, or tool switch
**Notes:** Cancel should still allow final fade-out cleanup rather than hard visual pop.

---

## OpenCode's Discretion

- Exact dot radius, trail thickness, and ping timing values within requirement ranges.
- Internal local-state model and rendering plumbing.

## Deferred Ideas

- `L` shortcut for laser tool — v2 requirement, not Phase 3.
- Auto-return to previous tool after release — v2 workflow, not Phase 3.
- Configurable laser color — Phase 5.
