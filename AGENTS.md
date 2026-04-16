lecAGENTS
======

Purpose
-------
This file documents: build / lint / test commands, how to run a single test, and repository coding style guidelines that automated agentic coders should follow when making changes in this repository.

Findings
--------
- Repository scanned on 2026-03-25. No package.json, .csproj, pyproject.toml, go.mod, or other language-specific project files were detected at the repository root. Only Git metadata and an OpenCode memory folder were present.
- No Cursor rules (.cursor/rules/ or .cursorrules) were found.
- No Copilot instructions file (.github/copilot-instructions.md) was found.

If you add a project manifest (package.json, *.csproj, pyproject.toml, go.mod, etc.), append concrete commands for the chosen stack to this file.

Agent Safety & Conduct
----------------------
- Never commit secrets, credentials, or private keys. If a change requires secrets, request them from the user or use secure vaults.
- Run formatters and linters before creating a commit/PR. Do not bypass hooks unless explicitly asked.
- If a change is large or architectural, create a short design note in the PR and request a review from a human maintainer.
- Run tests locally (or via CI) and include test output when asking for help debugging.

Build / Lint / Test Commands (Generic)
-------------------------------------
The repository currently contains no language-specific manifests. Below are recommended, copy-pasteable commands for common stacks. Use only those that match this repository once a manifest is present.

Node / JavaScript / TypeScript (npm / yarn / pnpm)
- Install: npm ci  # or yarn install / pnpm install
- Dev server: npm run dev
- Build: npm run build
- Lint: npm run lint  # expects eslint configuration
- Format: npm run format  # expects prettier or similar
- Test (all): npm test  # maps to jest / vitest / mocha depending on package.json
- Test (single Jest): npx jest path/to/file.test.ts -t "name of test"
- Test (single Vitest): npx vitest run path/to/file.test.ts -t "name of test"

.NET (dotnet)
- Build: dotnet build
- Run: dotnet run --project <Project.csproj>
- Test (all): dotnet test
- Test (single test): dotnet test --filter "FullyQualifiedName~Namespace.ClassName.TestName" or use --filter "TestName=TestName"
- Format: dotnet format

Python (pytest)
- Install deps: python -m pip install -r requirements.txt
- Test (all): pytest
- Test (single test function): pytest path/to/test_file.py::test_function_name -q
- Lint/format: ruff . && black .

Go
- Build: go build ./...
- Test (all): go test ./...
- Test (single): go test ./pkg/path -run TestName
- Format: go fmt ./...

Java (Maven/Gradle)
- Maven build: mvn -B -DskipTests package
- Maven test single: mvn -Dtest=MyTest#testMethod test
- Gradle build: ./gradlew build
- Gradle test single: ./gradlew test --tests "com.example.MyTest.testMethod"

Running a single test — quick reference
-------------------------------------
- Jest: npx jest path/to/file.test.ts -t "test name regex"
- Vitest: npx vitest run path/to/file.test.ts -t "test name regex"
- dotnet: dotnet test --filter "FullyQualifiedName~Namespace.Class.TestName"
- pytest: pytest tests/test_foo.py::test_name -q
- go: go test ./pkg/name -run TestName
- maven: mvn -Dtest=MyTest#testMethod test
- gradle: ./gradlew test --tests "com.example.MyTest.testMethod"

Editor / LSP / Diagnostics
--------------------------
- Run language server diagnostics before pushing: (TypeScript) npx tsc --noEmit or (C#) use dotnet build and lsp diagnostics tools.
- Keep CI green: ensure linters and a core test-suite run in the PR pipeline.

Code Style Guidelines
---------------------
These rules are intended to be concrete and conservative so agentic coders produce cohesive changes.

Formatting & Tooling
- Use a shared formatter (Prettier for JS/TS, Black for Python, dotnet format for C#) and ensure formatting is applied to any modified files before committing.
- Line length: prefer 100 characters; allow 120 for long strings and generated files.
- Indentation: spaces, 2 for JS/TS, 4 for Python/C# unless project config says otherwise.
- Trailing commas: enabled for multi-line object/array literals where the language supports it.

Imports
- Order imports by groups: 1) standard library / core runtime, 2) external dependencies, 3) internal modules, 4) styles/assets. Separate groups with a blank line.
- Use absolute imports for application-level modules if the project config supports it (tsconfig paths / NODE_PATH). Otherwise prefer relative imports but avoid deep relative chains like ../../../../.
- Keep import lists concise: import only what you use. Prefer named imports over namespace imports unless necessary.

