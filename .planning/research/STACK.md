# Stack Research: Laser Pointer Tool

**Domain:** Collaborative whiteboard — laser pointer with fading trail
**Researched:** 2026-04-15
**Confidence:** HIGH

## Context

Existing stack: .NET 9, Avalonia 11.3, SkiaSharp 3.x, LiteNetLib 2.1, MessagePack 2.5. No new dependencies needed. This research covers _patterns and APIs within the existing stack_ for implementing a laser pointer with:
- Dot at cursor position
- Fading trail (~1-2 seconds)
- Press-and-hold activation
- Real-time broadcast to all peers
- Configurable color per user

---

## Recommended Stack (additions to existing)

### Core Technologies — No New Packages

| Technology | Already In Stack | Purpose for Laser | Why Reuse |
|---|---|---|---|
| SkiaSharp `SKPaint` + `SKPath` | SkiaSharp 3.116.1 | Trail rendering with per-segment alpha | Already renders all canvas content |
| `DispatcherTimer` | Avalonia 11.3 | Drive fade animation + cleanup at 60Hz | Already used for network polling (50ms) and autosave |
| MessagePack `[Union]` | MessagePack 2.5 | Serialize `LaserPointerOperation` | Existing `BoardOperation` union pattern |
| LiteNetLib `Sequenced` delivery | LiteNetLib 2.1 | Broadcast laser position — drops stale packets, delivers latest | Better than `Unreliable` for position data |

### Supporting Patterns

| Pattern | Purpose | When to Use |
|---|---|---|
| Circular buffer (`LaserTrailPoint[]`) | Fixed-size trail point storage | Always — replaces dynamic `List<T>` growth |
| Time-stamped points | Drive alpha fade per-point | Each point gets `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` |
| Separate draw pass (ephemeral overlay) | Laser never in element list | Render after elements, before cursors |
| Snapshot-on-UI-thread | Thread-safe render data | Existing `BoardDrawOperation` pattern — snapshot laser state in constructor |

---

## Rendering Approach

### Strategy: Per-Segment Alpha via Individual `DrawLine` Calls

**NOT** a single `SKPath` with one paint — that gives uniform alpha across the entire trail. Instead, draw each segment individually with computed alpha.

```csharp
// Pseudocode for rendering laser trail
var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
var fadeDurationMs = 1500L; // 1.5 seconds

for (int i = 1; i < trailPoints.Length; i++)
{
    var point = trailPoints[i];
    var age = now - point.TimestampMs;
    if (age >= fadeDurationMs) continue;

    var alpha = (byte)(255 * (1.0 - (double)age / fadeDurationMs));
    
    using var paint = new SKPaint
    {
        Color = laserColor.WithAlpha(alpha),
        StrokeWidth = 3f,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke
    };
    
    canvas.DrawLine(
        trailPoints[i - 1].Position.X, trailPoints[i - 1].Position.Y,
        point.Position.X, point.Position.Y,
        paint);
}

// Draw dot at current position (full alpha, slightly larger)
using var dotPaint = new SKPaint
{
    Color = laserColor,
    IsAntialias = true,
    Style = SKPaintStyle.Fill
};
canvas.DrawCircle(currentPos.X, currentPos.Y, 5f, dotPaint);
```

### Key SkiaSharp APIs

| API | Usage | Why |
|---|---|---|
| `SKPaint.Color` with `SKColor.WithAlpha(byte)` | Per-segment fade alpha | Simple, direct alpha control per draw call |
| `SKCanvas.DrawLine(x1, y1, x2, y2, paint)` | Individual trail segments | Each gets own alpha — unlike `DrawPath` which is uniform |
| `SKCanvas.DrawCircle(x, y, r, paint)` | Laser dot at current position | Existing pattern (eraser preview, cursors) |
| `SKPaint.StrokeCap = Round` | Smooth trail ends | Prevents jagged line endings |
| `SKPaint.IsAntialias = true` | Smooth edges | Required for thin lines |

