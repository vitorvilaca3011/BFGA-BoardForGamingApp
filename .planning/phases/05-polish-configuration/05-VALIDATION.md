---
phase: 05
slug: polish-configuration
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-16
---

# Phase 05 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + `dotnet test` |
| **Config file** | none |
| **Quick run command** | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~BoardScreenViewModelTests|FullyQualifiedName~BoardViewPipelineTests|FullyQualifiedName~MainViewModelTests" --no-restore -v q` |
| **Full suite command** | `dotnet test --no-restore -v q` |
| **Estimated runtime** | ~45 seconds |

---

## Sampling Rate

- **After every task commit:** Run task-local focused `dotnet test` command
- **After every plan wave:** Run `dotnet test --no-restore -v q`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 45 seconds

---

## Per-task Verification Map

| task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | CONF-01 | T-05-01 / T-05-02 | settings persist only through settings service; no toolbar source | unit | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~BoardScreenViewModelTests" --no-restore -v q` | ✅ | ⬜ pending |
| 05-01-02 | 01 | 1 | CONF-01 | T-05-03 | settings panel exposes labeled swatch UI only | unit | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~BoardScreenViewModelTests|FullyQualifiedName~PropertyPanelTests" --no-restore -v q` | ✅ | ⬜ pending |
| 05-02-01 | 02 | 2 | CONF-01 | T-05-04 / T-05-05 | host-authoritative color update path rejects spoofed peer identity | unit/integration | `dotnet test tests/BFGA.Network.Tests --filter "FullyQualifiedName~ProtocolTests|FullyQualifiedName~NetworkTests" --no-restore -v q` | ✅ | ⬜ pending |
| 05-02-02 | 02 | 2 | CONF-01 | T-05-06 | roster/cursor/laser identity converges from host metadata updates | unit | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~RosterOverlayTests" --no-restore -v q` | ✅ | ⬜ pending |
| 05-03-01 | 03 | 2 | CONF-01 | T-05-07 | local laser uses persisted presence color, not stroke color | unit | `dotnet test tests/BFGA.App.Tests --filter "FullyQualifiedName~BoardViewPipelineTests" --no-restore -v q` | ✅ | ⬜ pending |
| 05-03-02 | 03 | 2 | CONF-01 | T-05-08 | stale timeout becomes synthetic release without board redraw coupling | unit | `dotnet test tests/BFGA.Canvas.Tests --filter "FullyQualifiedName~LaserOverlayCanvasTests" --no-restore -v q` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Settings panel layout matches UI-SPEC spacing/copy | CONF-01 | AXAML visual arrangement not fully proven by unit tests | Open Settings, verify `LASER COLOR` label, helper copy, preview chip, 4x4 swatches, no toolbar picker |
| Live peer-visible color propagation | CONF-01 | requires two running app instances | Host + client, change color in Settings on either side, verify roster/cursor/laser update without restart |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
