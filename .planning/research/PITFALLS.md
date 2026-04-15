# Pitfalls Research

**Domain:** Collaborative whiteboard — laser pointer tool
**Researched:** 2026-04-15
**Confidence:** HIGH (verified against codebase + Context7 docs for SkiaSharp, LiteNetLib, Avalonia)

---

## Critical Pitfalls

### Pitfall 1: Fading Trail Drives Continuous Rendering — CPU/GPU Death Spiral

**What goes wrong:** Trail points fade over 1-2s. Naive approach: run a DispatcherTimer at 16ms that calls `InvalidateVisual()` to animate opacity. This forces full `BoardDrawOperation.Render()` every frame — re-rendering ALL board elements, dot grid, remote cursors, stroke previews, and selection overlays. With 200+ elements and multiple users, CPU pegs at 100%.

**Why it happens:** Current `BoardCanvas.Render()` creates a `BoardDrawOperation` that redraws everything. No layer separation. Laser fade animation means ~60 redraws/sec for the entire scene, even when nothing else changes.

**How to avoid:**
- Render laser trail in a **separate overlay control** stacked on top of `BoardCanvas` (or a separate `ICustomDrawOperation` that only redraws the laser layer).
- Use Avalonia's `CompositionCustomVisualHandler` (confirmed available in Avalonia 11.x via Context7 docs) for render-thread animation — calls `RequestNextFrameRendering()` for the laser overlay only, not the whole board.
- Alternatively: use a dedicated `DispatcherTimer` at 30Hz (not 60) that only invalidates the laser overlay, not `BoardCanvas`.
- Trail points that have fully faded (alpha = 0) must be pruned from the buffer to stop the timer when no active trails exist.

**Warning signs:**
- CPU usage stays high when laser is active but pointer isn't moving
- FPS drops during laser use with many elements on board
- `BoardDrawOperation.Render()` showing up in profiler during laser fade

**Phase to address:** Rendering phase — must be architectural decision BEFORE implementing trail drawing.

---

### Pitfall 2: Full Board Clone Triggered by Laser Network Messages

**What goes wrong:** Current pattern: host receives operation → `ApplyOperationCore()` → `_boardState.Elements = _boardElements.Values.ToList()` → `MainViewModel.SyncBoardFromHost()` → `CloneBoardState()` (full MessagePack serialize/deserialize). If laser operations flow through this same pipeline, every laser point broadcast triggers a full board clone. At 20-60 Hz mouse movement = 20-60 full board clones per second per laser user.

**Why it happens:** Laser pointer op might be treated as a regular `BoardOperation` routed through `GameHost.ApplyOperationCore()`. But laser is ephemeral — it NEVER modifies board state. Routing it through the state pipeline is incorrect and catastrophically expensive.

**How to avoid:**
- Laser operations must **bypass** `ApplyOperationCore()` entirely. Handle them in `GameHost.ProcessInboundOperation()` as a pure relay: validate sender, broadcast to peers, done. No state modification, no undo stack, no board clone.
- In `MainViewModel`, laser ops should update a `RemoteLaserState` dictionary (like `RemoteCursors`) — never touch `Board` or `SyncBoardFromHost()`.
- Model laser ops similar to `CursorUpdateOperation` — pure presence data, not board mutations.

**Warning signs:**
- `CloneBoardState()` called at mouse-movement frequency during laser use
- GC pressure spikes (Gen0 collections) during laser activity
- Board responsiveness degrades when any user activates laser

**Phase to address:** Network protocol design phase — must decide operation routing BEFORE implementing host-side handling.

---

### Pitfall 3: Using `DeliveryMethod.Unreliable` for Laser Trail Points

**What goes wrong:** Current `CursorUpdateOperation` uses `DeliveryMethod.Unreliable`. Temptation: do the same for laser. Problem: `Unreliable` delivers packets in any order. Trail points arriving out-of-order produce jagged, zigzag trails on remote peers. Also: UDP packet loss means gaps in the trail, causing the trail to "teleport" between positions.

