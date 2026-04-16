# Phase 4: Multiplayer Integration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves alternatives considered.

**Date:** 2026-04-16
**Phase:** 04-multiplayer-integration
**Areas discussed:** Laser color source, Overlay isolation, Remote release behavior, Multi-peer color policy

---

## Laser Color Source

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse assigned player color | Use existing `PlayerInfo.AssignedColor` for lasers too; keeps roster/cursor/laser identity aligned | ✓ |
| Separate laser-only color logic | Add distinct laser color assignment path now | |

**User's choice:** Reuse assigned player color.
**Notes:** Recommended selected. Phase 5 remains home for user-configurable color changes.

---

## Overlay Isolation

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated laser overlay | Isolate remote laser redraws from full board render path | ✓ |
| Keep current `BoardCanvas` path | Simpler wiring, but laser updates still invalidate full board draw | |

**User's choice:** Dedicated laser overlay.
**Notes:** Recommended selected. This directly addresses Phase 4 success criterion requiring isolated remote laser rendering.

---

## Remote Release Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Fade-out after release | Stop new points immediately, keep existing trail fading naturally | ✓ |
| Clear instantly | Remove remote trail as soon as deactivation arrives | |

**User's choice:** Fade-out after release.
**Notes:** Recommended selected. Keeps remote behavior aligned with local semantics from Phase 3.

---

## Multi-Peer Color Policy

| Option | Description | Selected |
|--------|-------------|----------|
| Existing host palette | Use current `GameHost.PlayerColors` assignment for Phase 4 | ✓ |
| Stricter laser-specific contrast rules | Add extra contrast/collision handling now | |

**User's choice:** Existing host palette.
**Notes:** Recommended selected. Four-peer acceptance target does not justify extra color negotiation in this phase.

---

## OpenCode's Discretion

- Exact dedicated overlay implementation form.
- Exact state/property plumbing for isolated invalidation.
- Test placement across projects.

## Deferred Ideas

- User-configurable laser color picker — Phase 5.
- Laser-specific contrast tuning if readability issues appear in later validation.
- Stale remote laser timeout policy — Phase 5.
