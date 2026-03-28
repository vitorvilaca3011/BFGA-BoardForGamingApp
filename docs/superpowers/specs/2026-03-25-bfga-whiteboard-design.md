# BFGA - Board For Gaming App: Design Specification

**Date:** 2026-03-25
**Status:** Approved

## Overview

BFGA is a collaborative whiteboard/gaming board desktop application for use among friends during gaming sessions (Minecraft modpacks, Warframe, etc.). It provides a shared infinite canvas where players can draw, drop images, and see each other's cursors in real time.

The app runs as a single Avalonia executable that can operate in either **host** or **guest** mode. One player hosts; others connect over a VPN (Tailscale, ZeroTier, or Hamachi). No cloud servers or paid hosting required.

## 1. Architecture

### High-Level Design

- **Single app, dual mode:** One executable. User picks "Host Game" or "Join Game" at startup.
- **Host-authoritative model:** The host holds the canonical board state. All mutations flow through the host, which validates and broadcasts to all clients.
- **Operations-based sync:** Every user action (draw stroke, add image, move element) becomes a serialized operation message. The host applies it to state and rebroadcasts to all peers.

### Technology Stack

| Component | Technology | Why |
|---|---|---|
| UI Framework | Avalonia 11+ | Cross-platform .NET desktop (Windows, macOS, Linux) |
| Rendering | SkiaSharp (via Avalonia) | Direct `SKCanvas` access for vector drawing, images, strokes |
| Pan/Zoom | `Avalonia.Controls.PanAndZoom` (wieslawsoltes, v12, MIT) | Battle-tested infinite canvas control |
| Networking | LiteNetLib v2.0 | Reliable UDP, multiple channel types, NAT traversal helpers |
| Serialization | MessagePack-CSharp | Fast binary serialization for board state and network messages |

### Why These Choices

- **LiteNetLib over raw TCP/SignalR:** Purpose-built for game networking. Supports reliable, sequenced, and unreliable delivery on separate channels. Low latency over VPN. No HTTP overhead.
- **SkiaSharp over Avalonia shapes:** Direct canvas control avoids Avalonia's visual tree overhead for hundreds of strokes/shapes. Better performance at scale.
- **Host-authoritative over CRDT:** Simpler to implement and reason about for a small group (2-8 players). CRDTs add complexity without clear benefit when one player is always online as host.
- **MessagePack over JSON/protobuf:** Fastest .NET binary serializer, compact, good for both persistence and wire format.

## 2. Board State Model

### Base Element

```csharp
public abstract class BoardElement
{
    public Guid Id { get; set; }
    public Vector2 Position { get; set; }  // world coordinates
    public Vector2 Size { get; set; }
    public float Rotation { get; set; }    // degrees
    public int ZIndex { get; set; }
    public Guid OwnerId { get; set; }     // ClientId of who created it
    public bool IsLocked { get; set; }
}
```

### Element Subtypes

| Type | Key Properties |
|---|---|
| `StrokeElement` | `List<Vector2> Points`, `SKColor Color`, `float Thickness` |
| `ShapeElement` | `ShapeType Type` (Rectangle, Ellipse, Line, Arrow), `SKColor StrokeColor`, `SKColor FillColor`, `float StrokeWidth`. **Note:** Arrow is in the data model but deferred from v0.1 MVP. |
| `ImageElement` | `byte[] ImageData` (embedded, not path-referenced), `string OriginalFileName` |
| `TextElement` | `string Text`, `float FontSize`, `SKColor Color`, `string FontFamily`. **Deferred from v0.1 MVP** — included in data model for forward compatibility. |

### Board State

```csharp
public class BoardState
{
    public Guid BoardId { get; set; }
    public string BoardName { get; set; }
    public List<BoardElement> Elements { get; set; }
    public DateTime LastModified { get; set; }
}
```

**Note on forward compatibility:** `TextElement` and `BoardElement.IsLocked` are defined in the v0.1 data model but not implemented in the UI. This avoids breaking the `.bfga` file format when these features are added in v0.2+.

## 3. Network Protocol

### Operations

All mutations are expressed as operation messages:

