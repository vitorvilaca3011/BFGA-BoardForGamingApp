# Technology Stack

**Analysis Date:** 2026-04-15

## Languages

**Primary:**
- C# (latest LangVersion via .NET 9 SDK) - All source code across 4 projects

**Secondary:**
- AXAML (Avalonia XAML) - UI markup in `src/BFGA.App/Views/*.axaml`, `src/BFGA.App/Styles/*.axaml`

## Runtime

**Environment:**
- .NET 9.0 (`net9.0` TFM in all `.csproj` files)
- Nullable reference types enabled globally (`<Nullable>enable</Nullable>`)
- Implicit usings enabled globally (`<ImplicitUsings>enable</ImplicitUsings>`)

**Package Manager:**
- NuGet (via `dotnet restore` / Visual Studio)
- No `global.json` detected — uses whatever .NET 9 SDK is installed
- No `Directory.Build.props` — each project manages its own properties

## Frameworks

**Core:**
- Avalonia UI 11.3.12 - Cross-platform desktop UI framework (replaces WPF)
  - `Avalonia` - Core framework
  - `Avalonia.Desktop` - Desktop platform support
  - `Avalonia.Themes.Fluent` - Fluent design theme
  - `Avalonia.Skia` - SkiaSharp rendering backend
- SkiaSharp 3.116.1 - 2D graphics rendering engine for canvas/board drawing

**Networking:**
- LiteNetLib 2.1.0 - UDP networking library for peer-to-peer game sessions

**Serialization:**
- MessagePack 2.5.198 - Binary serialization for board state and network protocol
  - Source generator disabled (`<MessagePackDisableSourceGenerator>true</MessagePackDisableSourceGenerator>`)
  - Uses `DynamicUnionResolver` for polymorphic union types

**Testing:**
- xUnit 2.9.2 - Test framework
- xunit.runner.visualstudio 2.8.2 - VS test adapter
- Microsoft.NET.Test.Sdk 17.12.0 - Test host infrastructure

**Build/Dev:**
- Visual Studio 2022 (Solution format v17)
- MSBuild via `dotnet build`

## Key Dependencies

**Critical (Runtime):**
- `Avalonia` 11.3.12 - UI framework; entire app built on it
- `SkiaSharp` 3.116.1 - Canvas rendering engine; board drawing, element rendering
- `LiteNetLib` 2.1.0 - UDP networking; multiplayer host/client sessions
- `MessagePack` 2.5.198 - Binary serialization; board file persistence + network protocol

**Testing:**
- `xunit` 2.9.2 - Test assertions and test framework
- `Microsoft.NET.Test.Sdk` 17.12.0 - Test execution infrastructure

**Standard Library Usage (no NuGet):**
- `System.Text.Json` - Settings file serialization (`src/BFGA.App/Services/SettingsService.cs`)
- `System.Numerics.Vector2` - 2D coordinates throughout board/canvas/network code
- `System.Collections.Concurrent` - Thread-safe collections in `GameHost`

## Solution Structure

```
BFGA.sln
├── src/
│   ├── BFGA.Core          # Domain models, serialization, file I/O (class library)
│   ├── BFGA.Network       # P2P networking protocol, host/client (class library)
│   ├── BFGA.Canvas        # SkiaSharp rendering, tools, viewport (class library)
│   └── BFGA.App           # Avalonia desktop app, MVVM views/viewmodels (WinExe)
├── tests/
│   ├── BFGA.Core.Tests    # xUnit tests for Core
│   ├── BFGA.Network.Tests # xUnit tests for Network
│   └── BFGA.App.Tests     # xUnit tests for App
```

**Project dependency graph:**
```
BFGA.App → BFGA.Core, BFGA.Network, BFGA.Canvas
BFGA.Canvas → BFGA.Core, BFGA.Network
BFGA.Network → BFGA.Core
BFGA.Core → (no project refs)
```

## Configuration

**Application Settings:**
- Stored as JSON at `%APPDATA%/BFGA/settings.json` via `src/BFGA.App/Services/SettingsService.cs`
- Settings: GridOpacity, Language, DefaultImageFolder, AutosaveEnabled, AutosaveIntervalSeconds
- No appsettings.json — this is a desktop app, not a web app

**Environment Variables:**
- None detected. No `.env` files present.

**Build Configuration:**
- `src/BFGA.App/BFGA.App.csproj`: `<OutputType>WinExe</OutputType>`
- `src/BFGA.App/app.manifest`: Windows 10+ compatibility manifest
- `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` for perf
- Debug|Release configurations for Any CPU, x64, x86

## Build Commands

```bash
dotnet build BFGA.sln              # Build all projects
dotnet run --project src/BFGA.App  # Run the desktop app
dotnet test                        # Run all tests
dotnet test --filter "FullyQualifiedName~BFGA.Core.Tests"  # Run specific test project
```

**Batch helper:**
- `runtests.bat` - Hardcoded path to run BFGA.Core.Tests (worktree-specific, not portable)

## Platform Requirements

**Development:**
- .NET 9 SDK
- Visual Studio 2022+ or any editor with .NET support
- Windows 10+ (app.manifest targets Win10; Avalonia supports cross-platform but manifest is Windows-only)

**Production / Target:**
- Desktop application (WinExe output type)
- Cross-platform via Avalonia (Windows, macOS, Linux)
- No server deployment — purely client-side with P2P networking
- Default network port: 7777 (UDP via LiteNetLib)

## Serialization Architecture

**MessagePack configuration** (`src/BFGA.Core/MessagePackSetup.cs`):
- Custom formatters: `Vector2Formatter`, `SKColorFormatter` (in `src/BFGA.Core/Serialization/`)
- Union resolver: `DynamicUnionResolver` handles polymorphic `BoardElement` and `BoardOperation` types
- Security: `MessagePackSecurity.UntrustedData` enabled for network deserialization safety
- Shared options: `NetworkMessagePackSetup` in BFGA.Network delegates to Core's setup

**Two serialization paths:**
1. **File persistence**: `BoardFileStore` → MessagePack → `.bfga` board files (local filesystem)
2. **Network protocol**: `OperationSerializer` → MessagePack → UDP packets via LiteNetLib

---

*Stack analysis: 2026-04-15*