### Performance Optimization: Paint Reuse

Creating `SKPaint` per segment is expensive. Optimization: create ONE paint, mutate `.Color` between draw calls:

```csharp
using var paint = new SKPaint
{
    StrokeWidth = 3f,
    StrokeCap = SKStrokeCap.Round,
    StrokeJoin = SKStrokeJoin.Round,
    IsAntialias = true,
    Style = SKPaintStyle.Stroke
};

for (int i = 1; i < trailCount; i++)
{
    var alpha = ComputeAlpha(trailPoints[i].TimestampMs, now, fadeDurationMs);
    if (alpha <= 0) continue;
    paint.Color = laserColor.WithAlpha((byte)alpha);
    canvas.DrawLine(prev.X, prev.Y, curr.X, curr.Y, paint);
}
```

Single `SKPaint` allocation. Mutating `.Color` is cheap — no native handle recreation.

### Render Integration Point

Add laser rendering in `BoardDrawOperation.Render()` AFTER elements, BEFORE remote cursors:

```
elements → selection overlay → eraser preview → ★ LASER TRAILS ★ → remote stroke previews → remote cursors
```

Laser renders on top of board content but under cursor labels. This matches visual hierarchy — laser is "pointing at" content.

---

## Animation / Timing

### Strategy: `DispatcherTimer` at ~16ms (60 FPS) During Active Laser

**Why not `RequestAnimationFrame`?** It exists in Avalonia but requires `TopLevel` reference and recursive self-scheduling. `DispatcherTimer` is simpler, already used in codebase (network polling, autosave), and runs on UI thread.

**Why not always-on timer?** Waste. Only tick when ANY laser is active (local or remote).

```csharp
// Start timer when laser activates (local or first remote laser appears)
_laserFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
_laserFadeTimer.Tick += (_, _) =>
{
    PruneExpiredPoints();  // Remove points older than fadeDuration
    InvalidateVisual();    // Trigger re-render with updated alphas
    
    if (NoActiveLasers())
        _laserFadeTimer.Stop();
};
_laserFadeTimer.Start();
```

### Timer Lifecycle

| Event | Action |
|---|---|
| Local pointer pressed OR first remote laser received | Start timer if not running |
| Local pointer released AND no remote lasers AND all trails expired | Stop timer |
| Each tick | Prune expired points, `InvalidateVisual()` |

### Why 16ms?

- 60 FPS = smooth fade animation
- Existing network poll is 50ms (20 FPS) — too choppy for visual fade
- ~16ms matches typical display refresh
- Only runs during active laser — zero cost when idle

---

## Network Protocol

### New Operation: `LaserPointerOperation`

Follows existing `CursorUpdateOperation` pattern exactly. Ephemeral, never persisted, broadcast to all peers.

```csharp
// New OperationType enum value
LaserPointer = 13

// New operation class
[MessagePackObject]
public class LaserPointerOperation : BoardOperation
{
    public override OperationType Type => OperationType.LaserPointer;

    /// <summary>
    /// Current laser position in board coordinates.
    /// </summary>
    [Key(3)]
    public Vector2 Position { get; set; }

    /// <summary>
    /// True = laser active (pointer down), False = laser deactivated (pointer up).
    /// </summary>
    [Key(4)]
    public bool IsActive { get; set; }

    /// <summary>
    /// Laser color chosen by this user.
    /// </summary>
    [Key(5)]
    public SKColor Color { get; set; }
}
```

### Wire Size

Approximately: 1 (union key) + 16 (SenderId Guid) + 8 (Timestamp) + 8 (Vector2) + 1 (bool) + 4 (SKColor) = ~38 bytes before MessagePack overhead. Fits in single UDP packet trivially.

### Delivery Method: `Sequenced` on Dedicated Channel

| Delivery | Behavior | Why For Laser |
|---|---|---|
| ~~`Unreliable`~~ | Any order, may drop | Old positions could arrive after new ones → jitter |
| **`Sequenced`** | **Only latest delivered, old dropped** | **Perfect — we only care about newest position** |
| ~~`ReliableOrdered`~~ | Guaranteed, ordered | Overkill, adds latency for ephemeral data |