| Operation | Direction | Purpose |
|---|---|---|
| `AddElement` | Client -> Host -> All | Create a new element |
| `UpdateElement` | Client -> Host -> All | Modify element properties |
| `DeleteElement` | Client -> Host -> All | Remove an element |
| `MoveElement` | Client -> Host -> All | Change element position/size/rotation |
| `CursorUpdate` | Client -> Host -> All | Report cursor position |
| `DrawStrokePoint` | Client -> Host -> All | Stream individual stroke points during drawing. Each message includes the target `StrokeId` (the `Guid` that will become the final `StrokeElement.Id`). |
| `CancelStroke` | Client -> Host -> All | Cancel an in-progress stroke preview, identified by `StrokeId`. |
| `RequestFullSync` | Client -> Host | Request complete board state |
| `FullSyncResponse` | Host -> Client | Complete board state dump (all elements + player roster) |
| `PeerJoined` | Host -> All | A new player connected (ClientId, display name, assigned color) |
| `PeerLeft` | Host -> All | A player disconnected |

### LiteNetLib Channels

| Channel | Delivery Mode | Used For |
|---|---|---|
| 0 | Reliable Ordered | All operations: element CRUD, stroke streaming, move/resize, full sync, join/leave, roster updates |
| 1 | Unreliable | Cursor positions (~60 updates/sec, packet loss acceptable) |

**Design rationale:** A single reliable ordered channel for all operations eliminates cross-channel ordering issues and per-channel sequencing conflicts between users. For a small group (2-8 players), throughput on one reliable ordered channel is more than sufficient. If profiling later shows bottlenecks, additional channels can be introduced.

### Connection Flow

1. **Host starts:** `NetManager` listens on port 7777 (configurable)
2. **Client connects:** Enters host VPN IP + port, and a display name (free-text, stored locally)
3. **Host validates:** Accepts connection, assigns client a unique `ClientId` (GUID), sends `FullSyncResponse` with entire board state and the player roster (id + display name for each connected peer)
4. **Steady state:** Client sends operations; host validates, applies, broadcasts to all

### User Identity

- Each player chooses a **display name** at startup (persisted in local app settings).
- On connection, the host assigns a unique `ClientId` (GUID) used as the `OwnerId` for elements and cursor identification.
- The host maintains a **player roster** (`ClientId` -> display name + assigned color) and broadcasts roster updates when players join/leave.
- No authentication or account system — trust-based among friends.

### Host Validation

The host performs lightweight validation before applying operations:
- **Existence checks:** Element referenced by `UpdateElement`/`DeleteElement`/`MoveElement` must exist in board state.
- **No conflict resolution:** In a small trusted group, last-write-wins. The host does not reject valid operations based on ownership or concurrent edits.
- **Malformed messages:** Dropped silently (logged in debug mode).

### Large Image Transfer

Images can be several MB. LiteNetLib handles fragmentation internally for reliable channels (its built-in MTU-based splitting and reassembly). The library delivers the fully reassembled payload to the application layer. **No app-level progress indicator in v0.1** — the image simply appears when transfer completes. Progress indicators can be added in v0.2 if needed (would require app-level chunking).

## 4. Rendering and Canvas

### SkiaSharp Custom Control

A custom Avalonia control that overrides `Render()` to get direct `SKCanvas` access. This provides:

- **Stroke rendering:** Catmull-Rom or bezier smoothing on point arrays for clean vector lines
- **Shape rendering:** Rectangles, ellipses, lines, arrows with configurable stroke/fill
- **Image rendering:** `SKBitmap` decoded from embedded byte arrays
- **Selection handles:** Resize and rotate grips on selected elements
- **Live cursors:** Colored arrow cursors with username labels

### Infinite Canvas

`Avalonia.Controls.PanAndZoom` `ZoomBorder` wraps the SkiaSharp canvas control. It provides:

- Mouse wheel zoom (in/out)
- Drag-to-pan (middle mouse or space+drag)
- Matrix transforms for viewport management

The canvas renders in **world coordinates**; `ZoomBorder` handles the viewport-to-screen transform.

### Hit Testing

Reverse-iterate elements by ZIndex (top-to-bottom) to find what's under the cursor:

- **Strokes:** Distance-to-polyline (threshold based on stroke thickness)
- **Shapes:** Bounding rectangle intersection
- **Images:** Bounding rectangle intersection

## 5. Tool System