Types and Type Systems
- TypeScript: enable strict mode (strict: true). Prefer explicit return types for public functions and module exports. Avoid using any; prefer unknown and narrow it as early as possible.
- C#: prefer nullable reference types enabled. Use explicit types for public APIs; var is acceptable for local variables when the type is obvious.
- Python: prefer type annotations for public APIs and complex functions. Use typing.Any sparingly.

Naming Conventions
- Variables / parameters: camelCase
- Functions: camelCase (verb-first for actions, e.g., fetchUser)
- Classes / Types / Components: PascalCase
- Constants: UPPER_SNAKE_CASE for true constants; prefer readonly or const where supported.
- Files: kebab-case or camelCase for JS/TS, snake_case for Python. Prefer each file to export a single top-level concept when it makes sense.

Error Handling
- Do not swallow errors silently. Always handle or rethrow with added context.
- Prefer structured errors (custom error classes) when a function can fail in well-known modes. Use error wrapping or additional fields to preserve original stack and context.
- In async code, prefer async/await with try/catch. When returning error objects, document the shape and propagate consistently.

Logging
- Keep logs meaningful and include context (request id, user id) when available. Use the project's logging facility (do not litter console.log in production code).
- Debug logs should be gated behind debug/trace flags.

Testing
- Follow AAA pattern: Arrange, Act, Assert. Keep tests fast and deterministic.
- Tests should be unit-first. Use integration tests sparingly and clearly mark them.
- Use fixtures and factories to create test data. Avoid heavy test setup in many tests; prefer shared helper modules.
- When adding a failing test for bug reproduction, add a one-line comment with the bug id or short description.

PRs and Commits
- Keep commits focused and small. Each commit should represent a single logical change.
- Commit message style: <type>(scope): short-summary
  - type: feat | fix | chore | docs | refactor | test | style
  - scope: optional area of repo
- Don't amend or rewrite history after pushing to a shared branch. Use new commits to fix issues.

Automation & CI
- Agents must run format and lint locally before creating PRs. If CI fails, include CI logs in the PR description and avoid merging until green.

Security & Secrets
- Never add credentials to the repo. If changes require secrets to run, document the variables and ask a human to provide them via secure channels.

Cursor / Copilot Rules
----------------------
- Cursor rules (.cursor/rules/ or .cursorrules): none detected.
- Copilot instructions (.github/copilot-instructions.md): none detected.

If you add Cursor or Copilot rules, update this file to reference them and ensure automated agents follow the specified constraints.

When to Ask for Help
---------------------
- If a build or test failure is unclear after 2 attempts and a reasonable local reproduce fails, include logs and ask a human.
- For ambiguous requirements or risky refactors, open a draft PR and request design feedback before completing the implementation.

Appendix: Example single-test commands (copyable)
- Jest: npx jest src/components/MyComponent.test.ts -t "renders with props"
- Vitest: npx vitest run src/lib/foo.test.ts -t "handles error"
- pytest: pytest tests/test_api.py::test_create_item -q
- dotnet: dotnet test --filter "FullyQualifiedName~MyNamespace.MyTests.Test_CreateItem"
- go: go test ./internal/service -run TestCreateItem

Maintainers: If you are a repo maintainer and want stricter or project-specific rules, please commit an updated AGENTS.md with concrete commands and tooling configs.

<!-- GSD:project-start source:PROJECT.md -->
## Project

**BFGA — Board For Gaming App**

A collaborative digital whiteboard/board game companion built with Avalonia UI and SkiaSharp. Users draw, place shapes/images/text on a shared canvas with real-time multiplayer via LiteNetLib P2P networking. Designed for tabletop RPG and board game sessions.