Use `DeliveryMethod.Sequenced` on channel 1 (existing unreliable channel). Same channel as `CursorUpdate` — both are ephemeral position data.

**Alternative:** Add channel 2 to separate laser from cursor updates. Currently `ChannelsCount = 2` (channels 0-1). Bumping to 3 gives laser its own sequenced stream. Marginal benefit — only worth it if cursor and laser fight for bandwidth (unlikely with <10 peers).

**Recommendation:** Start with channel 1 + `Sequenced`. Upgrade to channel 2 only if issues arise.

### Send Rate

Match cursor update rate (~60 updates/sec during pointer move). No throttling needed — `Sequenced` delivery drops stale packets automatically.

### Host Relay Pattern

`IsOperationReliable()` must return `false` for `LaserPointerOperation` — same as `CursorUpdateOperation`:

```csharp
private static bool IsOperationReliable(BoardOperation operation)
{
    return operation is not CursorUpdateOperation
        and not LaserPointerOperation;
}
```

### Deactivation Signal

When pointer released, send one final `LaserPointerOperation` with `IsActive = false`. This lets remote peers know to stop expecting updates. Trail fades naturally from last known points.

---

## Point Buffer Strategy

### Data Structure: Fixed-Size Array as Circular Buffer

```csharp
public struct LaserTrailPoint
{
    public Vector2 Position;   // Board coordinates
    public long TimestampMs;   // DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
}

public sealed class LaserTrailBuffer
{
    private readonly LaserTrailPoint[] _points;
    private int _head;  // Next write index
    private int _count; // Current number of valid points
    
    public LaserTrailBuffer(int capacity = 128) 
    {
        _points = new LaserTrailPoint[capacity];
    }
    
    public void Add(Vector2 position, long timestampMs) { ... }
    
    // Enumerate valid (non-expired) points in chronological order
    public ReadOnlySpan<LaserTrailPoint> GetActivePoints(long nowMs, long fadeDurationMs) { ... }
    
    public void Clear() { ... }
}
```

### Why Circular Buffer?

| Approach | Problem | 
|---|---|
| `List<LaserTrailPoint>` | Grows unbounded, GC pressure from resizing, `RemoveAt(0)` is O(n) |
| `Queue<LaserTrailPoint>` | Dequeue is fine but no span/index access for rendering |
| `LinkedList<LaserTrailPoint>` | GC pressure from node allocations, poor cache locality |
| **Fixed array + head/count** | **Zero allocation, O(1) add, cache-friendly, predictable memory** |

### Capacity: 128 Points

At 60 updates/sec and 1.5s fade = 90 points max during continuous movement. 128 gives headroom. Power-of-2 enables `& (capacity - 1)` modulo optimization.

### Per-Peer Storage

```csharp
// In Canvas layer — manages all laser trails
public sealed class LaserPointerState
{
    // Key: peer ClientId (Guid)
    // Value: their trail buffer + active flag + color
    private readonly Dictionary<Guid, PeerLaserState> _peers = new();
}

public sealed class PeerLaserState
{
    public LaserTrailBuffer Trail { get; } = new(128);
    public bool IsActive { get; set; }
    public SKColor Color { get; set; }
}
```

### Snapshot for Render Thread

Existing pattern: `BoardDrawOperation` constructor snapshots state on UI thread. Laser data must be snapshotted too:

```csharp
// In BoardDrawOperation constructor (UI thread):
_laserTrails = owner.LaserPointerState?.Snapshot();

// Snapshot creates immutable copy of active trail points
public sealed record LaserTrailSnapshot(
    Guid ClientId,
    SKColor Color,
    IReadOnlyList<LaserTrailPoint> Points);  // Only non-expired points
```

---

## What NOT to Use

