---
phase: 08
slug: verify-configuration-and-traceability-closure
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-17
---

# Phase 08 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + `dotnet test` |
| **Config file** | none |
| **Quick run command** | `dotnet test --no-restore -v q` |
| **Full suite command** | `dotnet test --no-restore -v q` |
| **Estimated runtime** | ~45 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --no-restore -v q`
- **After every plan wave:** Run `dotnet test --no-restore -v q`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 45 seconds

---

## Per-task Verification Map

| task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 08-01-01 | 01 | 1 | CONF-01 | T-08-01 / T-08-02 | verification artifact cites exact trusted evidence and manual-only gaps explicitly | unit/docs | `dotnet test --no-restore -v q` | ✅ | ⬜ pending |
| 08-02-01 | 02 | 2 | CONF-01 | T-08-03 | requirements ledger updated only after verification artifact exists | unit/docs | `dotnet test --no-restore -v q` | ✅ | ⬜ pending |
| 08-02-02 | 02 | 2 | CONF-01 | T-08-04 / T-08-05 | milestone audit regenerated from current verification + requirements state | unit/docs | `dotnet test --no-restore -v q` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Settings panel layout still matches approved `LASER COLOR` UI contract | CONF-01 | exact AXAML spacing/visual polish still needs human eyes | Open Settings, verify `LASER COLOR` label, helper copy, preview chip, 4x4 swatches, no toolbar picker |
| Live peer-visible color propagation still works across two running app instances | CONF-01 | requires host + client runtime, not headless unit test only | Host + client, change color in Settings, verify roster/cursor/laser update without restart |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
