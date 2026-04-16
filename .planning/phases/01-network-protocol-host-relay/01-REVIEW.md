---
phase: 01-network-protocol-host-relay
reviewed: 2026-04-15T12:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - src/BFGA.Network/Protocol/BoardOperation.cs
  - src/BFGA.Network/GameHost.cs
  - src/BFGA.Network/GameClient.cs
  - tests/BFGA.Network.Tests/ProtocolTests.cs
  - tests/BFGA.Network.Tests/NetworkTests.cs
findings:
  critical: 1
  warning: 4
  info: 2
  total: 7
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-04-15T12:00:00Z
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Network protocol layer, host/client implementations, and tests reviewed. One critical bug: `UpdateElementOperation.ModifiedProperties` uses `Dictionary<string, object>` which breaks type fidelity after MessagePack roundtrip — undo inverse values become wrong types. Four warnings: silent failure on unknown property keys in undo path, empty catch block, unreachable throw after TCS exception, missing test assertion. Two info items: unused field, unconditional accept in client listener.

## Critical Issues

### CR-01: UpdateElement undo inverse corrupted by MessagePack type erasure

**File:** `src/BFGA.Network/GameHost.cs:487-498`
**Issue:** `GetElementProperty` captures current property values (e.g., `Vector2`, `float`) into `Dictionary<string, object>` for the undo inverse. But `UpdateElementOperation.ModifiedProperties` is `Dictionary<string, object>` — after MessagePack serialize/deserialize roundtrip, `Vector2` becomes `object[]` and `float` may become `double`. When undo replays the inverse `UpdateElementOperation`, `ApplyModifiedProperties` pattern-matches on concrete types (`is Vector2 pos`, `is float f`), so deserialized inverse values silently fail to apply. **Undo of property updates over network is broken.**

The test at `ProtocolTests.cs:79` already documents this: `Vector2` roundtrips as `object[]`, confirming the type erasure.

**Fix:** Use strongly-typed property bags instead of `Dictionary<string, object>`, or add type conversion in `ApplyModifiedProperties` to handle the deserialized representations:
```csharp
// In ApplyModifiedProperties, handle Vector2 deserialized as object[]:
case "Position":
    if (kvp.Value is Vector2 pos)
        element.Position = pos;
    else if (kvp.Value is object[] arr && arr.Length == 2)
        element.Position = new Vector2(Convert.ToSingle(arr[0]), Convert.ToSingle(arr[1]));
    break;
```
This pattern would need to be applied for all Vector2 and numeric properties. A cleaner fix: introduce a `PropertyChange` record with explicit type tags.

## Warnings

### WR-01: GetElementProperty returns meaningless value for unknown keys

**File:** `src/BFGA.Network/GameHost.cs:568`
**Issue:** Default case returns `new object()`. If a caller passes an unrecognized property key in `ModifiedProperties`, the undo inverse silently stores a meaningless sentinel. Undo would then "restore" the property to `new object()`, which `ApplyModifiedProperties` silently ignores (no matching pattern). Data loss: the original property value is lost from the undo stack.
**Fix:**
```csharp
_ => throw new ArgumentException($"Unknown element property key: '{key}' for element type {element.GetType().Name}")
```

### WR-02: Empty catch block swallows connection data read errors

**File:** `src/BFGA.Network/GameHost.cs:730-732`
**Issue:** `OnConnectionRequest` catches all exceptions when reading display name from connection data. Malformed connection data (e.g., from a non-BFGA client) is silently accepted. Should at minimum log.
**Fix:**
```csharp
catch (Exception ex)
{
    Debug.WriteLine($"[Host] Failed to read display name from connection request: {ex.Message}");
}
```

### WR-03: Double error reporting in ConnectAsync

**File:** `src/BFGA.Network/GameClient.cs:109-112`
**Issue:** When `_netManager.Start()` fails, exception is set on `_connectCompletionSource` (line 109), then `_connectCompletionSource` is nulled (line 110), then `throw` on line 112. Callers who `await ConnectAsync()` will get the thrown exception (not the TCS one). The TCS exception is set but immediately orphaned since the TCS is cleared. Inconsistent error paths.
**Fix:** Remove the throw — let the TCS be the sole error channel:
```csharp
if (!_netManager.Start())
{
    var ex = new InvalidOperationException("Failed to start network client");
    _connectCompletionSource.TrySetException(ex);
    var task = _connectCompletionSource.Task;
    _connectCompletionSource = null;
    CleanupNetworkState();
    return task;
}
```

### WR-04: Test missing assertion — vacuous test

**File:** `tests/BFGA.Network.Tests/NetworkTests.cs:316-332`
**Issue:** `GameHost_HandleLaserOp_FiresOperationReceivedEvent` — comment on line 328 says OperationReceived should NOT fire for local ops, but there's no `Assert.Null(receivedOp)` to verify. Test always passes regardless of behavior.
**Fix:**
```csharp
// After TryApplyLocalOperation:
Assert.Null(receivedOp); // OperationReceived should NOT fire for local ops
```

## Info

### IN-01: Unused field in ProtocolTests

**File:** `tests/BFGA.Network.Tests/ProtocolTests.cs:12-13`
**Issue:** `_serialized` field initialized to `Array.Empty<byte>()` in constructor but never referenced in any test.
**Fix:** Remove the field and simplify constructor.

### IN-02: Client listener unconditionally accepts connection requests

**File:** `src/BFGA.Network/GameClient.cs:283-285`
**Issue:** `ClientEventListener.OnConnectionRequest` calls `request.Accept()` on all incoming requests. Clients shouldn't receive connection requests (they're not servers), but if they somehow do, they accept unconditionally. Low risk since LiteNetLib won't route requests to clients normally.
**Fix:** Consider rejecting instead:
```csharp
public void OnConnectionRequest(ConnectionRequest request)
{
    request.Reject(); // Clients don't accept incoming connections
}
```

---

_Reviewed: 2026-04-15T12:00:00Z_
_Reviewer: OpenCode (gsd-code-reviewer)_
_Depth: standard_