**Core Value:** Real-time collaborative canvas that stays in sync across all connected peers — drawing, shapes, and pointer interactions must feel instant and consistent.

### Constraints

- **Tech stack**: .NET 9, Avalonia 11.3, SkiaSharp 3.x, LiteNetLib — must use existing stack
- **Architecture**: Must follow existing 4-layer pattern (Core → Network → Canvas → App)
- **Performance**: Laser trail rendering must not degrade canvas FPS; use efficient point buffer
- **Network**: Laser state is transient — never persisted, only broadcast to active peers
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

## Languages
- C# (latest LangVersion via .NET 9 SDK) - All source code across 4 projects
- AXAML (Avalonia XAML) - UI markup in `src/BFGA.App/Views/*.axaml`, `src/BFGA.App/Styles/*.axaml`
## Runtime
- .NET 9.0 (`net9.0` TFM in all `.csproj` files)
- Nullable reference types enabled globally (`<Nullable>enable</Nullable>`)
- Implicit usings enabled globally (`<ImplicitUsings>enable</ImplicitUsings>`)
- NuGet (via `dotnet restore` / Visual Studio)
- No `global.json` detected — uses whatever .NET 9 SDK is installed
- No `Directory.Build.props` — each project manages its own properties
## Frameworks
- Avalonia UI 11.3.12 - Cross-platform desktop UI framework (replaces WPF)
- SkiaSharp 3.116.1 - 2D graphics rendering engine for canvas/board drawing
- LiteNetLib 2.1.0 - UDP networking library for peer-to-peer game sessions
- MessagePack 2.5.198 - Binary serialization for board state and network protocol
- xUnit 2.9.2 - Test framework
- xunit.runner.visualstudio 2.8.2 - VS test adapter
- Microsoft.NET.Test.Sdk 17.12.0 - Test host infrastructure
- Visual Studio 2022 (Solution format v17)
- MSBuild via `dotnet build`
## Key Dependencies
- `Avalonia` 11.3.12 - UI framework; entire app built on it
- `SkiaSharp` 3.116.1 - Canvas rendering engine; board drawing, element rendering
- `LiteNetLib` 2.1.0 - UDP networking; multiplayer host/client sessions
- `MessagePack` 2.5.198 - Binary serialization; board file persistence + network protocol
- `xunit` 2.9.2 - Test assertions and test framework
- `Microsoft.NET.Test.Sdk` 17.12.0 - Test execution infrastructure
- `System.Text.Json` - Settings file serialization (`src/BFGA.App/Services/SettingsService.cs`)
- `System.Numerics.Vector2` - 2D coordinates throughout board/canvas/network code
- `System.Collections.Concurrent` - Thread-safe collections in `GameHost`
## Solution Structure
## Configuration
- Stored as JSON at `%APPDATA%/BFGA/settings.json` via `src/BFGA.App/Services/SettingsService.cs`
- Settings: GridOpacity, Language, DefaultImageFolder, AutosaveEnabled, AutosaveIntervalSeconds
- No appsettings.json — this is a desktop app, not a web app
- None detected. No `.env` files present.
- `src/BFGA.App/BFGA.App.csproj`: `<OutputType>WinExe</OutputType>`
- `src/BFGA.App/app.manifest`: Windows 10+ compatibility manifest
- `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` for perf
- Debug|Release configurations for Any CPU, x64, x86
## Build Commands
- `runtests.bat` - Hardcoded path to run BFGA.Core.Tests (worktree-specific, not portable)
## Platform Requirements
- .NET 9 SDK
- Visual Studio 2022+ or any editor with .NET support
- Windows 10+ (app.manifest targets Win10; Avalonia supports cross-platform but manifest is Windows-only)
- Desktop application (WinExe output type)
- Cross-platform via Avalonia (Windows, macOS, Linux)
- No server deployment — purely client-side with P2P networking
- Default network port: 7777 (UDP via LiteNetLib)
## Serialization Architecture
- Custom formatters: `Vector2Formatter`, `SKColorFormatter` (in `src/BFGA.Core/Serialization/`)
- Union resolver: `DynamicUnionResolver` handles polymorphic `BoardElement` and `BoardOperation` types
- Security: `MessagePackSecurity.UntrustedData` enabled for network deserialization safety
- Shared options: `NetworkMessagePackSetup` in BFGA.Network delegates to Core's setup
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

