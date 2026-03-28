# Whiteboard Application UI Patterns Reference

**Research Date:** 2026-03-27  
**Context:** BFGA (Board For Gaming App) redesign from combined connection/board screen to separate lobby and canvas screens  
**Sources:** Excalidraw, tldraw, FigJam, Miro, official documentation and GitHub repositories

---

## 1. Overall Layout Patterns

### 1.1 Board Screen Structure

All major whiteboard apps share a similar high-level layout that maximizes canvas space while keeping essential controls accessible:

```
┌──────────────────────────────────────────────────────────────────────┐
│  [Menu]                              [Collab] [Share] [Theme]       │ ← Top Bar (48px)
├─────┬───────────────────────────────────────────────────────────────┤
│     │                                                               │
│ T   │                                                               │
│ O   │                                                               │
│ O   │                    INFINITE CANVAS                           │
│ L   │                                                               │
│ S   │                                                               │
│     │                                                               │
├─────┴───────────────────────────────────────────────────────────────┤
│  [Zoom: -] [100%] [+]                              [Minimap]        │ ← Bottom Bar (40px)
└──────────────────────────────────────────────────────────────────────┘
```

### 1.2 Toolbar Placement

| App | Primary Toolbar | Secondary | Notes |
|-----|-----------------|-----------|-------|
| **Excalidraw** | Left edge, vertical | Top-right for actions | Floating toolbar, draggable |
| **tldraw** | Bottom-center, horizontal | Top-right for style panel | Responsive: moves to left on tablet |
| **FigJam** | Left edge, vertical | Top for menu | Integrated with Figma ecosystem |
| **Miro** | Left edge, vertical | Top for search/menu | Collapsible sidebar |

**Recommendation for BFGA:**
- **Primary toolbar:** Left edge, vertical (48-56px wide)
  - Follows established convention
  - Works well with Avalonia's control placement
  - Natural thumb zone on tablets
- **Style/properties panel:** Top-right, floating
- **Bottom bar:** Zoom controls, minimap toggle

### 1.3 Tool Organization

Tools are typically grouped by function:

```
┌──────┐
│ Select│ ← Navigation (hand, select, zoom)
│ Hand  │
│ Zoom  │
├──────┤
│ Pen   │ ← Drawing (pen, highlighter, eraser)
│ Marker│
│Eraser │
├──────┤
│  ▢   │ ← Shapes (rectangle, ellipse, diamond)
│  ○   │
│  ◇   │
│  →   │
│  /   │
├──────┤
│  T   │ ← Content (text, sticky note, image)
│  📝  │
│  🖼️  │
└──────┘
```

**Key Insight:** Grouping tools by category with visual separators improves discoverability. tldraw uses a "Tool Dock" pattern with keyboard navigation support.

### 1.4 Canvas Background Patterns

| Pattern | Visual | Use Case |
|---------|--------|----------|
| **Dots** | Small circles (2-4px) spaced 20-30px apart | Default in tldraw, Excalidraw - provides spatial reference without visual noise |
| **Grid** | Lines forming squares (20-50px) | Technical drawing, alignment help |
| **Lines** | Horizontal ruled lines | Note-taking, writing apps |
| **Solid** | Single color fill | Clean presentation, exported content |
| **None** | Pure white/black | Maximum canvas space |

**Implementation Notes (from tldraw):**
- Background is rendered as a separate layer
- Grid scales with zoom level to maintain visual consistency
- Excalidraw uses a "hand-drawn" grid style that wobbles slightly

