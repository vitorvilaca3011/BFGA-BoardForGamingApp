# Technical Concerns

**Analysis Date:** 2026-04-15

## Technical Debt

### Duplicated Property Dispatch Logic
- Issue: `ApplyModifiedProperties` in `GameHost` and `ApplyUpdateElement` in `MainViewModel` are near-identical 60+ line switch blocks that map string property names to element properties via `Dictionary<string, object>`.
- Files: `src/BFGA.Network/GameHost.cs` (lines 535–598), `src/BFGA.App/ViewModels/MainViewModel.cs` (lines 963–1016)
- Impact: Adding a new element property requires updating both locations. Missing one causes silent data loss during sync.
- Fix approach: Extract a shared `BoardElement.ApplyProperties(Dictionary<string, object>)` method in `BFGA.Core` and call it from both sites. Similarly, `GetElementProperty` in `GameHost.cs` (lines 510–533) is the mirror read-side.

### Stringly-Typed Property Updates
- Issue: `UpdateElementOperation.ModifiedProperties` is `Dictionary<string, object>`. Property names are magic strings scattered across `BoardView.axaml.cs`, `GameHost.cs`, and `MainViewModel.cs`. No compile-time safety.
- Files: `src/BFGA.Network/Protocol/BoardOperation.cs` (line 112), every call site using `UpdateElementOperation`
- Impact: Typo in a property key = silent failure. New properties require updating string literals in 3+ files.
- Fix approach: Introduce a `PropertyKey` enum or strongly-typed property bag. Or use `nameof()` constants in a shared class.

### `BoardView.axaml.cs` God File (886 lines)
- Issue: This code-behind handles pointer events, inline text editing, image import/paste, clipboard, zoom control, tool synchronization, and selection overlay — all in one file with no separation.
- Files: `src/BFGA.App/Views/BoardView.axaml.cs`
- Impact: Hard to test in isolation, high coupling to ViewModel internals. Any change risks regressions in unrelated features.
- Fix approach: Extract `InlineTextEditBehavior`, `ImageImportBehavior`, and `ZoomBehavior` into separate classes or attached behaviors.

### `MainViewModel.cs` God ViewModel (1186 lines)
- Issue: Handles connection lifecycle, board state management, operation dispatch, roster management, remote cursor/stroke tracking, autosave, undo/redo shadow counts, settings, and polling — all in one class.
- Files: `src/BFGA.App/ViewModels/MainViewModel.cs`
- Impact: Difficult to reason about state transitions. Methods like `ApplyInboundOperation` modify multiple private fields simultaneously.
- Fix approach: Split into `ConnectionManager`, `BoardStateManager`, `CollaboratorPresenceManager`, and keep `MainViewModel` as a thin coordinator.

### No TODO/FIXME Comments
- Zero `TODO`, `FIXME`, or `HACK` comments found anywhere in the codebase. This is unusual and suggests undocumented debt — the `NOTE:` comments in `GameHost.cs` (lines 70–72 and 130–133) acknowledge design shortcuts for "v0.1" without using standard markers.

## Security Concerns

### All Connection Requests Unconditionally Accepted
- Risk: Both `GameHost` and `GameClient` accept every connection request with `request.Accept()` — no authentication, no connection key validation, no rate limiting.
- Files: `src/BFGA.Network/GameHost.cs` (line 700), `src/BFGA.Network/GameClient.cs` (line 272)
- Current mitigation: LAN-only use case assumption. MessagePack uses `UntrustedData` security mode.
- Recommendations: Add a shared secret/room code that the host generates and clients must provide. Add a max-player limit. The display name is read from untrusted connection data without sanitization (line 688).

### No Input Validation on Display Names
- Risk: Player display names are read from network data and stored/displayed without length limits or sanitization. Malicious names could cause UI issues or consume memory.
- Files: `src/BFGA.Network/GameHost.cs` (lines 681–696)
- Current mitigation: None — bare `catch` swallows parsing errors.
- Recommendations: Truncate display names to a reasonable length (e.g. 32 chars). Strip control characters.