## Naming Patterns
- PascalCase for all C# files: `BoardState.cs`, `BoardToolController.cs`, `MainViewModel.cs`
- One primary type per file, file name matches type name
- Test files: `{ClassName}Tests.cs` (e.g., `BoardStateTests.cs`, `MainViewModelTests.cs`)
- PascalCase for all public/internal methods: `AddElement()`, `RemoveElement()`, `HandlePointerPressed()`
- Private methods also PascalCase: `ApplyOperation()`, `NotifyStateChanged()`
- Verb-first for actions: `Load`, `Save`, `Apply`, `Handle`, `Create`, `Update`, `Remove`
- Boolean methods use `Is`/`Has`/`Can` prefix or question form: `IsPointInBounds()`, `HasSelection()`
- camelCase for locals and parameters: `element`, `boardState`, `operationType`
- Private fields use underscore prefix: `_state`, `_host`, `_client`, `_operations`
- No `this.` qualification — underscore prefix disambiguates
- PascalCase for classes, interfaces, enums, records, structs
- Interfaces prefixed with `I`: `IBoardTool`, `IFileDialogService`, `IGameHostSession`, `IClipboardService`
- Abstract base classes use descriptive names without `Base` suffix: `BoardElement`, `BoardOperation`
- Enums use singular PascalCase: `BoardToolType`, `ShapeType`, `SelectionHandleKind`, `ConnectionMode`
- Records for immutable DTOs: `ToolInput`, `ToolResult`, `ToolContext`, `SelectionHandle`, `ClipboardImageData`
- `const` fields use PascalCase (not UPPER_SNAKE): `MaxUndoDepth = 50`, `DefaultPort = 9050`
- Static readonly also PascalCase: `ThemeColors.SelectionBlue`, `ThemeColors.GridDot`
- Mirror folder structure: `BFGA.{Project}.{Subfolder}`
- Examples: `BFGA.Core.Models`, `BFGA.Canvas.Tools`, `BFGA.Canvas.Rendering`, `BFGA.App.ViewModels`, `BFGA.App.Services`, `BFGA.Network.Protocol`
## Code Style
- No .editorconfig or centralized formatting config detected
- No `dotnet format` configuration
- Indentation: 4 spaces (standard C# convention observed throughout)
- Line length: generally under 120 characters
- File-scoped namespaces everywhere: `namespace BFGA.Core.Models;`
- Braces on own line for types and methods (Allman style)
- Single blank line between methods
- No trailing commas in C# (language doesn't support them in most contexts)
- No analyzer packages (StyleCop, Roslynator) referenced in any .csproj
- `<Nullable>enable</Nullable>` in all projects — nullable reference types enforced
- `<ImplicitUsings>enable</ImplicitUsings>` in all projects
- Warnings treated as default (no `<TreatWarningsAsErrors>`)
## Import Organization
- None. All imports use full namespace paths.
- Test projects add `<Using Include="Xunit" />` in .csproj for global xUnit access
- ImplicitUsings covers `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`, etc.
## Error Handling
- Guard clauses at method entry with `ArgumentNullException.ThrowIfNull()` or manual `throw new ArgumentNullException()`
- `ArgumentException` for invalid values
- Exception wrapping in I/O: `BoardFileStore` catches and wraps file errors with context
- `AsyncRelayCommand` catches exceptions and routes to status bar text — does NOT rethrow
- Network layer: `Debug.WriteLine` for malformed messages, silently drops them (design choice for resilience)
- No custom exception hierarchy — uses built-in .NET exceptions
## Logging
- `Debug.WriteLine` used in network code for error conditions: `src/BFGA.Network/GameHost.cs`, `src/BFGA.Network/GameClient.cs`
- `BoardDebugLogger` (`src/BFGA.App/Services/BoardDebugLogger.cs`) — custom static logger gated behind `BFGA_BOARD_DEBUG_LOG` environment variable
- No production logging infrastructure (Serilog, NLog, etc.)
- Console output not used in production code
- Use `BoardDebugLogger` for board operation diagnostics (enabled via env var)
- Use `Debug.WriteLine` for network-level diagnostics only
- Do NOT add `Console.WriteLine` — not appropriate for Avalonia desktop app
## Comments
- XML doc comments (`///`) used on some public APIs but NOT consistently — most public methods lack XML docs
- Inline comments used sparingly for non-obvious logic (coordinate transforms, hit testing math)
- No TODO/FIXME comments detected in source
## Function Design
- Most methods under 30 lines
- Notable exceptions: `BoardToolController` methods can reach 50-80 lines (tool state machine logic)
- `MainViewModel` constructor is large (~100 lines of command wiring)
- Prefer passing domain objects over primitives: `ToolInput` record bundles pointer position, modifiers, pressure
- Use `readonly record struct` for small value bundles: `ToolInput`, `ToolContext`
- Optional parameters used sparingly; prefer method overloads or separate methods
- `ToolResult` record returned from tool operations (contains operations list, cursor, status)
- Boolean returns for try-pattern: methods that can fail return bool with out parameter
- Void for mutation methods that modify state in place
- Nullable return `T?` when absence is meaningful
## Module Design
- One primary type per file
- `public` for cross-project APIs
- `internal` used for implementation details: `UndoRedoManager` in BFGA.Network is internal, tested via `[InternalsVisibleTo]`
- `[assembly: InternalsVisibleTo("BFGA.Network.Tests")]` in `src/BFGA.Network/` to expose internals to test project
- Not used. No re-export patterns. Each consumer imports the specific namespace needed.
## Key Architectural Conventions
- `ViewModelBase` at `src/BFGA.App/Infrastructure/ViewModelBase.cs` — implements `INotifyPropertyChanged`
- `SetProperty<T>` helper for property change notification
- `RelayCommand` and `AsyncRelayCommand` at `src/BFGA.App/Infrastructure/`
- No CommunityToolkit.Mvvm or other MVVM framework — all infrastructure is custom
- All serializable types decorated with `[MessagePackObject]` and `[Key(n)]` attributes
- Polymorphism via `[Union(n, typeof(...))]` on abstract bases: `BoardElement`, `BoardOperation`
- Custom formatters for non-MessagePack types: `Vector2Formatter`, `SKColorFormatter` in `src/BFGA.Core/Serialization/MessagePackSetup.cs`
- Deep cloning via serialize-then-deserialize roundtrip
- Platform services abstracted: `IFileDialogService`, `IClipboardService` (`src/BFGA.App/Services/`)
- Network sessions abstracted: `IGameSessionFactory`, `IGameHostSession`, `IGameClientSession` (`src/BFGA.App/Networking/`)
- Enables testing without Avalonia or network dependencies
- Dependencies wired manually in constructors
- Factory pattern for session creation: `IGameSessionFactory` → `NetworkGameSessionFactory`
- ViewModels receive dependencies via constructor parameters
- Pure functions organized in static classes: `ElementBoundsHelper`, `HitTestHelper`, `CoordinateTransformHelper`
- Located in `src/BFGA.Canvas/Rendering/`
- Stateless — take all inputs as parameters, return results
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

## Pattern Overview
- 4-project solution: Core → Network → Canvas → App (dependency direction)
- MVVM in App layer using hand-rolled `ViewModelBase` (no CommunityToolkit)
- Operation-based protocol: all board mutations expressed as `BoardOperation` subtypes
- Host-authoritative networking: host validates/applies ops then broadcasts to clients
- MessagePack binary serialization for both file persistence and network protocol
- SkiaSharp rendering via Avalonia's `ICustomDrawOperation` for GPU-accelerated canvas
## Layers
- Purpose: Board state, element models, serialization infrastructure
- Location: `src/BFGA.Core/`
- Contains: `BoardState`, `BoardElement` hierarchy, `BoardFileStore`, MessagePack formatters
- Depends on: MessagePack, SkiaSharp (for `SKColor` in models)
- Used by: All other projects
- Purpose: P2P networking protocol, host/client management, undo/redo
- Location: `src/BFGA.Network/`
- Contains: `GameHost`, `GameClient`, `BoardOperation` hierarchy, `UndoRedoManager`, `OperationSerializer`
- Depends on: BFGA.Core, LiteNetLib, MessagePack
- Used by: BFGA.Canvas, BFGA.App
- Purpose: SkiaSharp-based canvas rendering, drawing tools, hit testing, viewport management
- Location: `src/BFGA.Canvas/`
- Contains: `BoardCanvas`, `BoardViewport`, `BoardToolController`, rendering helpers, tool abstractions
- Depends on: BFGA.Core, BFGA.Network (for `BoardOperation` in `ToolResult`), Avalonia, Avalonia.Skia, SkiaSharp
- Used by: BFGA.App
- Purpose: Avalonia desktop app, MVVM viewmodels, views, platform services
- Location: `src/BFGA.App/`
- Contains: ViewModels, Views (AXAML), Services, Networking adapters, Converters, Styles
- Depends on: BFGA.Core, BFGA.Network, BFGA.Canvas, Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent
- Used by: End user (executable)
## Project Dependency Graph
```
```
- `BFGA.Core` has zero project references (leaf dependency)
- `BFGA.Network` references only `BFGA.Core`
- `BFGA.Canvas` references `BFGA.Core` and `BFGA.Network` (needs `BoardOperation` types for `ToolResult`)
- `BFGA.App` references all three
## Key Abstractions
- Base: `BoardElement` (abstract) — `src/BFGA.Core/Models/BoardElement.cs`
- Subtypes: `StrokeElement`, `ShapeElement`, `TextElement`, `ImageElement`
- MessagePack `[Union]` discriminated — keys 0–3
- Common properties: Id, Position, Size, Rotation, ZIndex, OwnerId, IsLocked
- Base: `BoardOperation` (abstract) — `src/BFGA.Network/Protocol/BoardOperation.cs`
- 13 subtypes: AddElement, UpdateElement, DeleteElement, MoveElement, CursorUpdate, DrawStrokePoint, CancelStroke, RequestFullSync, FullSyncResponse, PeerJoined, PeerLeft, Undo, Redo
- MessagePack `[Union]` discriminated — keys 0–12
- Common properties: Type (enum), SenderId, Timestamp
- Location: `src/BFGA.Canvas/Tools/IBoardTool.cs`
- Methods: `PointerDown`, `PointerMove`, `PointerUp` accepting `ToolContext` + `ToolInput`, returning `ToolResult`
- Note: Currently NOT used directly. `BoardToolController` handles all tools inline via switch statements.
- `IGameHostSession` — `src/BFGA.App/Networking/IGameHostSession.cs`
- `IGameClientSession` — `src/BFGA.App/Networking/IGameClientSession.cs`
- `IGameSessionFactory` — `src/BFGA.App/Networking/IGameSessionFactory.cs`
- `NetworkGameSessionFactory` — adapts `GameHost`/`GameClient` via private adapter classes
- `IFileDialogService` — `src/BFGA.App/Services/IFileDialogService.cs`
- `IClipboardService` — `src/BFGA.App/Services/IClipboardService.cs`
- Implementations: `AvaloniaFileDialogService`, `AvaloniaClipboardService`
- Location: `src/BFGA.App/Infrastructure/ViewModelBase.cs`
- Hand-rolled `INotifyPropertyChanged` with `SetProperty<T>` helper
- No dependency on CommunityToolkit or other MVVM frameworks
## Data Flow
- `DispatcherTimer` at 50ms interval calls `PollNetwork()` on UI thread
- `Host.PollEvents()` / `Client.PollEvents()` → LiteNetLib processes queued packets
- Events fire synchronously on UI thread (no marshaling needed)
- `BoardCanvas.Render()` creates `BoardDrawOperation` (implements `ICustomDrawOperation`)
- Avalonia calls `BoardDrawOperation.Render()` on render thread with `ISkiaSharpApiLeaseFeature`
- Gets raw `SKCanvas` → draws background, dot grid, sorted elements, selection overlay, eraser preview, remote cursors
- State is snapshot on UI thread in `BoardDrawOperation` constructor to avoid cross-thread issues
- `BoardFileStore.SaveAsync()` / `LoadAsync()` — `src/BFGA.Core/BoardFileStore.cs`
- Format: MessagePack binary (`.bfga` extension)
- Atomic write: temp file → File.Replace
- Autosave: `DispatcherTimer` at 1-minute interval → `%USERPROFILE%/Documents/BFGA/{boardname}.bfga`
- `MainViewModel` owns canonical `BoardState` instance
- In host mode: `GameHost` has authoritative copy; MainViewModel clones after each mutation
- In client mode: MainViewModel applies ops locally (optimistic) and also sends to host
- Immutable state updates via MessagePack clone: `Serialize → Deserialize` pattern
- Remote presence (cursors, stroke previews) managed as immutable dictionaries on MainViewModel
## Entry Points
- Location: `src/BFGA.App/Program.cs`
- `Main()` → `BuildAvaloniaApp()` → `StartWithClassicDesktopLifetime()`
- `App.OnFrameworkInitializationCompleted()` creates `MainWindow`
- `MainWindow` constructor creates `MainViewModel` with platform service implementations
- `MainViewModel.CurrentScreen` property returns either `ConnectionScreenViewModel` or `BoardScreenViewModel`
- Switches based on `ConnectionState` (Hosting/Connected → Board, else → Connection)
- Views bound via DataTemplates in AXAML
## Error Handling
- `AsyncRelayCommand` wraps async operations with try/catch → `HandleShellError()` updates `StatusText`
- Network errors: `Debug.WriteLine` for logging, malformed packets silently dropped
- `BoardFileStore`: validates arguments, wraps deserialization errors in `InvalidOperationException`
- Host operation validation: rejects invalid operations (missing elements, host-only ops from clients)
- Errors do NOT propagate to user as dialogs — status bar only
## Cross-Cutting Concerns
- `Debug.WriteLine` in network layer (host/client message tracing)
- `BoardDebugLogger` — optional file-based debug logging, enabled via `BFGA_BOARD_DEBUG_LOG=1` env var
- No structured logging framework
- `GameHost.ValidateOperation()` — rejects host-only ops from clients, checks element existence for updates/moves/deletes
- `BoardFileStore` — argument validation on save/load
- No input validation framework (e.g., FluentValidation)
- MessagePack with `DynamicUnionResolver` for polymorphic types
- Custom formatters: `Vector2Formatter`, `SKColorFormatter` in `src/BFGA.Core/Serialization/`
- Two setup classes: `MessagePackSetup` (Core), `NetworkMessagePackSetup` (Network, delegates to Core)
- `UntrustedData` security mode enabled
- `UndoRedoManager` — `src/BFGA.Network/UndoRedoManager.cs`
- Per-user undo stacks (max 50 depth)
- Inverse operations computed at apply time
- Host-only: clients send `UndoOperation`/`RedoOperation` requests, host resolves to concrete ops
- `SettingsService` — `src/BFGA.App/Services/SettingsService.cs`
- JSON file at `%APPDATA%/BFGA/settings.json`
- Debounced save (500ms) on property changes
- `ImageDecodeCache` in `src/BFGA.Canvas/Rendering/ImageDecodeCache.cs` — caches decoded SKBitmap for ImageElements
- No network-level or state caching
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->
## Project Skills

No project skills found. Add skills to any of: `.OpenCode/skills/`, `.agents/skills/`, `.cursor/skills/`, or `.github/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using edit, write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-OpenCode-profile` -- do not edit manually.
<!-- GSD:profile-end -->
