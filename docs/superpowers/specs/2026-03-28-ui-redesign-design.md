# BFGA UI Redesign & Feature Additions: Design Specification

**Date:** 2026-03-28
**Status:** Approved
**Builds on:** `docs/superpowers/specs/2026-03-25-bfga-whiteboard-design.md`

## Overview

A visual overhaul and feature expansion of the existing BFGA whiteboard app. The redesign applies a Swiss-design-influenced editorial aesthetic (deep blacks, minimal chrome, intentional typography) while adding four user-facing features: property panel, player roster, undo/redo, and transitions/polish.

**Approach:** Incremental overlay — modify existing files in place, add new components, keep all 203 existing tests passing. No clean-slate rewrite.

**Scope explicitly excluded:** Quick color picker (standalone), board settings/export UI, light theme support, mobile/responsive layout.

---

## 1. Theme & Color System

### 1.1 Resource Architecture

Split the current single `WhiteboardTheme.axaml` into three files loaded in `App.axaml`:

| File | Purpose |
|------|---------|
| `Styles/Colors.axaml` | All color tokens as `SolidColorBrush` resources |
| `Styles/Typography.axaml` | Font families, sizes, weights as `Style` selectors |
| `Styles/WhiteboardTheme.axaml` | Component-level styles (toolbar, buttons, panels) referencing the above tokens |

### 1.2 Color Tokens

All UI colors are defined once in `Colors.axaml` as named `SolidColorBrush` resources:

| Token | Hex | Usage |
|-------|-----|-------|
| `BgBase` | `#0A0A0A` | Window/app background |
| `BgSurface` | `#0D0D0D` | Canvas background, card backgrounds |
| `BgElevated` | `#111111` | Floating panels (toolbar, property, bottom bar) |
| `BgOverlay` | `#161616` | Hover states, overlays |
| `TextPrimary` | `#FAFAFA` | Headings, primary text, active labels |
| `TextSecondary` | `#B0B0B0` | Body text, descriptions |
| `TextTertiary` | `#666666` | Hints, shortcut labels |
| `TextMuted` | `#404040` | Decorative text, disabled labels |
| `BorderDefault` | `#2A2A2A` | Panel borders, dividers |
| `BorderSubtle` | `#1A1A1A` | Inner dividers, subtle separators |
| `AccentWhite` | `#FFFFFF` | Active tool indicator, primary actions |

### 1.3 SkiaSharp Color Bridge

A static class `ThemeColors` in `BFGA.Canvas` exposes the same palette as `SKColor` values for canvas rendering. This avoids duplicating hex values between AXAML and C#.

```csharp
public static class ThemeColors
{
    public static readonly SKColor BgSurface = new(0x0D, 0x0D, 0x0D);
    public static readonly SKColor DotGrid = new(0x1F, 0x1F, 0x1F);
    // ... etc
}
```

### 1.4 Typography

| Role | Font | Weight | Size |
|------|------|--------|------|
| Logo | Inter | 200 (ExtraLight) | 28px, letter-spacing 12px |
| Section heading | Inter | 300 (Light) | 11px, letter-spacing 3px, uppercase |
| Input label | JetBrains Mono | 400 | 10px, uppercase |
| Input value | Inter | 400 | 14px |
| Button text | Inter | 500 (Medium) | 13px, letter-spacing 1px, uppercase |
| Status text | Inter | 400 | 12px |
| Tooltip | Inter | 400 | 11px |

**Font loading:** Bundle Inter and JetBrains Mono `.ttf` files as embedded resources in `BFGA.App/Assets/Fonts/`. Load in `App.axaml` via Avalonia's `FontFamily` resource with `avares://` URI.

---

## 2. Connection Screen Redesign

### 2.1 Layout

Replace the current utilitarian form with an editorial design:

