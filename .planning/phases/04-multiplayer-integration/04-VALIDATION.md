---
phase: 04
slug: multiplayer-integration
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-16
---

# Phase 04 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 |
| **Config file** | none |
| **Quick run command** | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~BoardViewPipelineTests|FullyQualifiedName~MainViewModelTests" --no-restore -v q` |
| **Full suite command** | `dotnet test --no-restore -v q` |
| **Estimated runtime** | ~45 seconds |

---

## Sampling Rate

- **After every task commit:** Run task-scoped `dotnet test ... --filter ... --no-restore -v q`
- **After every plan wave:** Run `dotnet test --no-restore -v q`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 45 seconds

---

## Per-task Verification Map

| task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | MULT-01 | T-04-01 / T-04-02 | Pointer gesture emits only laser ops; sender id stays authoritative in network layer | unit | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~BoardViewPipelineTests" --no-restore -v q` | ✅ | ⬜ pending |
| 04-02-01 | 02 | 1 | MULT-01, MULT-02, MULT-03 | T-04-03 / T-04-04 | Remote state uses roster color, removes stale peers, preserves fade trail on release | unit | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~MainViewModelTests" --no-restore -v q` | ✅ | ⬜ pending |
| 04-03-01 | 03 | 1 | MULT-01, MULT-02, MULT-03 | T-04-05 | Dedicated overlay owns laser timer and draw path | unit | `dotnet test tests/BFGA.Canvas.Tests --filter "FullyQualifiedName~LaserOverlayCanvasTests" --no-restore -v q` | ❌ W0 | ⬜ pending |
| 04-03-02 | 03 | 1 | MULT-01, MULT-02, MULT-03 | T-04-06 | Laser updates redraw overlay only; board render path stays isolated | unit | `dotnet test tests/BFGA.Canvas.Tests --filter "FullyQualifiedName~BoardViewportOverlayTests" --no-restore -v q` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] Existing xUnit infrastructure already present in `tests/BFGA.App.Tests` and `tests/BFGA.Canvas.Tests`
- [ ] `tests/BFGA.Canvas.Tests/LaserOverlayCanvasTests.cs` — overlay timer/render isolation tests
- [ ] `tests/BFGA.Canvas.Tests/BoardViewportOverlayTests.cs` — dedicated overlay wiring tests

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Four peers visually point at once without flicker | MULT-03 | real-time LAN visual confidence check | Host 1 session + connect 3 clients, hold laser on all peers, confirm unique colors and smooth independent fade-out |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