### No Size Limits on Image Data
- Risk: `ImageElement.ImageData` is `byte[]` with no maximum size. A malicious client can send arbitrarily large image data via `AddElementOperation`, consuming host memory and propagating to all clients via broadcast.
- Files: `src/BFGA.Core/Models/ImageElement.cs`, `src/BFGA.Network/GameHost.cs` (operation handling)
- Current mitigation: None.
- Recommendations: Validate `ImageData.Length` in `ValidateOperation`. Set a reasonable max (e.g. 10 MB). Reject oversized elements.

### No Board File Integrity Verification
- Risk: `BoardFileStore.LoadAsync` deserializes MessagePack from any file without integrity checks. A tampered `.bfga` file could cause deserialization crashes or malformed state.
- Files: `src/BFGA.Core/BoardFileStore.cs`
- Current mitigation: Exception wrapping catches most deserialization errors.
- Recommendations: Add a file header with version and optional checksum.

## Performance Concerns

### Linear Element Lookups in Hot Paths
- Issue: `Board.Elements` is `List<BoardElement>`. Many operations use `.FirstOrDefault(e => e.Id == id)` which is O(n) per lookup. In `GameHost`, `_boardElements` dictionary provides O(1) lookup, but `BoardToolController` and `MainViewModel` use the raw list.
- Files: `src/BFGA.Canvas/Tools/BoardToolController.cs` (lines 92, 209, 455, 600), `src/BFGA.App/ViewModels/MainViewModel.cs` (lines 952, 965, 1020, 1031)
- Impact: With hundreds of elements, selection, deletion, and move operations degrade linearly.
- Fix approach: Maintain a parallel `Dictionary<Guid, BoardElement>` in `BoardToolController` or add an index to `BoardState`.

### Elements List Rebuilt on Every Mutation
- Issue: `GameHost.ApplyOperationCore` calls `_boardState.Elements = _boardElements.Values.ToList()` after every single operation. This allocates a new list on every add/update/delete/move.
- Files: `src/BFGA.Network/GameHost.cs` (line 495)
- Impact: Frequent GC pressure under rapid drawing (many stroke points → each `AddElement` triggers a ToList).
- Fix approach: Use `_boardElements.Values` directly (backed by a stable collection) or batch updates.

### Full Board Clone on Every Host Operation
- Issue: `MainViewModel.SyncBoardFromHost()` calls `CloneBoardState()` which serializes+deserializes the entire board via MessagePack. Called after every local operation when hosting.
- Files: `src/BFGA.App/ViewModels/MainViewModel.cs` (lines 821–827, 1051–1056)
- Impact: With many elements (especially large images), each operation triggers full serialization round-trip. This blocks the UI thread.
- Fix approach: Use incremental state updates instead of full clones. Or only clone on significant state transitions.

### No Stroke Point Batching
- Issue: Each individual stroke point generates a `DrawStrokePointOperation` sent over the network at ~60 Hz. No batching or throttling.
- Files: `src/BFGA.Network/Protocol/BoardOperation.cs` (class `DrawStrokePointOperation`)
- Impact: High network traffic during active drawing with multiple users.
- Fix approach: Batch stroke points (e.g. every 50ms or every 5 points).

### Image Data in Network Broadcast
- Issue: `ImageElement` embeds raw `byte[]` image data. When broadcast via `AddElementOperation`, the entire image is serialized and sent to every client. `FullSyncResponseOperation` includes all images in the board.
- Files: `src/BFGA.Core/Models/ImageElement.cs`, `src/BFGA.Network/Protocol/BoardOperation.cs`
- Impact: Large images cause massive network payloads. A board with 5 images of 5 MB each = 25 MB full sync.
- Fix approach: Separate image data transfer from element metadata. Use chunked transfer or content-addressed storage.

## Maintainability Concerns