| Avoid | Why | Use Instead |
|---|---|---|
| `SKPath` with single `DrawPath()` for trail | Uniform alpha across entire path — no per-segment fade | Individual `DrawLine()` per segment with computed alpha |
| `SKShader.CreateLinearGradient` on trail path | Complex to map gradient along arbitrary polyline, doesn't handle curves | Per-segment alpha calculation from timestamp |
| `ReliableOrdered` for laser network messages | Unnecessary reliability for ephemeral position data, adds latency | `Sequenced` — only latest position matters |
| `List<T>.RemoveAt(0)` for trail point cleanup | O(n) shift on every prune tick | Circular buffer with head pointer — O(1) |
| Always-on fade timer | Wastes CPU when no laser active | Start/stop timer based on active laser state |
| `RequestAnimationFrame` | Requires `TopLevel` ref, recursive scheduling, less familiar pattern in codebase | `DispatcherTimer(16ms)` — consistent with existing patterns |
| Persisting laser state in `BoardState` | Laser is ephemeral — adds serialization bloat, stale data on reload | Separate `LaserPointerState` object, never serialized |
| New `BoardElement` subtype for laser | Laser is not an element — shouldn't be selectable, undoable, or in z-order | Ephemeral overlay rendered separately |
| `SKMaskFilter.CreateBlur()` for glow effect | GPU-heavy, inconsistent across platforms, overkill for simple pointer | Two-pass drawing: wider semi-transparent stroke + narrow opaque stroke |
| `System.Threading.Timer` for fade | Not on UI thread — requires `Dispatcher.Invoke` marshaling | `DispatcherTimer` runs on UI thread natively |

---

## Optional Enhancement: Glow Effect

Simple two-pass trick — no shaders or filters needed:

```csharp
// Pass 1: Wide semi-transparent "glow" stroke
paint.StrokeWidth = 8f;
paint.Color = color.WithAlpha((byte)(alpha * 0.3));
canvas.DrawLine(x1, y1, x2, y2, paint);

// Pass 2: Narrow bright core
paint.StrokeWidth = 2f;
paint.Color = color.WithAlpha((byte)alpha);
canvas.DrawLine(x1, y1, x2, y2, paint);
```

Cheap, looks good, no platform-specific shader issues.

---

## Architecture Placement

| Component | Layer | Responsibility |
|---|---|---|
| `LaserPointerOperation` | BFGA.Network | MessagePack operation, union key 13 |
| `OperationType.LaserPointer` | BFGA.Network | Enum value |
| `LaserTrailBuffer` | BFGA.Canvas | Circular buffer for trail points |
| `LaserPointerState` | BFGA.Canvas | Per-peer laser management |
| `LaserTrailRenderer` | BFGA.Canvas/Rendering | Static `Draw()` method — follows `CollaboratorOverlayHelper` pattern |
| Laser tool handler | BFGA.Canvas/Tools | Pointer event → LaserPointerOperation |
| Timer + wiring | BFGA.App (ViewModel/View) | Owns DispatcherTimer, wires network ↔ canvas state |

Follows existing 4-layer architecture exactly. No cross-layer violations.

---

## Sources

- **Context7 /mono/skiasharp**: SKPaint, SKPath, SKCanvas API surface — confirmed `DrawLine`, `SKColor.WithAlpha`, paint property mutation (HIGH confidence)
- **Context7 /revenantx/litenetlib**: `DeliveryMethod.Sequenced` confirmed — drops old packets, delivers latest only. Channel support 0-63 confirmed (HIGH confidence)
- **Context7 /avaloniaui/avalonia-docs**: `DispatcherTimer` for periodic UI updates, `InvalidateVisual()` for manual render trigger, `RequestAnimationFrame` API exists but unnecessary here (HIGH confidence)
- **Existing codebase**: `CursorUpdateOperation` on channel 1 unreliable — exact precedent for laser network pattern. `BoardDrawOperation` snapshot pattern. `CollaboratorOverlayHelper` rendering pattern. (HIGH confidence — direct code inspection)
