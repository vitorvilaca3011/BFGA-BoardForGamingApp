# Coding Conventions

**Analysis Date:** 2026-04-15

## Naming Patterns

**Files:**
- PascalCase for all C# files: `BoardState.cs`, `BoardToolController.cs`, `MainViewModel.cs`
- One primary type per file, file name matches type name
- Test files: `{ClassName}Tests.cs` (e.g., `BoardStateTests.cs`, `MainViewModelTests.cs`)

**Functions/Methods:**
- PascalCase for all public/internal methods: `AddElement()`, `RemoveElement()`, `HandlePointerPressed()`
- Private methods also PascalCase: `ApplyOperation()`, `NotifyStateChanged()`
- Verb-first for actions: `Load`, `Save`, `Apply`, `Handle`, `Create`, `Update`, `Remove`
- Boolean methods use `Is`/`Has`/`Can` prefix or question form: `IsPointInBounds()`, `HasSelection()`

**Variables/Parameters:**
- camelCase for locals and parameters: `element`, `boardState`, `operationType`
- Private fields use underscore prefix: `_state`, `_host`, `_client`, `_operations`
- No `this.` qualification — underscore prefix disambiguates

**Types:**
- PascalCase for classes, interfaces, enums, records, structs
- Interfaces prefixed with `I`: `IBoardTool`, `IFileDialogService`, `IGameHostSession`, `IClipboardService`
- Abstract base classes use descriptive names without `Base` suffix: `BoardElement`, `BoardOperation`
- Enums use singular PascalCase: `BoardToolType`, `ShapeType`, `SelectionHandleKind`, `ConnectionMode`
- Records for immutable DTOs: `ToolInput`, `ToolResult`, `ToolContext`, `SelectionHandle`, `ClipboardImageData`

**Constants:**
- `const` fields use PascalCase (not UPPER_SNAKE): `MaxUndoDepth = 50`, `DefaultPort = 9050`
- Static readonly also PascalCase: `ThemeColors.SelectionBlue`, `ThemeColors.GridDot`

**Namespaces:**
- Mirror folder structure: `BFGA.{Project}.{Subfolder}`
- Examples: `BFGA.Core.Models`, `BFGA.Canvas.Tools`, `BFGA.Canvas.Rendering`, `BFGA.App.ViewModels`, `BFGA.App.Services`, `BFGA.Network.Protocol`

## Code Style