**Why it happens:** Developer assumes "cursor and laser are similar" and copies the cursor delivery strategy. Cursor only needs latest position (a dot). Laser needs ordered sequence of positions (a trail).

**How to avoid:**
- Use `DeliveryMethod.Sequenced` (confirmed in LiteNetLib via Context7): delivers only the latest packet, drops stale ones, but preserves order. Perfect for laser where you want the latest trail state.
- **Better approach:** Batch trail points. Send `LaserTrailUpdate { Points: Vector2[], IsActive: bool }` every 50ms containing the last N points with timestamps. Single packet = atomic trail segment. Use `Sequenced` delivery on a dedicated channel.
- Configure `ChannelsCount` to 3 (currently 2): channel 0 = reliable, channel 1 = unreliable cursors, channel 2 = sequenced laser.

**Warning signs:**
- Remote laser trails appear jagged/zigzag while local trail is smooth
- Trail has visible "teleport" gaps
- Occasional out-of-order trail rendering

**Phase to address:** Network protocol design phase.

---

### Pitfall 4: Unbounded Trail Point Buffer — Memory Leak

**What goes wrong:** Each laser trail point is stored with a timestamp for fade calculation. Fast mouse movement at 60Hz = 60 points/sec. If trail fades over 2s, buffer should max at ~120 points. But without proper pruning: user holds laser for 5 minutes = 18,000 points per user. With 4 users = 72,000 points. Each `Vector2 + timestamp + alpha` ≈ 20 bytes. Not huge for memory, but iterating 72,000 points per render frame at 30-60 FPS is the real problem.

**Why it happens:** Focus on "add points" without implementing "remove expired points." Or pruning happens only on render, not on ingest.

**How to avoid:**
- Use a **ring buffer** (fixed-size `Vector2[]` + write index) with capacity = `maxFadeTimeMs / sampleIntervalMs`. For 2s fade at 50ms batches = 40 slots max.
- Prune expired points BEFORE render, not during. On each timer tick: advance head pointer past expired entries.
- Cap points per user. Hard limit (e.g., 200 points). Drop oldest beyond cap.
- When laser deactivated (pointer released), start a "drain" timer. Once all points have faded, remove the trail state entirely and stop the render timer.

**Warning signs:**
- Memory/CPU usage grows linearly with laser usage duration
- Render time increases the longer laser is held
- No cleanup after laser deactivated

**Phase to address:** Data structure design — before implementing trail storage.

---

### Pitfall 5: SKPaint/SKPath Object Allocation Per Frame

**What goes wrong:** Laser trail rendering creates `new SKPaint()`, `new SKPath()`, possibly `SKPathEffect.CreateDash()` on every render frame. At 30-60 FPS, that's 30-60 native allocations per second per laser user. SkiaSharp objects wrap native Skia handles — each allocation = native malloc + GC tracking.

**Why it happens:** Existing code (e.g., `CollaboratorOverlayHelper.DrawRemoteStrokePreview`) allocates `SKPaint` per draw call with `using`. Fine for 1-2 calls per frame. Catastrophic at 30-60 FPS continuous animation.

**How to avoid:**
- **Cache `SKPaint` objects** per laser color. Create once, reuse across frames. Only recreate if color changes.
- Cache `SKPath` and call `path.Reset()` + `path.Rewind()` instead of allocating new. Or use `SKPath` pool.
- For fading segments: use `paint.Color = color.WithAlpha(alpha)` to update alpha without recreating the paint.
- Profile: verify with `dotnet-counters` that Gen0 GC rate stays stable during laser animation.

**Warning signs:**
- GC pauses visible as micro-stutters in trail animation
- `SKPaint` finalizer queue growing (check with diagnostics)
- `dotnet-counters` shows elevated Gen0 collection rate during laser