| Tool | Behavior |
|---|---|
| **Select** | Click to select, drag to move, handles to resize/rotate. Drag-box for multi-select. |
| **Pen/Draw** | Freehand drawing. Mouse down starts collecting points with a pre-assigned `StrokeId` (GUID); mouse up emits `AddElement(StrokeElement)` with that same ID. During drawing, points are streamed via `DrawStrokePoint` (each including the `StrokeId`) for live preview on other clients. If cancelled (e.g. Escape), a `CancelStroke` message clears the preview on peers. All messages flow over Ch0 Reliable Ordered. |
| **Shape** | Click-drag to create rectangle, ellipse, or line. Sub-tool selector for shape type. (Arrow shape deferred to v0.2.) |
| **Image** | Drag-drop from filesystem or paste from clipboard. Reads file bytes, embeds in `ImageElement`. |
| **Text** | Click to place, inline editing. *(Deferred to v0.2)* |
| **Eraser** | Click on an element to delete it (sends `DeleteElement`). |
| **Pan** | Middle-mouse button or space+drag. Handled by ZoomBorder, always available regardless of active tool. |

## 6. Persistence and Export

### Auto-Save

- Host auto-saves every 60 seconds and on session close
- Saves to `.bfga` file (MessagePack-serialized `BoardState`)
- Default location: `~/Documents/BFGA/` (configurable)

### Load

- Host can open any `.bfga` file to resume a previous board session
- Board state is deserialized and becomes the active session state

### Client Export

- Any connected client can request "Download Board"
- Host responds with `FullSyncResponse`
- Client saves the received state as a `.bfga` file locally

### Re-Hosting

Any `.bfga` file can be opened in host mode by any player. Boards are portable and not tied to a specific host.

## 7. Project Structure

```
BFGA-BoardForGamingApp/
  src/
    BFGA.Core/                    # Shared models, operations, serialization
      Models/                     # BoardElement, StrokeElement, ShapeElement, etc.
      Operations/                 # AddElement, MoveElement, DeleteElement, etc.
      Serialization/              # MessagePack formatters and resolvers
    BFGA.Network/                 # LiteNetLib wrapper and host/client logic
      GameHost.cs                 # Host-side networking (listen, validate, broadcast)
      GameClient.cs               # Client-side networking (connect, send, receive)
      Protocol/                   # Message types, channel definitions
    BFGA.Canvas/                  # SkiaSharp rendering, tool system, hit testing
      BoardCanvas.cs              # Custom Avalonia/SkiaSharp control
      Tools/                      # SelectTool, PenTool, ShapeTool, ImageTool, EraserTool
      Rendering/                  # Per-element-type renderers
    BFGA.App/                     # Avalonia application shell
      Views/                      # AXAML views (MainWindow, BoardView, ConnectionDialog)
      ViewModels/                 # MVVM ViewModels
      Program.cs                  # Entry point
  tests/
    BFGA.Core.Tests/              # Unit tests for models, operations, serialization
    BFGA.Network.Tests/           # Unit tests for protocol handling
  docs/
    superpowers/specs/            # Design specifications
  BFGA.sln                        # Solution file
```

### Project Dependencies

```
BFGA.App -> BFGA.Canvas, BFGA.Network, BFGA.Core
BFGA.Canvas -> BFGA.Core
BFGA.Network -> BFGA.Core
BFGA.Core -> (no internal deps)
```

## 8. MVP Scope (v0.1)

### Build Order

1. **Avalonia app scaffold** with SkiaSharp canvas + PanAndZoom working
2. **Board state model** + MessagePack serialization + save/load `.bfga` files
3. **Drawing tools** - Pen (freehand), Select (move/delete), basic shapes (rect, ellipse, line)
4. **Image support** - drag-drop images onto the board, embedded in board state
5. **Networking** - LiteNetLib host/client, operation sync, full-state sync on join
6. **Live cursors** - show each connected user's cursor with a colored indicator and name
7. **Export** - client-side "Download Board" button

### Deferred to v0.2+

- Text tool and inline text editing
- Undo/redo (operation stack)
- Grid snapping and alignment guides
- Element locking and per-user permissions
- Chat or voice integration
- Layers system

## 9. Constraints and Assumptions

- **Target group size:** 2-8 players. Not designed for dozens of simultaneous users.
- **VPN required:** Players must be on the same virtual network. No NAT hole-punching or relay servers.
- **Desktop only:** Avalonia desktop targets (Windows, macOS, Linux). No mobile or web.
- **No authentication:** Trust-based model among friends. No user accounts or access control.
- **Image size:** Large images (10MB+) may cause brief freezes during transfer. No progress indicator in v0.1; boards with many large images will increase join-time sync duration.