### Mutable Shared Board State
- Issue: `GameHost.BoardState` returns a direct reference to internal state (documented in comments, lines 130–133). `BoardToolController` directly mutates `Board.Elements` (e.g., `Board.Elements.Add()` in lines 176, 196, 316, 363). This makes it impossible to reason about who owns and modifies state.
- Files: `src/BFGA.Network/GameHost.cs`, `src/BFGA.Canvas/Tools/BoardToolController.cs`
- Impact: Race conditions in multi-threaded scenarios. Hard to add change tracking or event sourcing later.
- Fix approach: Make `BoardState` immutable or use a builder pattern. Route all mutations through a single method.

### View Code-Behind Directly Calls ViewModel Internals
- Issue: `BoardView.axaml.cs` directly calls `mainViewModel.PublishLocalBoardOperation()`, `mainViewModel.SyncBoardFromHost()`, and accesses `mainViewModel.Host`. The view layer has deep knowledge of networking/state management.
- Files: `src/BFGA.App/Views/BoardView.axaml.cs` (lines 546, 556, 763–769, 844, 1042–1049)
- Impact: Tight coupling makes it impossible to test view behavior without the full ViewModel graph. Breaks MVVM separation.
- Fix approach: Expose a single `ICommand` or event-based interface from the ViewModel for dispatching operations. The view should never touch `Host` directly.

### `NetDataWriter` Not Thread-Safe (Acknowledged)
- Issue: `GameHost._dataWriter` is a single shared instance used for all sends. The comment (lines 70–72) acknowledges this is only safe for single-threaded UI use.
- Files: `src/BFGA.Network/GameHost.cs` (line 73)
- Impact: If polling or broadcasting ever moves off the UI thread, message corruption will occur.
- Fix approach: Use `ObjectPool<NetDataWriter>` or create a new writer per send.

### Sync-Over-Async in MainWindow
- Issue: `GetAwaiter().GetResult()` called twice in `MainWindow.cs` — once in `CloseDataContext` (line 74) and once in `OnKeyDown` for paste handling (line 130).
- Files: `src/BFGA.App/MainWindow.axaml.cs` (lines 74, 130)
- Impact: Deadlock risk on the UI thread if the awaited tasks try to dispatch back to the UI thread.
- Fix approach: Make `OnKeyDown` async or use fire-and-forget with proper error handling. `CloseDataContext` should be made async.

### Swallowed Exceptions in Settings and Network
- Issue: Multiple bare `catch` blocks that silently swallow all exceptions with only comments like "Corrupt settings — use defaults" or "Use default name if reading fails".
- Files: `src/BFGA.App/Services/SettingsService.cs` (lines 46–49, 88–91), `src/BFGA.Network/GameHost.cs` (lines 693–696), `src/BFGA.App/MainWindow.axaml.cs` (lines 63–65)
- Impact: Silent failures make debugging difficult. Corrupt settings could lead to unexpected behavior with no indication.
- Fix approach: Log to Debug.WriteLine at minimum. For settings, consider logging a warning when falling back to defaults.

## Operational Concerns

### No Health Check or Connectivity Monitoring
- Issue: No heartbeat between host and clients beyond LiteNetLib's built-in timeout. No user-visible indication when latency is high or connection is degrading.
- Files: Entire `BFGA.Network` project
- Current mitigation: LiteNetLib handles disconnect detection internally.
- Recommendations: Show ping/latency in the UI. Add a "connection quality" indicator.

### Debug.WriteLine-Only Logging
- Issue: All network error/debug logging uses `Debug.WriteLine` which is only available in debug builds and has no structured output.
- Files: `src/BFGA.Network/GameHost.cs` (lines 748, 756–758, 772), `src/BFGA.Network/GameClient.cs` (lines 154, 175, 288, 301)
- Impact: Production issues are invisible. No way to diagnose network problems in release builds.
- Recommendations: Introduce a minimal `ILogger` abstraction. The optional `BoardDebugLogger` in `BFGA.App` exists but doesn't cover network layer.