**Phase to address:** Rendering implementation phase.

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| **Invalidating entire BoardCanvas for laser fade** | FPS drops, all elements re-rendered 30-60x/sec | Separate laser overlay control/layer | >50 elements on board, any zoom level |
| **Full board clone on laser op** | GC pressure, UI thread stalls | Bypass `ApplyOperationCore` for laser ops | Immediately — first laser activation while hosting |
| **New SKPaint/SKPath per render frame** | Micro-stutters, elevated GC | Cache paint objects, reuse/reset path | Sustained laser use >5 seconds |
| **Trail smoothing on full point buffer** | CPU spike per frame | Smooth only visible/recent segment, not full history | >100 trail points |
| **60 Hz network send rate for laser** | Bandwidth saturation with 4+ users, dropped packets | Batch points at 50ms intervals (~20Hz) | 3+ simultaneous laser users on typical LAN |
| **RemoteLaserState dictionary copy-on-write** | GC allocation per inbound laser update | Use mutable dictionary + property notification, or single shared instance with generation counter | High-frequency updates from multiple peers |
| **DispatcherTimer at 16ms for fade animation** | Timer drift, inconsistent frame timing | Use `Stopwatch`-based elapsed time for fade calculation, not tick count | Variable system load |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| **Trail visible only to remote peers, not local user** | "Is my laser even on?" — no visual feedback | Show trail locally too. Local trail renders from local point buffer (no network roundtrip). |
| **No visual feedback on press-and-hold activation** | User presses, nothing happens for 200ms, confused | Show dot immediately on pointer down. Trail accumulates as pointer moves. Zero delay. |
| **Laser dot size doesn't scale with zoom** | Dot invisible when zoomed out, huge when zoomed in | Render dot and trail in **screen space** (after zoom transform), not board space. Divide by zoom. |
| **Trail opacity drops linearly** | Trail looks "same brightness then suddenly gone" — unnatural | Use ease-out curve: `alpha = (1 - t)^2` or cubic. Tail fades faster, head stays bright. |
| **Remote laser color same as cursor color** | Can't distinguish who's pointing what | Laser should use the peer's assigned color (already in `PlayerInfo.AssignedColor`), but with higher saturation/brightness than cursor |
| **No indication WHOSE laser it is** | "Who's pointing at that?" | Show small name label near the laser dot (like `DrawRemoteCursor` does) |
| **Laser trail persists after disconnect** | Ghost trail from disconnected peer stays on screen | Clean up laser state in `PeerLeft` handler — same pattern as `RemoveRemoteCursor` |
| **Laser works during pan/zoom (Hand tool)** | User accidentally triggers laser while navigating | Laser only active when LaserPointer tool is selected AND pointer is pressed. Hand tool = no laser. |
| **Trail in board coordinates jitters during pan** | Trail "slides" as board pans | Store trail points in board coordinates, render after pan/zoom transform (inside `canvas.Save/Translate/Scale` block) |

---

## Network Gotchas

| Gotcha | Risk | Prevention |
|--------|------|------------|
| **Laser op routed through undo/redo stack** | "Undo" undoes someone's laser pointer (?!) | Laser ops must not push to `UndoManager`. Explicitly exclude in `ApplyOperationCore` or bypass entirely. |
| **Laser op included in `FullSyncResponse`** | New joiner gets stale laser trails from before they connected | Laser state is ephemeral — never included in full sync. New peers start with empty laser state. |
| **Host validates laser op against board elements** | Laser op rejected because `_boardElements` doesn't contain a laser "element" | Laser op validation should be trivial: check `SenderId` is valid peer, done. No element lookup. |
| **`_dataWriter` shared instance for laser sends** | Known concern: `NetDataWriter` not thread-safe (CONCERNS.md line 106-110). Laser sends at high frequency increase collision risk. | Laser sends happen on UI thread via poll timer (same as cursor updates), so currently safe. But document this constraint. |
| **Network poll rate (50ms) throttles laser updates** | Inbound laser updates processed at 20Hz max, causing choppy remote trails | This is actually fine for laser. 20Hz with batched points (2-3 points per batch) = smooth enough. Don't "fix" by increasing poll rate — it affects all network traffic. |
| **MessagePack serialization overhead per laser packet** | Each laser update serializes/deserializes through `OperationSerializer` | Keep laser op payload minimal: `{ SenderId, Position, IsActive }`. No unnecessary fields. Consider whether laser needs full `BoardOperation` base class or a lighter-weight message. |
| **Laser state not cleaned up when tool switches** | User switches from laser to pen — remote peers still see laser dot | Send explicit "laser off" message when tool changes or pointer exits canvas. Don't rely on timeout alone. |

