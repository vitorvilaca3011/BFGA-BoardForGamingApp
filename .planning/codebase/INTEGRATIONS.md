# External Integrations

**Analysis Date:** 2026-04-15

## APIs & External Services

**None.** This is a self-contained desktop application with no cloud APIs, REST endpoints, or external web services. All functionality runs locally or via LAN peer-to-peer networking.

## Networking (P2P)

**LiteNetLib UDP Networking:**
- Purpose: Real-time multiplayer whiteboard sessions over LAN
- SDK: `LiteNetLib` 2.1.0 (NuGet)
- Protocol: Custom binary protocol using MessagePack serialization
- Architecture: Host/Client model (not dedicated server)
  - Host: `src/BFGA.Network/GameHost.cs` — listens on UDP port (default 7777)
  - Client: `src/BFGA.Network/GameClient.cs` — connects to host
- Channels:
  - Channel 0: Reliable ordered (board operations, sync, peer join/leave)
  - Channel 1: Unreliable (cursor updates at ~60/sec)
- Auth: Display name sent during connection handshake. No authentication or encryption.
- Session factory pattern: `src/BFGA.App/Networking/IGameSessionFactory.cs` → `NetworkGameSessionFactory.cs`

**Network Protocol Operations** (`src/BFGA.Network/Protocol/BoardOperation.cs`):
- AddElement, UpdateElement, DeleteElement, MoveElement
- CursorUpdate, DrawStrokePoint, CancelStroke
- RequestFullSync, FullSyncResponse
- PeerJoined, PeerLeft
- Undo, Redo

## Data Storage

**Board Files:**
- Format: MessagePack binary serialization of `BoardState`
- I/O: `src/BFGA.Core/BoardFileStore.cs`
- Safe write: atomic temp-file + replace pattern
- Location: User-chosen via file dialog (`src/BFGA.App/Services/AvaloniaFileDialogService.cs`)

**Application Settings:**
- Format: JSON via `System.Text.Json`
- Location: `%APPDATA%/BFGA/settings.json`
- Service: `src/BFGA.App/Services/SettingsService.cs`
- Debounced save (500ms) to avoid rapid I/O
- Properties: GridOpacity, Language, DefaultImageFolder, AutosaveEnabled, AutosaveIntervalSeconds

**Databases:**
- None. No SQLite, no EF Core, no database of any kind.

**File Storage:**
- Local filesystem only. No cloud storage integration.

**Caching:**
- `src/BFGA.Canvas/Rendering/ImageDecodeCache.cs` — in-memory SkiaSharp image decode cache for `ImageElement` rendering

## Authentication & Authorization

**No authentication system.** Players connect to hosts using display names only. No user accounts, tokens, or identity providers.

**Player identity:**
- `Guid ClientId` assigned by host upon connection (`src/BFGA.Network/GameHost.cs`)
- `PlayerInfo` stores DisplayName + AssignedColor (`src/BFGA.Network/PlayerInfo.cs`)
- Color assignment: Rotating palette of 8 colors from `GameHost.PlayerColors`

## Monitoring & Observability

**Logging:**
- `System.Diagnostics.Debug.WriteLine()` — Network events in `GameHost` and `GameClient` (debug-only, stripped from release)
- `BoardDebugLogger` (`src/BFGA.App/Services/BoardDebugLogger.cs`) — Optional file-based structured logging
  - Writes to `%DOCUMENTS%/BFGA/logs/board-debug-*.log`
  - Auto-prunes to 5 most recent log files
  - UTC timestamps, `[EventName] Message` format
  - Created conditionally via `CreateIfEnabled()`

**Metrics/Tracing:**
- None. No OpenTelemetry, no APM, no structured metrics.

**Health Checks:**
- None. Desktop app — no HTTP health endpoint.

**Error Tracking:**
- None. No Sentry, Application Insights, or crash reporting.
- Errors generally caught and silently handled (settings load, network message parsing)

## CI/CD & Deployment

**Hosting:**
- Desktop application — distributed as executable, not hosted
- No installer detected (no WiX, MSIX, or InnoSetup config)

**CI Pipeline:**
- None detected. No `.github/workflows/`, no Azure Pipelines YAML, no Jenkinsfile.

**Deployment:**
- Manual `dotnet build` / `dotnet publish`
- No automated release pipeline

## Clipboard Integration

**Avalonia Clipboard:**
- `src/BFGA.App/Services/IClipboardService.cs` — abstraction interface
- `src/BFGA.App/Services/AvaloniaClipboardService.cs` — implementation using Avalonia's clipboard API
- Used for copy/paste of board elements

## File Dialog Integration

**Avalonia File Dialogs:**
- `src/BFGA.App/Services/IFileDialogService.cs` — abstraction interface
- `src/BFGA.App/Services/AvaloniaFileDialogService.cs` — implementation using Avalonia's storage API
- Used for Open/Save board files and image import

## Webhooks & Callbacks

**Incoming:** None
**Outgoing:** None

## Environment Configuration

**Required env vars:** None
**Secrets:** None — no API keys, no cloud credentials
**Network config:** UDP port 7777 (configurable at host start)

---

*Integration audit: 2026-04-15*