### No Graceful Reconnection
- Issue: When a client disconnects, there's no automatic reconnection attempt. The user must manually navigate back to the connection screen and reconnect.
- Files: `src/BFGA.App/ViewModels/MainViewModel.cs` (connection state management)
- Impact: Poor UX during network interruptions. Lost work if the board wasn't saved.
- Recommendations: Add auto-reconnect with exponential backoff. Maintain local board state during disconnection.

### Autosave Overwrites Without Rotation
- Issue: `HostAutosaveAsync` writes to a single fixed path based on board name. No backup rotation, no versioning. A corrupt write could destroy the only autosave.
- Files: `src/BFGA.App/ViewModels/MainViewModel.cs` (lines 1264–1306)
- Impact: Data loss if autosave coincides with a crash or file system error.
- Fix approach: Keep 2–3 rotated autosave files. `BoardFileStore.SaveAsync` uses atomic temp-file-then-rename which mitigates partial writes, but doesn't help with logical corruption.

## Test Coverage Gaps

### No Dedicated Canvas Test Project
- What's not tested: `BFGA.Canvas` project has no dedicated test project. Canvas rendering, image decode cache lifecycle, and viewport coordinate transforms are tested only indirectly through `BFGA.Core.Tests` (which references BFGA.Canvas).
- Files: `src/BFGA.Canvas/BoardCanvas.cs`, `src/BFGA.Canvas/BoardViewport.cs`, `src/BFGA.Canvas/Rendering/ImageDecodeCache.cs`
- Risk: Rendering regressions (e.g., z-order sorting, overlay drawing) go undetected.
- Priority: Medium

### Network Integration Tests Are Minimal
- What's not tested: `tests/BFGA.Network.Tests/NetworkTests.cs` (242 lines) covers basic host/client lifecycle. Missing: multi-client scenarios, large payload handling, malformed message handling, reconnection after disconnect, undo/redo across multiple users.
- Files: `tests/BFGA.Network.Tests/NetworkTests.cs`
- Risk: Multi-player edge cases (concurrent edits, ordering) are untested.
- Priority: High

### `BoardView.axaml.cs` Code-Behind Largely Untested
- What's not tested: The 886-line code-behind file handles critical interaction logic (inline text editing, image import, clipboard paste, pointer dispatch). `BoardViewPipelineTests.cs` (417 lines) covers some pointer pipeline scenarios but inline text editing, clipboard operations, and image import are not covered.
- Files: `src/BFGA.App/Views/BoardView.axaml.cs`
- Risk: Regressions in user interaction flows go undetected.
- Priority: Medium

### No Error Path Testing for Network
- What's not tested: Malformed messages, oversized payloads, rapid disconnect/reconnect, simultaneous operations on the same element.
- Files: `src/BFGA.Network/GameHost.cs`, `src/BFGA.Network/GameClient.cs`
- Risk: Crash or data corruption under adversarial or flaky network conditions.
- Priority: High

## Risk Assessment

| Area | Severity | Description |
|------|----------|-------------|
| Unconditional connection accept | High | Any network peer can join a game session with no authentication |
| No image size limit | High | Unbounded memory allocation from network-supplied image data |
| Sync-over-async in MainWindow | High | `GetAwaiter().GetResult()` on UI thread risks deadlock |
| Duplicated property dispatch | Medium | Adding element properties requires coordinated changes in 3+ files |
| `MainViewModel` complexity | Medium | 1186-line god class; state transitions hard to verify |
| Mutable shared board state | Medium | Direct reference sharing between host and UI layers |
| Linear element lookups | Medium | O(n) lookups on every pointer interaction and network operation |
| Full board clone per operation | Medium | Serialization round-trip on every local operation while hosting |
| No network layer logging in release | Medium | Production network issues are invisible |
| Missing network error path tests | Medium | Malformed/adversarial network input behavior is unverified |
| Swallowed exceptions | Low | Silent catch blocks in settings and network code |
| No autosave rotation | Low | Single autosave file with no backup history |

---

*Concerns audit: 2026-04-15*
