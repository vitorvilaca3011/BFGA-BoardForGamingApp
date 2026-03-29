# Infinite Canvas: Viewport-Sized Control with Virtual World

**Date:** 2026-03-29  
**Status:** Draft  
**Replaces:** Fixed 200K workspace approach

## Problem

The board uses a fixed 200,000 x 200,000 pixel `BoardCanvas` control wrapped by `ZoomBorder` (PanAndZoom NuGet). This causes:

1. **Drawings disappear at certain zoom levels.** Avalonia's rendering pipeline culls the `ICustomDrawOperation` when the 200K-unit bounds interact poorly with the compositor's dirty-region/clip math at low zoom.
2. **Visual clipping artifacts.** The massive control size causes rendering precision issues when combined with `ZoomBorder`'s `RenderTransform`.
3. **Hard workspace boundary.** Users cannot place content beyond the 200K edge.

## Solution

Replace the fixed-size canvas with a **viewport-sized control** that draws into a virtual infinite world. `BoardCanvas` fills its parent's layout bounds (matching the screen viewport). All pan/zoom math is managed directly via an SKCanvas transform matrix inside the SkiaSharp draw operation. The `ZoomBorder` NuGet dependency is removed.

Board coordinate (0,0) is always the logical origin. There is no workspace boundary.

## Architecture

### Visual Tree (before)

```
BoardViewport (Border, ClipToBounds=true)
  -> ZoomBorder (PanAndZoom, RenderTransform)
    -> BoardCanvas (Control, Width=200K, Height=200K)
```

### Visual Tree (after)

```
BoardViewport (Border, ClipToBounds=true)
  -> BoardCanvas (Control, fills parent via Stretch)
```

ZoomBorder is removed. BoardViewport manages zoom/pan state. BoardCanvas receives the current zoom/pan values and builds the SKCanvas transform matrix during `Render()`.

### Coordinate Spaces

| Space | Description |
|-------|-------------|
| **Screen space** | Pixels relative to the BoardCanvas control bounds. Origin at control top-left. |
| **Board space** | Logical coordinates where elements live. Origin at (0,0), extends infinitely. |

**Conversion formulas (no origin offset):**

```
ScreenToBoard(screenPoint) = (screenPoint - pan) / zoom
BoardToScreen(boardPoint)  = boardPoint * zoom + pan
```

Where `pan` is a Vector2 in screen pixels and `zoom` is a positive float (1.0 = 100%).

### Initial State

On first layout, `pan` is set so board (0,0) is centered in the viewport:

```
pan = (viewportWidth / 2, viewportHeight / 2)
```

Zoom starts at 1.0.

## Component Changes

### 1. BoardSurfaceHelper

**Remove:** `StableWorkspaceSize`, `StableOriginOffset`, `GetSurfaceMetrics()`, `BoardToCanvas()`, `CanvasToBoard()`, `BoardSurfaceMetrics`.

**Keep:** `GetBoardBounds(BoardState?)` -- computes content AABB in board coordinates. No changes needed.

### 2. BoardCanvas

**Remove:** Fixed `Width`/`Height` in constructor. The `canvas.Translate(100K, 100K)` origin offset. The 200K workspace background rect.

**Add:**
- Properties: `float Zoom`, `Vector2 Pan` (set by BoardViewport).
- In `Render()`: draw op bounds = control's actual layout bounds (`new Rect(0, 0, Bounds.Width, Bounds.Height)`).

**`BoardDrawOperation.Render()` changes:**

```
canvas.Save();
canvas.Translate(pan.X, pan.Y);
canvas.Scale(zoom, zoom);

// Now in board space -- draw everything relative to (0,0)
DrawBackground(canvas);    // fills visible board rect with BgSurface
DrawDotGrid(canvas, ...);  // uses canvas.LocalClipBounds for visible region
DrawElements(canvas, ...); // unchanged -- already in board space
DrawOverlays(canvas, ...); // unchanged

canvas.Restore();
```

The background is drawn as a large rect covering the visible board area (derived from `canvas.LocalClipBounds`), not a fixed workspace. The dot grid already uses `canvas.LocalClipBounds` for visible-area-only rendering.

**`ICustomDrawOperation.Bounds`:** `new Rect(0, 0, controlWidth, controlHeight)` -- always matches the viewport size. This is correct for the Avalonia contract (canvas-local coordinates).