**Recommendation for BFGA:**
- Default: **Dot pattern** (modern, clean aesthetic)
- Options: Grid, Lines, None (in settings)
- Dot spacing: 24px, dot size: 2px
- Colors: Light gray (#E0E0E0) on light theme, dark gray (#404040) on dark theme

---

## 2. Tool Palette Design

### 2.1 Standard Tool Set

| Category | Tools | Shortcut |
|----------|-------|----------|
| **Selection** | Select, Lasso | V, Escape |
| **Navigation** | Hand (pan), Zoom | H, Z |
| **Drawing** | Pen, Pencil, Highlighter | P, O, H (when combined) |
| **Shapes** | Rectangle, Ellipse, Diamond, Arrow, Line | R, E, D, A, L |
| **Text** | Text, Sticky Note | T, N |
| **Media** | Image, Embed | I, E |
| **Utility** | Eraser, Undo, Redo | X, Ctrl+Z, Ctrl+Shift+Z |

### 2.2 Tool Button Design

**Visual States:**
```
┌────────────┐
│            │  Default: Transparent/light background
│    Icon    │  Hover: Subtle highlight (#F5F5F5)
│            │  Active/Selected: Filled background (#E8E8E8), accent border
│            │  Disabled: 50% opacity
└────────────┘
```

**Best Practices:**
- Icon size: 24x24px with 12px padding
- Tooltip on hover (200ms delay)
- Keyboard shortcut displayed in tooltip
- Active tool has accent color indicator (2px left border or filled background)

### 2.3 Sub-Options and Flyouts

When a tool is selected, additional options appear:

```
┌─────────────────────────────────────┐
│  [Pen ▼]  [Color] [Stroke: 2px ▼]  │
└─────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────┐
│  [Pen]   [Color] [Stroke]           │  ← Pen options
│  ├─ Pen (solid)                     │
│  ├─ Pencil (textured)                │  ← Flyout menu
│  └─ Highlighter (transparent)       │
└─────────────────────────────────────┘
```

**Implementation Pattern (tldraw):**
- Tool options appear in a horizontal sub-toolbar below the main toolbar
- Flyout menus appear on click/dropdown
- Options panel slides in from right for detailed settings

### 2.4 Keyboard Shortcuts Convention

| Category | Pattern | Example |
|----------|---------|---------|
| **Tools** | Single letter | V=Select, P=Pen, R=Rectangle |
| **Modifiers** | Shift+key | Shift+drag = constrain proportions |
| **Actions** | Ctrl/Cmd+letter | Ctrl+C=Copy, Ctrl+Z=Undo |
| **Quick Access** | Double-tap | Double-tap tool for tool options |

**BFGA Recommendation:**
- Follow industry conventions (V, P, R, T, H, Z, X for tools)
- Display shortcuts in tooltips
- Provide keyboard shortcut reference dialog (accessible via ? or F1)

---

## 3. Canvas Area

### 3.1 Infinite Canvas Behavior

All whiteboard apps implement infinite canvas with similar behaviors:

| Behavior | Implementation |
|----------|---------------|
| **Pan** | Middle-mouse drag, Space+drag, Two-finger scroll, Hand tool |
| **Zoom** | Scroll wheel, Pinch gesture, Zoom buttons, Ctrl++/- |
| **Zoom Range** | 10% to 6400% (tldraw default) |
| **Zoom to Fit** | Double-click hand tool or keyboard shortcut |
| **Reset View** | Home key or button to return to 100% and center |

### 3.2 Grid/Background Implementation

**tldraw's Approach:**
- Grid rendered as a drawable component
- Uses level-of-detail (LOD) - grid simplifies at low zoom
- Background is separate from canvas content (easier for export)

**BFGA Implementation Notes:**
```
Canvas Layers (bottom to top):
1. Background Layer    → Grid pattern, solid color
2. Content Layer       → Board elements (strokes, shapes, text)
3. UI Layer           → Selection handles, context menus
4. Overlay Layer      → Collaborator cursors, chat bubbles
```

### 3.3 Zoom Controls

**Typical UI:**
```
┌─────────────────────────────────────────────────────┐
│  [−]  [Auto] [+]              [1:1]  [Fit]  [≡]   │
└─────────────────────────────────────────────────────┘
        │        │      │          │      │    │
        ▼        ▼      ▼          ▼      ▼    ▼
    Zoom out  Auto-fit Zoom in  % zoom  Fit   Menu
                        to content
```

**Zoom Percentage Display:**
- Shows current zoom level (e.g., "150%", "50%")
- Click to manually enter percentage
- Auto-fit: Zooms to show all content with padding

### 3.4 Minimap

**Purpose:** Overview of entire canvas showing:
- Current viewport position (highlighted rectangle)
- All content placement
- Quick navigation by clicking

**Implementation (tldraw):**
- OpenGL-rendered for performance
- Toggle in bottom-right corner
- Semi-transparent overlay
- Click to navigate

**BFGA Recommendation:**
- Implement minimap as optional feature (toggle in bottom bar)
- Show viewport indicator
- 150x100px default size
- Draggable viewport rectangle for quick navigation

---

## 4. Collaboration UI Overlays

### 4.1 Remote Cursor Display

**tldraw's Implementation:**
```
User Color Palette:
#FF802B, #EC5E41, #F2555A, #F04F88, #E34BA9, #BD54C6,
#9D5BD2, #7B66DC, #02B1CC, #11B3A3, #39B178, #55B467
```

**Cursor Rendering:**
- Arrow icon in user's assigned color
- Name label below cursor (white text on colored pill)
- Smooth interpolation between position updates (60fps)
- Fade out after 3-5 seconds of inactivity
- Hide when cursor leaves canvas bounds

**Excalidraw's Approach:**
- Similar colored cursors with name labels
- Shows active tool icon next to cursor
- In-progress strokes visible to other users (optimistic rendering)

### 4.2 Presence Indicators

**User Avatar Display (Top Bar):**
```
┌─────────────────────────────────────────────┐
│  [Menu]     [User1] [User2] [User3] [+]   │
│                      ↑        ↑      ↑     │
│                      └────────┬───────────┘
│                               │
│                    Stacked avatars with
│                    overlapping circles
└────────────────────────────────────────────┘
```

- Circular avatars (24-32px)
- Colored border matching user's assigned color
- Tooltip shows full name on hover
- Clicking opens user list popover

### 4.3 Connection Status

| State | Visual | Behavior |
|-------|--------|----------|
| **Connected** | Green dot or checkmark | Normal operation |
| **Connecting** | Yellow dot, spinner | Show "Connecting..." |
| **Disconnected** | Red dot | Show reconnect button, queue local changes |
| **Offline** | Gray dot | Local-only mode indicator |

**BFGA Recommendation:**
- Show connection status in top bar (subtle indicator)
- Display toast notification on connect/disconnect
- Queue operations when offline, sync on reconnect

### 4.4 In-Progress Stroke Visibility

**Pattern:** Show remote users' current strokes before they're finalized:
- Stroke appears as user draws
- Different opacity (50-70%) to distinguish from committed content
- Transitions to full opacity when stroke completes

---

## 5. Property Panels / Context Menus

### 5.1 Property Panel Design

**tldraw's Style Panel (Top-Right):**
```
┌──────────────────────────┐
│ Style              [✕]   │
├──────────────────────────┤
│ Fill      [███████] #FFF │
│ Stroke    [██████] #000 │
│ Stroke W  [2px ▼]        │
│ Opacity   [100% ▼]       │
│ Font      [Inter ▼]      │
│ Text Size [16px ▼]       │
└──────────────────────────┘
         │
         ▼
┌──────────────────────────┐
│ Color picker with:       │
│ ┌──────────────────────┐ │
│ │ ■ ■ ■ ■ ■ ■ ■ ■ ■ ■  │ │  ← Preset colors
│ │ ■ ■ ■ ■ ■ ■ ■ ■ ■ ■  │ │
│ └──────────────────────┘ │
│ ┌──────────────────────┐ │
│ │ Custom picker (HSL)   │ │
│ └──────────────────────┘ │
│ [Hex input: #________]  │
└──────────────────────────┘
```

### 5.2 Context Menu (Right-Click)

```
┌────────────────────────────────┐
│ Cut              Ctrl+X       │
│ Copy             Ctrl+C       │
│ Paste            Ctrl+V       │
│ ───────────────────────────── │
│ Bring to Front                │
│ Send to Back                  │
│ ───────────────────────────── │
│ Duplicate        Ctrl+D      │
│ Delete           Del         │
│ ───────────────────────────── │
│ Group           Ctrl+G       │
│ Ungroup         Ctrl+Shift+G │
│ ───────────────────────────── │
│ Lock                         │
│ ───────────────────────────── │
│ Properties...                │
└────────────────────────────────┘
```

### 5.3 Floating vs. Docked Panels

| Approach | Pros | Cons |
|----------|------|------|
| **Floating** | Flexible, doesn't consume canvas | Can obscure content, floating debris |
| **Docked** | Always accessible, clean | Reduces canvas space |

**tldraw's Hybrid Approach:**
- Style panel docks to right side on wide screens (>1000px)
- Becomes modal overlay on narrow screens
- Contextual toolbar appears near selection

**BFGA Recommendation:**
- **Style panel:** Docked right side (250px wide), collapsible
- **Context toolbar:** Appears near selection for quick actions
- **Context menu:** Right-click for full options

---

## 6. Clean/Minimal Aesthetic

### 6.1 Visual Design Tokens

**Color Palette (Light Theme):**
| Role | Color | Usage |
|------|-------|-------|
| Background | #FFFFFF | Canvas |
| Surface | #F5F5F5 | Panels, toolbars |
| Surface Hover | #EBEBEB | Button hover |
| Border | #E0E0E0 | Dividers, outlines |
| Text Primary | #1A1A1A | Main text |
| Text Secondary | #6B6B6B | Labels, hints |
| Accent | #2563EB | Selected items, active state |

**Typography:**
- Font: System UI / Inter (modern, readable)
- Sizes: 12px (small), 14px (body), 16px (headings)
- Weight: 400 (normal), 500 (medium), 600 (semibold)

**Spacing:**
- Base unit: 4px
- Toolbar padding: 8px (2 units)
- Button padding: 8px 12px
- Panel padding: 16px
- Gap between elements: 8px

### 6.2 Icon Style

**Characteristics:**
- 24x24px base size
- 2px stroke weight
- Rounded corners (2px radius)
- Filled variants for active states
- Consistent 4px padding inside container

**Iconography Sources:**
- Phosphor Icons (used by tldraw)
- Lucide (open source, Excalidraw)
- Custom-drawn for hand-drawn aesthetic

### 6.3 Chrome Reduction

**Techniques Used:**
1. **Semi-transparent overlays** - Panels have slight transparency (95% opacity)
2. **Shadow layering** - Subtle drop shadows (0 2px 8px rgba(0,0,0,0.1))
3. **Rounded corners** - 8px for panels, 4px for buttons
4. **Minimal borders** - Only where necessary for separation
5. **Hide chrome when idle** - Auto-hide toolbars after inactivity (optional)
6. **Focus on content** - UI retreats to edges, canvas dominates

### 6.4 Touch/Input Modality

**Considerations:**
- Large hit targets (44x44px minimum for touch)
- Floating panels don't block canvas
- Gesture support (pinch zoom, two-finger pan)
- Tool palettes adapt to input type

---

## 7. BFGA-Specific Recommendations

### 7.1 Screen Layout for BFGA

**Lobby/Connection Screen:**
```
┌────────────────────────────────────────────────────────┐
│                    BFGA                                │
│              Board For Gaming App                      │
├────────────────────────────────────────────────────────┤
│                                                         │
│   ┌─────────────────────────────────────────────────┐  │
│   │  Room Code: [________]                         │  │
│   │                                                 │  │
│   │  [Create Room]     [Join Room]                  │  │
│   └─────────────────────────────────────────────────┘  │
│                                                         │
│   Your Name: [________________]                       │
│                                                         │
└────────────────────────────────────────────────────────┘
```

**Board Screen:**
```
┌─────────────────────────────────────────────────────────────┐
│ [BFGA] Room: XXXX    [Users: ●●●]        [≡] [☀/🌙]      │
├─────┬───────────────────────────────────────────────┬─────┤
│     │                                               │     │
│ [V] │                                               │ S   │
│ [H] │           INFINITE CANVAS                     │ T   │
│ [Z] │           (SkiaSharp Rendered)                │ Y   │
│     │                                               │ L   │
│─────│                                               │ E   │
│ [P] │                                               │     │
│ [R] │                                               │     │
│ [E] │                                               │     │
│     │                                               │     │
│─────│                                               │     │
│ [T] │                                               │     │
│ [I] │                                               │     │
│     │                                               │     │
├─────┴───────────────────────────────────────────────┴─────┤
│  [−] [100%] [+]      [Grid ▼]              [Minimap]    │
└─────────────────────────────────────────────────────────────┘
```

### 7.2 Implementation Priorities

**Phase 1 - Core Canvas:**
1. Infinite canvas with pan/zoom
2. Basic tool palette (select, pan, shapes, text)
3. Dot grid background
4. Zoom controls in bottom bar

**Phase 2 - Polish:**
1. Style panel (colors, stroke width)
2. Context menus
3. Keyboard shortcuts
4. Dark theme support

**Phase 3 - Collaboration:**
1. Remote cursor display
2. User presence indicators
3. Connection status UI
4. In-progress stroke visibility

### 7.3 Technical Considerations (Avalonia/SkiaSharp)

| Feature | Implementation Approach |
|---------|------------------------|
| **Infinite Canvas** | Transform matrix for pan/zoom, render visible region only |
| **Grid Background** | SKCanvas draw loop with transform, dot pattern |
| **Tool Rendering** | Icons as DrawingImage or custom SKPath |
| **Cursors** | Custom SKElement overlay for remote cursors |
| **Panels** | Native Avalonia controls with custom styling |
| **Dark Theme** | ResourceDictionary with color overrides |

### 7.4 Reference SkiaSharp Patterns

From tldraw's approach (WebGL/OpenGL optimized):
- Render grid at current zoom level (simplify when zoomed out)
- Use layers: Background → Content → UI → Cursors
- Batch similar draw operations
- Cache static elements (grid, backgrounds)
- Invalidate only changed regions when possible

---

## 8. Appendix: Layout Diagrams

### Excalidraw Layout
```
┌──────────────────────────────────────────────────────────────┐
│  [Menu]                              [Collab] [Share] [⚙] │
├─────┬────────────────────────────────────────────────────────┤
│     │                                                        │
│ [V] │                                                        │
│ [H] │              CANVAS                                    │
│     │                                                        │
│─────│                                                        │
│[▢○◇]│                                                        │
│     │                                                        │
│─────│                                                        │
│[T]  │                                                        │
│[P]  │                                                        │
│     │                                                        │
├─────┴────────────────────────────────────────────────────────┤
│                                         [Zoom: -][100%][+]  │
└──────────────────────────────────────────────────────────────┘
     ↑                                                      ↑
  Toolbar (left edge)                              Zoom (bottom right)
```

### tldraw Layout
```
┌──────────────────────────────────────────────────────────────┐
│ [Menu]  Page 1                        [Users] [Share] [⚙] │
├────────┬────────────────────────────────────────┬───────────┤
│        │                                        │           │
│        │                                        │  Style    │
│        │           CANVAS                      │  Panel    │
│        │                                        │           │
│        │                                        │           │
├────────┴────────────────────────────────────────┴───────────┤
│ [Tools]  [Pen] [Shapes] [Text] [Eraser]    [Zoom] [?]      │
└──────────────────────────────────────────────────────────────┘
                      ↑
               Toolbar (bottom center)
```

### Miro Layout
```
┌──────────────────────────────────────────────────────────────┐
│ [ Miro ] [Search...]  [Cards] [Frames] ...      [Profile]  │
├─────┬───────────────────────────────────────────────────────┤
│     │                                                        │
│[🔍] │                                                        │
│[✋] │                                                        │
│[🖊️] │              CANVAS                                    │
│[▢] │                                                        │
│[○] │                                                        │
│[T] │                                                        │
│[📝]│                                                        │
│     │                                                        │
│─────│                                                        │
│[🤝]│                                                        │
│     │                                                        │
├─────┴───────────────────────────────────────────────────────┤
│  [-] [100%] [+]                    [Map] [Board] [Share]   │
└──────────────────────────────────────────────────────────────┘
```

---

## 9. Sources & Further Reading

- **Excalidraw:** https://github.com/excalidraw/excalidraw
- **Excalidraw Docs:** https://docs.excalidraw.com
- **tldraw:** https://github.com/tldraw/tldraw
- **tldraw SDK Docs:** https://tldraw.dev
- **tldraw UI Components:** https://tldraw.dev/sdk-features/ui-components
- **tldraw Cursors:** https://tldraw.dev/sdk-features/cursors
- **tldraw Collaboration:** https://tldraw.dev/sdk-features/collaboration

---

*This document should be updated as the BFGA implementation progresses and new insights are gained from building and testing.*