---

## "Looks Done But Isn't" Checklist

- [ ] **Local trail renders without network roundtrip** — laser must feel instant locally even if network is slow
- [ ] **Trail fade uses wall-clock time, not frame count** — fade rate must be consistent regardless of FPS/timer drift
- [ ] **Ring buffer has fixed capacity** — verified no unbounded growth during 5-min sustained use
- [ ] **Laser rendering is isolated from board rendering** — verified `BoardDrawOperation` not called at animation frequency
- [ ] **Laser op doesn't trigger `SyncBoardFromHost()`** — verified no full board clone on laser activity
- [ ] **Laser op doesn't push to undo stack** — verified "undo" doesn't affect laser
- [ ] **Laser state cleaned up on peer disconnect** — verified ghost trails don't persist
- [ ] **Laser state cleaned up on tool switch** — verified remote dot disappears when user switches tools
- [ ] **Laser dot size independent of zoom** — verified dot visible at 10% zoom and not giant at 400% zoom
- [ ] **Multiple simultaneous lasers render correctly** — verified 3+ users using laser simultaneously
- [ ] **Laser works for host AND clients** — both code paths tested (host has `SyncBoardFromHost` path, client doesn't)
- [ ] **SKPaint objects cached, not allocated per frame** — verified with profiler during 10s continuous laser use
- [ ] **Fade animation timer stops when no active trails** — verified CPU returns to baseline after all lasers deactivated
- [ ] **Trail points batched for network send** — verified not sending 60 individual packets/sec

---

## Codebase-Specific Risks

These pitfalls are amplified by known concerns from CONCERNS.md:

| Known Concern | How Laser Makes It Worse | Mitigation |
|---------------|--------------------------|------------|
| `BoardView.axaml.cs` god file (886 lines) | Adding laser pointer event handling here adds more complexity | Implement laser handler as separate class/behavior; BoardView delegates to it |
| `MainViewModel.cs` god ViewModel (1186 lines) | Laser state management (local + remote) adds more fields/methods | Create `LaserPresenceManager` class; ViewModel delegates laser concerns to it |
| Full board clone per host operation | If laser accidentally enters this path, catastrophic perf | **Gate this in code review**: laser op handler must NOT call `SyncBoardFromHost()` |
| Linear element lookups | N/A — laser doesn't interact with elements | No action needed |
| No stroke point batching | Laser has same problem if point-per-packet | Batch from day one; don't copy the stroke-point anti-pattern |
| Elements list rebuilt on every mutation | If laser accidentally enters `ApplyOperationCore`, triggers this | Same gate as board clone: laser must bypass element pipeline |

---

## Sources

- **Codebase analysis:** `BoardCanvas.cs`, `BoardOperation.cs`, `GameHost.cs`, `MainViewModel.cs`, `CollaboratorOverlayHelper.cs`, `CollaboratorPresenceModels.cs` — direct inspection
- **LiteNetLib delivery methods:** Context7 `/revenantx/litenetlib` — `Sequenced` vs `Unreliable` vs `ReliableOrdered` semantics confirmed
- **SkiaSharp rendering:** Context7 `/mono/skiasharp` — `SKPaint`, `SKPath` allocation patterns
- **Avalonia rendering:** Context7 `/avaloniaui/avalonia-docs` — `CompositionCustomVisualHandler`, `InvalidateVisual()`, `ICustomDrawOperation` patterns, `AffectsRender`
- **Known tech debt:** `.planning/codebase/CONCERNS.md` — full board clone, god files, shared `NetDataWriter`, no stroke batching