**`Equals()`:** Compares `Bounds`, `_board`, `_owner`, `_renderGeneration`, `_zoom`, `_pan` so the draw operation is re-rendered on pan/zoom changes.

### 3. BoardViewport

**Remove:** `ZoomBorder` field and all ZoomBorder setup. `TryCenterWorkspaceOrigin()`. `CanvasPointToBoard()`/`BoardPointToCanvas()` that use `StableOriginOffset`.

**Add:**
- State: `double Zoom` (default 1.0, range 0.2-3.0), `Vector2 Pan`.
- `ScreenToBoard(Point screenPoint)`: returns `(screenPoint - pan) / zoom` as Vector2.
- `BoardToScreen(Vector2 boardPoint)`: returns `boardPoint * zoom + pan` as Point.
- Mouse wheel handling for zoom: adjusts `Zoom`, zooming toward cursor position.
- On first layout with nonzero bounds, set `Pan = (Bounds.Width / 2, Bounds.Height / 2)` to center board origin.
- Forward `Zoom`/`Pan` to `_canvas.Zoom`/`_canvas.Pan` and call `InvalidateVisual()`.
- `event EventHandler? ZoomChanged` -- fired after zoom/pan updates so BoardView can update the zoom label/slider.
- `void SetZoom(double zoom, double centerX, double centerY)` -- programmatic zoom toward a screen point (used by BoardView zoom buttons/slider).
- `void PanBy(Vector2 delta)` -- adjusts pan by a screen-space delta (used by BoardView for hand-tool drag).

**Zoom-toward-cursor formula:**

```
pan = cursorScreen - (cursorScreen - oldPan) * (newZoom / oldZoom)
```

This keeps the point under the cursor stable during zoom.

**Pan via drag:**

```
pan += currentPointerScreen - previousPointerScreen
```

### 4. CoordinateTransformHelper

**Simplify:** Remove `originOffset` parameter. Formulas become:

```csharp
ScreenToBoard(screenPoint, pan, zoom) = (screenPoint - pan) / zoom
BoardToScreen(boardPoint, pan, zoom) = boardPoint * zoom + pan
```

### 5. BoardView.axaml.cs

**Changes:**
- Remove `viewport.ZoomBorder.ZoomChanged` subscription. Subscribe to `viewport.ZoomChanged` event instead.
- `SetZoom()`: calls `viewport.SetZoom(zoom, centerX, centerY)` instead of `zoomBorder.ZoomTo()`.
- Pointer coordinate conversion: `viewport.ScreenToBoard(e.GetPosition(viewport))` instead of `viewport.CanvasPointToBoard(e.GetPosition(viewport.Canvas))`. Note the position is now relative to `viewport` (the Border), not `viewport.Canvas`, since the canvas fills the viewport.
- Remove `viewport.Canvas` references in pointer handlers where possible; use `viewport` as the coordinate reference.
- `HandleZoomChanged`: reads `viewport.Zoom` instead of `zoomBorder.ZoomX`.

**Pointer-routing contract (who owns what):**

`BoardView` remains the owner of all pointer gesture routing. `BoardViewport` does NOT intercept pointer events for pan — it only handles mouse wheel zoom (a non-capturing event). Pan gestures are routed by `BoardView`:

- **Hand tool active + left-drag:** `BoardView` detects the hand tool is selected (via `_toolController.CurrentTool == BoardToolType.Hand`), captures the pointer, computes screen-space deltas between move events, and calls `viewport.PanBy(delta)`. This is the same pattern as the existing `return false` shortcut in `TryHandlePointer()` for the Hand tool — but now it actively pans instead of delegating to ZoomBorder.
- **Middle-click drag:** `BoardView` detects middle button in `HandlePointerPressed`, captures the pointer, and routes to `viewport.PanBy(delta)` during moves. Released on pointer up.
- **Tool gestures (left-click for non-hand tools):** Existing flow unchanged. `BoardView` captures, converts to board space via `viewport.ScreenToBoard()`, passes to `_toolController`.
- **Mouse wheel:** `BoardViewport` overrides `OnPointerWheelChanged` to adjust zoom toward cursor position. This does not conflict with BoardView's pointer capture since wheel events are separate from press/move/release.

### 6. BoardView.axaml

If `BoardViewport` in XAML references ZoomBorder in any way, update. (Currently the viewport is referenced by `x:Name="viewport"` only -- no XAML changes expected.)

## What Stays the Same