```
┌──────────────────────────────────────────────────────┐
│                                    01 / CONNECTION    │
│                                                       │
│                    ○  ○                               │
│                  (decorative circles)                 │
│                                                       │
│                    B  F  G  A                         │
│                  (spaced logo)                        │
│                                                       │
│              ┌─────────────────────┐                  │
│              │  [HOST]  [JOIN]     │   ← tab switcher │
│              │                     │                  │
│              │  DISPLAY NAME       │                  │
│              │  ┌─────────────┐    │                  │
│              │  │ Player1     │    │                  │
│              │  └─────────────┘    │                  │
│              │                     │                  │
│              │  PORT               │   (or HOST ADDR  │
│              │  ┌─────────────┐    │    + PORT when   │
│              │  │ 9050        │    │    Join mode)     │
│              │  └─────────────┘    │                  │
│              │                     │                  │
│              │  [ START HOST ]     │   ← single       │
│              │                     │     context btn  │
│              │  Connected as host  │   ← status       │
│              └─────────────────────┘                  │
│                                                       │
│  B│O│A│R│D  (vertical text)                          │
└──────────────────────────────────────────────────────┘
```

### 2.2 Key Changes from Current

| Aspect | Current | New |
|--------|---------|-----|
| Mode selection | Two `RadioButton`s | Two styled tab buttons (`HOST` / `JOIN`) |
| Visible buttons | 4 always visible (Start/Stop/Connect/Disconnect) | 1-2 context-aware (changes label per state) |
| Form fields | All always shown | HOST mode: name + port. JOIN mode: name + address + port |
| Card width | Full-width StackPanel | Centered 380px card |
| Background | `#1F1F1F` hardcoded | `BgBase` (#0A0A0A) from theme |
| Branding | None | Spaced "B F G A" logo + decorative circles |
| Decorative | None | Section number "01 / CONNECTION", vertical "BOARD" text |

### 2.3 Tab-to-Button State Machine

| State | Active Tab | Button Label | Button Action |
|-------|-----------|--------------|---------------|
| Disconnected, Host mode | HOST | `START HOST` | `StartHostAsync()` |
| Hosting | HOST | `STOP HOST` | `StopHostAsync()` |
| Disconnected, Join mode | JOIN | `CONNECT` | `ConnectAsync()` |
| Joining | JOIN | `CONNECTING...` (disabled) | — |
| Connected | — | (screen transitions to Board) | — |

### 2.4 ViewModel Changes

No new ViewModel. Existing `MainViewModel` already exposes `ConnectionMode`, `ConnectionState`, `DisplayName`, `HostAddress`, `Port`, `StartHostCommand`, `StopHostCommand`, `ConnectCommand`, `DisconnectCommand`. The redesigned `ConnectionView.axaml` rebinds to these same properties with new visual layout.

### 2.5 Save/Load Button Location

The current connection screen has "Load Board" and "Save Board" buttons. In the redesigned connection screen, these buttons are **kept on the connection screen** below the main action button, styled as secondary/ghost buttons. Save/Load are pre-session operations (load a board file before hosting, or save after disconnecting), so they belong on the connection screen. During an active board session, autosave handles persistence automatically.

---

## 3. Board Screen Components

### 3.1 Toolbar Redesign

**Same 7 tools**, reorganized with visual grouping:

```
┌──────┐
│  ▶   │  Select  (V)
│  ✋  │  Hand    (H)
│──────│  ← subtle divider
│  ✏   │  Pen     (P)
│  ▢   │  Rect    (R)
│  ○   │  Ellipse (E)
│──────│  ← subtle divider
│  🖼  │  Image   (I)
│  ✕   │  Eraser  (X)
└──────┘
```

**Button states:**

| State | Background | Left Border | Foreground |
|-------|-----------|-------------|------------|
| Default | Transparent | None | `TextSecondary` (#B0B0B0) |
| Hover | `BgOverlay` (#161616) | None | `TextPrimary` (#FAFAFA) |
| Active (selected) | `BorderSubtle` (#1A1A1A) | 2px `AccentWhite` (#FFFFFF) | `TextPrimary` (#FAFAFA) |

**Positioning:** Floating left, vertically centered in the board area. Same 56px width. Uses `VerticalAlignment="Center"` within the DockPanel left dock area.

**Visual indicator binding:** `BoardScreenViewModel.SelectedTool` (already exists) drives which button shows the active state via a value converter or style class toggle.

### 3.2 Property Panel

A new `PropertyPanel.axaml` UserControl, 220px wide, floating on the right side of the board area.

**Visibility rule:** Visible when `SelectedTool` is `Pen`, `Rectangle`, or `Ellipse`. Hidden when `Hand`, `Select`, `Image`, or `Eraser`. (Note: `BoardToolType.Shape` exists in the enum but is not exposed in the toolbar UI — Rectangle and Ellipse are the user-facing shape tools. Image tool has no configurable properties in this version — images are dropped at their original size.)

**Layout:**

```
┌─ PROPERTIES ──────────┐
│                        │
│  STROKE                │
│  ┌──┬──┬──┬──┬──┬──┬──┬──┐ │
│  │██│██│██│██│██│██│██│██│ │  ← 8 colors row 1
│  │██│██│██│██│██│██│██│██│ │  ← 8 colors row 2
│  └──┴──┴──┴──┴──┴──┴──┴──┘ │
│                        │
│  WIDTH        ━━━●━━━  │  ← slider 1-20px
│  2px                   │
│                        │
│  OPACITY      ━━━━━●━  │  ← slider 0-100%
│  100%                  │
│                        │
│  FILL (shapes only)    │
│  ┌──┬──┬──┬──┬──┬──┬──┬──┐ │
│  │██│██│██│██│██│██│██│██│ │
│  │██│██│██│██│██│██│██│██│ │
│  └──┴──┴──┴──┴──┴──┴──┴──┘ │
└────────────────────────┘
```

**Color swatch palette (16 colors):**

```
Row 1: #000000  #FFFFFF  #FF3B30  #FF9500  #FFCC00  #34C759  #007AFF  #AF52DE
Row 2: #5856D6  #FF2D55  #A2845E  #8E8E93  #636366  #48484A  #2C2C2E  #1C1C1E
```

**State properties** (new on `BoardScreenViewModel`):

```csharp
public SKColor SelectedStrokeColor { get; set; } = SKColors.White;
public SKColor SelectedFillColor { get; set; } = SKColors.Transparent;
public float StrokeWidth { get; set; } = 2f;
public float Opacity { get; set; } = 1f;  // 0.0 to 1.0
```

**Opacity model:** Opacity is applied at draw time only — it is **not** persisted in element models. When creating a new element, the property panel's opacity value is baked into the `SKColor.Alpha` channel of the stroke/fill colors (e.g., opacity 0.5 + color `#FF3B30` → `#80FF3B30`). This avoids adding new fields to `StrokeElement`, `ShapeElement`, or the serialization format. The opacity slider provides a convenient UI for controlling alpha without users thinking in hex.

**Data flow:** `BoardView` reads these properties from `BoardScreenViewModel` and passes them to `BoardToolController` when creating strokes/shapes. The tool controller bakes opacity into color alpha and uses the result as defaults for new elements.

### 3.3 Player Roster

**Position:** Top-right of the board area, overlaid above the canvas.

**Design:** Horizontal row of 28px colored circles with 2-letter initials, overlapping by -6px. Hover shows tooltip with full display name.

```
                                    [JD][AS][MK]
┌──────────────────────────────────────────────┐
│                                              │
│              (board canvas)                  │
```

**Color assignment:** 8-preset palette, assigned by join order (modulo 8):

```
#FF6B6B  #4ECDC4  #45B7D1  #96CEB4  #FFEAA7  #DDA0DD  #98D8C8  #F7DC6F
```

**Data source:** `MainViewModel.Roster` (already exists as a dictionary of connected peers). A new `RosterOverlay.axaml` UserControl renders the circles. The overlay is placed in the `BoardScreen.axaml` layout as an absolute-positioned element in the top-right.

**Roster entry model:** Each entry uses the existing `PlayerInfo` model which has `DisplayName` and `AssignedColor`. The 2-letter initials are the first two characters of `DisplayName`, uppercased. The `AssignedColor` maps to the 8-preset palette above.

### 3.4 Bottom Bar Redesign

**Position:** Floating center-bottom of the board area (not full-width dock).

**Layout:**

```
┌──────────────────────────────────────────┐
│  [↶] [↷]  │  [−]  100%  [+]            │
│  undo redo    zoom controls              │
└──────────────────────────────────────────┘
```

**Changes from current:**
- Add undo/redo buttons on the left, separated by a divider from zoom controls
- Float centered instead of full-width dock
- Use theme tokens instead of hardcoded colors

---

## 4. Undo/Redo System

### 4.1 Core Design

**Scope:** Per-user undo. Each player can only undo their own operations. Undoing another player's work is not supported.

**Location:** `UndoRedoManager` class in `BFGA.Network` (not `BFGA.Core`). This is because the undo system works in terms of `BoardOperation` subtypes (`AddElementOperation`, `DeleteElementOperation`, `UpdateElementOperation`, `MoveElementOperation`) which are defined in `BFGA.Network.Protocol`. Placing `UndoRedoManager` in `BFGA.Network` avoids a circular dependency — `BFGA.Core` does not reference `BFGA.Network`.

**Host identity:** The host needs a stable user ID for its own undo stack. `GameHost` generates a `Guid` at construction time (`_hostUserId`) and uses it as the `SenderId` when recording host-local operations in the undo system. This ID is not a network peer ID — it is purely for undo stack keying. When `TryApplyLocalOperation()` is called, the host tags the operation with `_hostUserId` before pushing it to `UndoRedoManager`.

### 4.2 Operations

The undo system tracks inverse operation pairs. These use the existing `BoardOperation` subtypes from `src/BFGA.Network/Protocol/BoardOperation.cs`:

| Forward Operation | Inverse (Undo) |
|-------------------|-----------------|
| `AddElementOperation(element)` | `DeleteElementOperation(elementId)` |
| `DeleteElementOperation(elementId)` | `AddElementOperation(snapshotOfDeletedElement)` |
| `UpdateElementOperation(elementId, newState)` | `UpdateElementOperation(elementId, previousState)` |

Note: `MoveElementOperation` is treated as a special case of `UpdateElementOperation` for undo purposes — the inverse captures the element's pre-move position.

**Pre-change state capture:** The undo system needs snapshots of elements before they are modified or deleted. The capture seam is in `BoardToolController` and `GameHost`:

- **Host-side (authoritative):** `GameHost` already holds the authoritative element state in `_boardElements`. Before applying a `DeleteElementOperation`, `UpdateElementOperation`, or `MoveElementOperation`, the host snapshots the affected element from `_boardElements` and passes both the operation and the snapshot to `UndoRedoManager.Push()`. This is the natural place because the host processes every operation sequentially.
- **Client-side (shadow stacks):** The client shadow stack does NOT need full element snapshots. It only needs to track operation count for `CanUndo`/`CanRedo`. The shadow stack stores lightweight entries: `{ OperationType, ElementId }`. When the client sends an undo request, the host does the actual state restoration. The client shadow stack is purely for UI button enable/disable.

This means the client shadow stack is simpler than described in section 4.5 — it's a counter-like structure, not a full snapshot store.

Each undo entry stores (host-side):
```csharp
public record UndoEntry(
    Guid UserId,
    BoardOperation ForwardOp,
    BoardOperation InverseOp
);
```

### 4.3 Stack Management

- **Per-user stacks:** `Dictionary<Guid, Stack<UndoEntry>>` for undo, same for redo.
- **Max depth:** 50 entries per user. When exceeded, oldest entries are discarded (stack becomes a bounded deque).
- **Redo clear:** When a user performs a new forward operation, their redo stack is cleared.

### 4.4 Network Integration

Two new `BoardOperation` subtypes added to `src/BFGA.Network/Protocol/BoardOperation.cs`:

| Operation | Payload | Behavior |
|-----------|---------|----------|
| `UndoOperation` | (uses inherited `SenderId`) | Host pops sender's undo stack, applies inverse, pushes to redo |
| `RedoOperation` | (uses inherited `SenderId`) | Host pops sender's redo stack, applies forward, pushes to undo |

These operations carry no additional payload beyond the base `BoardOperation` fields (`SenderId`, `Timestamp`). The `SenderId` identifies whose stack to pop. No separate `UserId` field is needed.

**Host validation:** Host checks that the sender's undo/redo stack is non-empty before applying. If empty, the operation is silently ignored (no error response — the client's shadow stack will reconcile on the next `FullSyncResponse`).

**Host rejection path:** There is no explicit rejection message. If the host ignores an undo/redo request (e.g., element was already deleted by another user), the client's shadow stack remains stale until the next `FullSyncResponse` clears it. This is acceptable because undo is best-effort in a collaborative environment.

**Authority:** The host owns the `UndoRedoManager`. Clients send `UndoOperation`/`RedoOperation` requests. The host resolves them and broadcasts the resulting `AddElementOperation`/`DeleteElementOperation`/`UpdateElementOperation` to all clients as normal operations.

### 4.5 UI Integration

- **Keyboard:** `Ctrl+Z` → undo, `Ctrl+Y` or `Ctrl+Shift+Z` → redo
- **Buttons:** In bottom bar, undo (↶) and redo (↷) buttons. Disabled when respective stack is empty.
- **Host mode binding:** `MainViewModel` exposes `UndoCommand`, `RedoCommand`, `CanUndo`, `CanRedo`. In host mode, `MainViewModel` accesses undo state through the `IGameHostSession` interface. Two new methods are added to `IGameHostSession`: `bool TryUndo()` and `bool TryRedo()`, plus two properties: `bool CanUndo { get; }` and `bool CanRedo { get; }`. These delegate to the `UndoRedoManager` inside `GameHost`, using the host's `_hostUserId`. This preserves the existing abstraction boundary — `MainViewModel` never touches `UndoRedoManager` or `GameHost` directly.
- **Client mode binding:** Clients maintain a simple local shadow counter to drive `CanUndo`/`CanRedo` UI state. Each time the client sends an element-mutating operation (`AddElementOperation`, `DeleteElementOperation`, `UpdateElementOperation`, `MoveElementOperation`), it increments the undo shadow counter and resets the redo shadow counter to 0. When the client sends `UndoOperation`, it decrements undo and increments redo. When it sends `RedoOperation`, it decrements redo and increments undo. `CanUndo` = undo counter > 0, `CanRedo` = redo counter > 0. The host holds the actual snapshots and does the real work.
  
- **Shadow counter reconciliation:** On receiving a `FullSyncResponse` (and only on `FullSyncResponse`, not on `PeerJoined`), the client resets both shadow counters to 0. This means `CanUndo`/`CanRedo` reset to false after reconnection or board load.
- **Best-effort model:** The shadow stacks are optimistic. If another user deletes an element that's in your undo stack, your undo attempt will be silently ignored by the host. The client discovers this implicitly on the next full sync. No explicit error handling is needed for this edge case.

### 4.6 Limitations

- **Element-level only.** No board-level undo (e.g., cannot undo a full-board clear).
- **No cross-user undo.** A user cannot undo another user's strokes.
- **State sync:** On full board sync (new client joins, reconnect), undo/redo stacks are NOT transferred. New clients start with empty stacks. This is intentional — undo history is session-local.

---

## 5. Transitions & Polish

### 5.1 Screen Transition

**Connection → Board:** 300ms crossfade. Implemented via Avalonia `PageTransition` or a custom `CrossFade` transition on the `ContentControl` in `MainWindow.axaml`.

### 5.2 Tool Selection

**150ms** background color transition on tool buttons. Achieved via Avalonia `Transition` on `Background` property.

### 5.3 Hover Effects

| Element | Effect | Duration |
|---------|--------|----------|
| Tool buttons | Background → `BgOverlay` | 150ms |
| Color swatches | Scale 1.0 → 1.15 | 100ms |
| Roster avatars | Scale 1.0 → 1.1 | 150ms |
| Action buttons | Background lighten | 150ms |

### 5.4 Canvas Polish

| Element | Current | New |
|---------|---------|-----|
| Canvas background | `#111318` | `#0D0D0D` (BgSurface) |
| Dot grid color | `SKColor(140,150,165,120)` | `#1F1F1F` (subtler, non-transparent) |

### 5.5 Status Text

200ms fade on status text changes (connection status, save confirmation). Implemented via Avalonia `DoubleTransition` on `Opacity`.

### 5.6 Connection Button

While in `Joining` state, button text changes to `CONNECTING...` and is disabled. No spinner — text change is sufficient feedback for the VPN-connected use case.

---

## 6. File & Project Structure Changes

### 6.1 New Files

| File | Purpose |
|------|---------|
| `src/BFGA.App/Styles/Colors.axaml` | Color token resources |
| `src/BFGA.App/Styles/Typography.axaml` | Font styles |
| `src/BFGA.App/Assets/Fonts/Inter-*.ttf` | Inter font files |
| `src/BFGA.App/Assets/Fonts/JetBrainsMono-Regular.ttf` | JetBrains Mono font file |
| `src/BFGA.App/Views/PropertyPanel.axaml` + `.cs` | Property panel control |
| `src/BFGA.App/Views/RosterOverlay.axaml` + `.cs` | Player roster bubbles |
| `src/BFGA.Network/UndoRedoManager.cs` | Undo/redo logic (in Network layer to access BoardOperation types) |
| `src/BFGA.Canvas/Rendering/ThemeColors.cs` | SkiaSharp color constants |

### 6.2 Modified Files

| File | Changes |
|------|---------|
| `src/BFGA.App/App.axaml` | Load Colors.axaml, Typography.axaml, font resources |
| `src/BFGA.App/Styles/WhiteboardTheme.axaml` | Refactor to reference color tokens, add new component styles |
| `src/BFGA.App/Views/ConnectionView.axaml` | Full redesign (editorial layout, tab switcher) |
| `src/BFGA.App/Views/BoardScreen.axaml` | Add PropertyPanel, RosterOverlay, float toolbar |
| `src/BFGA.App/Views/ToolBar.axaml` | Grouped tools, active state styles, dividers |
| `src/BFGA.App/Views/BottomBar.axaml` | Add undo/redo buttons, float centered |
| `src/BFGA.App/ViewModels/BoardScreenViewModel.cs` | Add SelectedStrokeColor, StrokeWidth, Opacity, SelectedFillColor properties |
| `src/BFGA.App/ViewModels/MainViewModel.cs` | Add UndoCommand, RedoCommand, CanUndo, CanRedo; wire undo/redo to host/network |
| `src/BFGA.App/Networking/IGameHostSession.cs` | Add TryUndo(), TryRedo(), CanUndo, CanRedo for host-mode undo access |
| `src/BFGA.App/Networking/NetworkGameSessionFactory.cs` | Update host session wrapper to delegate new undo/redo methods to GameHost |
| `src/BFGA.App/MainWindow.axaml` | Add screen transition, undo/redo keyboard shortcuts |
| `src/BFGA.App/MainWindow.axaml.cs` | Handle Ctrl+Z/Y keyboard shortcuts |
| `src/BFGA.Canvas/Rendering/DotGridHelper.cs` | Use ThemeColors instead of hardcoded SKColor |
| `src/BFGA.Canvas/Rendering/ElementDrawingHelper.cs` | Use ThemeColors for canvas background |
| `src/BFGA.Canvas/BoardCanvas.cs` | Use ThemeColors for background fill |
| `src/BFGA.Canvas/Tools/BoardToolController.cs` | Accept tool properties (color, width, opacity) from ViewModel |
| `src/BFGA.Network/GameHost.cs` | Integrate UndoRedoManager, handle UndoOperation/RedoOperation |
| `src/BFGA.Network/Protocol/BoardOperation.cs` | Add `UndoOperation`, `RedoOperation` subtypes + `OperationType` enum entries |

### 6.3 New Test Files

| File | Covers |
|------|--------|
| `tests/BFGA.Network.Tests/UndoRedoManagerTests.cs` | Undo/redo stack logic, per-user isolation, max depth, redo clear |
| `tests/BFGA.App.Tests/PropertyPanelTests.cs` | Property panel visibility rules, default values |
| `tests/BFGA.App.Tests/RosterOverlayTests.cs` | Roster rendering, color assignment, initials extraction |

---

## 7. Non-Goals & Constraints

- **No light theme.** Dark-only. The `FluentTheme` base stays but all custom styling assumes dark.
- **No responsive breakpoints.** Fixed layout. Window minimum size enforced.
- **No custom color picker.** 16 preset swatches only. Custom color entry is a future feature.
- **No element-level opacity in UI.** The opacity slider affects new elements only. Editing existing element opacity requires selection + modify (future feature).
- **Existing tests must pass.** All 203 current tests remain green throughout.
- **Inter/JetBrains Mono must be bundled.** No external font downloads at runtime.

---

## 8. Success Criteria

1. App launches with the redesigned connection screen showing the editorial layout
2. HOST/JOIN tab switcher works correctly, showing context-appropriate fields and button
3. Board screen shows the redesigned toolbar with visual grouping and active tool indicator
4. Property panel appears/hides based on selected tool and controls stroke color, fill, width, opacity
5. Player roster shows connected peers as colored circles with initials
6. Undo/redo works per-user via Ctrl+Z/Ctrl+Y and bottom bar buttons
7. Screen transitions use 300ms crossfade
8. All 203+ tests pass
9. Build has 0 warnings, 0 errors