**Formatting:**
- No .editorconfig or centralized formatting config detected
- No `dotnet format` configuration
- Indentation: 4 spaces (standard C# convention observed throughout)
- Line length: generally under 120 characters
- File-scoped namespaces everywhere: `namespace BFGA.Core.Models;`
- Braces on own line for types and methods (Allman style)
- Single blank line between methods
- No trailing commas in C# (language doesn't support them in most contexts)

**Linting:**
- No analyzer packages (StyleCop, Roslynator) referenced in any .csproj
- `<Nullable>enable</Nullable>` in all projects — nullable reference types enforced
- `<ImplicitUsings>enable</ImplicitUsings>` in all projects
- Warnings treated as default (no `<TreatWarningsAsErrors>`)

**Project-level settings (all .csproj files):**
```xml
<PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

## Import Organization

**Order (observed):**
1. `using System.*` — standard library
2. `using` external packages — Avalonia, SkiaSharp, MessagePack, LiteNetLib
3. `using BFGA.*` — internal project references
4. No blank lines between groups (flat list)

**Path Aliases:**
- None. All imports use full namespace paths.

**Global usings:**
- Test projects add `<Using Include="Xunit" />` in .csproj for global xUnit access
- ImplicitUsings covers `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`, etc.

## Error Handling

**Patterns:**
- Guard clauses at method entry with `ArgumentNullException.ThrowIfNull()` or manual `throw new ArgumentNullException()`
- `ArgumentException` for invalid values
- Exception wrapping in I/O: `BoardFileStore` catches and wraps file errors with context
- `AsyncRelayCommand` catches exceptions and routes to status bar text — does NOT rethrow
- Network layer: `Debug.WriteLine` for malformed messages, silently drops them (design choice for resilience)
- No custom exception hierarchy — uses built-in .NET exceptions

**Error propagation:**
```csharp
// Guard pattern (used in constructors and public methods)
ArgumentNullException.ThrowIfNull(state);

// I/O wrapping pattern (BoardFileStore)
try { /* file op */ }
catch (Exception ex) { throw new InvalidOperationException($"Failed to save board: {path}", ex); }

// Async command pattern (ViewModels)
try { await DoWork(); }
catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
```

## Logging

**Framework:** `System.Diagnostics.Debug.WriteLine` — no structured logging framework

**Patterns:**
- `Debug.WriteLine` used in network code for error conditions: `src/BFGA.Network/GameHost.cs`, `src/BFGA.Network/GameClient.cs`
- `BoardDebugLogger` (`src/BFGA.App/Services/BoardDebugLogger.cs`) — custom static logger gated behind `BFGA_BOARD_DEBUG_LOG` environment variable
- No production logging infrastructure (Serilog, NLog, etc.)
- Console output not used in production code

**When to log:**
- Use `BoardDebugLogger` for board operation diagnostics (enabled via env var)
- Use `Debug.WriteLine` for network-level diagnostics only
- Do NOT add `Console.WriteLine` — not appropriate for Avalonia desktop app

## Comments

**When to Comment:**
- XML doc comments (`///`) used on some public APIs but NOT consistently — most public methods lack XML docs
- Inline comments used sparingly for non-obvious logic (coordinate transforms, hit testing math)
- No TODO/FIXME comments detected in source

**JSDoc/TSDoc:** Not applicable (C# project)

**XML Doc pattern (when used):**
```csharp
/// <summary>
/// Applies the operation to the board state and returns the inverse.
/// </summary>
```

## Function Design

**Size:**
- Most methods under 30 lines
- Notable exceptions: `BoardToolController` methods can reach 50-80 lines (tool state machine logic)
- `MainViewModel` constructor is large (~100 lines of command wiring)

**Parameters:**
- Prefer passing domain objects over primitives: `ToolInput` record bundles pointer position, modifiers, pressure
- Use `readonly record struct` for small value bundles: `ToolInput`, `ToolContext`
- Optional parameters used sparingly; prefer method overloads or separate methods

**Return Values:**
- `ToolResult` record returned from tool operations (contains operations list, cursor, status)
- Boolean returns for try-pattern: methods that can fail return bool with out parameter
- Void for mutation methods that modify state in place
- Nullable return `T?` when absence is meaningful

## Module Design

**Exports:**
- One primary type per file
- `public` for cross-project APIs
- `internal` used for implementation details: `UndoRedoManager` in BFGA.Network is internal, tested via `[InternalsVisibleTo]`
- `[assembly: InternalsVisibleTo("BFGA.Network.Tests")]` in `src/BFGA.Network/` to expose internals to test project

**Barrel Files:**
- Not used. No re-export patterns. Each consumer imports the specific namespace needed.

## Key Architectural Conventions

**MVVM (hand-rolled):**
- `ViewModelBase` at `src/BFGA.App/Infrastructure/ViewModelBase.cs` — implements `INotifyPropertyChanged`
- `SetProperty<T>` helper for property change notification
- `RelayCommand` and `AsyncRelayCommand` at `src/BFGA.App/Infrastructure/`
- No CommunityToolkit.Mvvm or other MVVM framework — all infrastructure is custom

**Serialization (MessagePack):**
- All serializable types decorated with `[MessagePackObject]` and `[Key(n)]` attributes
- Polymorphism via `[Union(n, typeof(...))]` on abstract bases: `BoardElement`, `BoardOperation`
- Custom formatters for non-MessagePack types: `Vector2Formatter`, `SKColorFormatter` in `src/BFGA.Core/Serialization/MessagePackSetup.cs`
- Deep cloning via serialize-then-deserialize roundtrip

**Interface abstraction:**
- Platform services abstracted: `IFileDialogService`, `IClipboardService` (`src/BFGA.App/Services/`)
- Network sessions abstracted: `IGameSessionFactory`, `IGameHostSession`, `IGameClientSession` (`src/BFGA.App/Networking/`)
- Enables testing without Avalonia or network dependencies

**No DI container:**
- Dependencies wired manually in constructors
- Factory pattern for session creation: `IGameSessionFactory` → `NetworkGameSessionFactory`
- ViewModels receive dependencies via constructor parameters

**Static helper classes for rendering:**
- Pure functions organized in static classes: `ElementBoundsHelper`, `HitTestHelper`, `CoordinateTransformHelper`
- Located in `src/BFGA.Canvas/Rendering/`
- Stateless — take all inputs as parameters, return results

---

*Convention analysis: 2026-04-15*