- **All element models** (BoardElement, StrokeElement, ShapeElement, etc.) -- board-space coordinates, no workspace awareness.
- **BoardToolController** -- receives board-space points, unchanged.
- **Network protocol** (BoardOperation, GameHost, GameClient) -- board-space operations, unchanged.
- **ElementDrawingHelper, ElementBoundsHelper, HitTestHelper** -- draw/test in board space after the transform, unchanged.
- **DotGridHelper** -- receives visible bounds as parameter, unchanged.
- **CollaboratorOverlayHelper** -- draws in board space, unchanged.
- **BoardFileStore** -- serializes board-space data, unchanged.

## Pan/Zoom Behavior

| Gesture | Owner | Action |
|---------|-------|--------|
| Mouse wheel | BoardViewport (OnPointerWheelChanged) | Zoom toward cursor (0.2x - 3.0x range) |
| Middle-click drag | BoardView (pointer capture) | Pan via `viewport.PanBy(delta)` |
| Hand tool + left-click drag | BoardView (pointer capture) | Pan via `viewport.PanBy(delta)` |
| Right-click drag | Not supported (removed with ZoomBorder) | -- |
| Zoom slider (BottomBar) | BoardView | `viewport.SetZoom(zoom, centerX, centerY)` |
| Zoom +/- buttons | BoardView | `viewport.SetZoom(zoom, centerX, centerY)` |

ZoomBorder currently handles mouse wheel zoom and right-click pan. Right-click pan is intentionally removed — pan is now middle-click or hand-tool. This avoids conflict with potential future right-click context menus.

## Dot Grid Behavior

The dot grid is drawn for the visible board-space region only (via `canvas.LocalClipBounds` after the zoom/pan transform). It extends as far as the user pans -- there is no boundary. Beyond visible content, the background is `BgSurface` (#0D0D0D) with dots. This creates the "infinite canvas" feel: the grid is always there, content can be placed anywhere.

## Risk and Mitigation

| Risk | Mitigation |
|------|------------|
| ZoomBorder had smooth animations, kinetic panning, touch support | Basic wheel zoom + drag pan first. Touch/animation can be added later. |
| Pointer capture/release changes | Existing pointer handling structure in BoardView stays; only coordinate conversion changes. |
| Tests reference StableWorkspaceSize/StableOriginOffset | Update to use new API. These tests are isolated to 4 files. |
| Third-party ZoomBorder removal | No other code uses ZoomBorder; it's only instantiated in BoardViewport. |

## Test Plan

### Update existing tests
1. **CanvasMathTests** -- Remove/update assertions on `StableWorkspaceSize`/`StableOriginOffset`. Add tests for `ScreenToBoard`/`BoardToScreen` round-trips. Test zoom-toward-cursor formula.
2. **PointerToToolTests** -- Update to use new `ScreenToBoard` API.
3. **DotGridRenderingTests** -- Remove hardcoded `200_000` literals; use realistic visible bounds.
4. **BoardScreenLayoutTests** -- Update zoom range assertions if API surface changes.

### New tests
5. **BoardViewport zoom/pan state** -- Initial pan centers origin. Zoom clamp 0.2-3.0. Pan updates correctly.
6. **Coordinate round-trip** -- `ScreenToBoard(BoardToScreen(p)) == p` for various zoom/pan values.

## Files Changed (Summary)

| File | Action |
|------|--------|
| `src/BFGA.Canvas/BFGA.Canvas.csproj` | Remove PanAndZoom NuGet package reference |
| `src/BFGA.Canvas/Rendering/BoardSurfaceHelper.cs` | Remove workspace constants, keep GetBoardBounds |
| `src/BFGA.Canvas/BoardCanvas.cs` | Remove fixed size, add Zoom/Pan, viewport-sized draw op |
| `src/BFGA.Canvas/BoardViewport.cs` | Remove ZoomBorder, add zoom/pan state + pointer handling |
| `src/BFGA.Canvas/Rendering/CoordinateTransformHelper.cs` | Remove originOffset parameter |
| `src/BFGA.App/Views/BoardView.axaml.cs` | Update zoom/pointer APIs |
| `tests/BFGA.Core.Tests/CanvasMathTests.cs` | Update for new API |
| `tests/BFGA.Core.Tests/PointerToToolTests.cs` | Update for new API |
| `tests/BFGA.Core.Tests/DotGridRenderingTests.cs` | Remove 200K literals |
| `tests/BFGA.App.Tests/BoardScreenLayoutTests.cs` | Update zoom assertions |
