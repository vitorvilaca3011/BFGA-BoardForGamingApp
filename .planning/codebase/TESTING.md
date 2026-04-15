# Testing Patterns

**Analysis Date:** 2026-04-15

## Test Framework

**Runner:**
- xUnit 2.9.2
- Microsoft.NET.Test.Sdk 17.12.0
- Config: no custom xUnit config file — defaults used

**Assertion Library:**
- xUnit built-in `Assert` class (no FluentAssertions, Shouldly, etc.)

**Mocking Library:**
- None. All test doubles are hand-written fakes embedded in test files.

**Run Commands:**
```bash
dotnet test                                    # Run all tests (all 3 test projects)
dotnet test tests/BFGA.Core.Tests              # Run Core tests only
dotnet test tests/BFGA.Network.Tests           # Run Network tests only
dotnet test tests/BFGA.App.Tests               # Run App tests only
dotnet test --filter "FullyQualifiedName~BoardStateTests"  # Single test class
dotnet test --filter "FullyQualifiedName~BoardStateTests.AddElement_IncreasesCount"  # Single test
```

**Batch script:**
- `runtests.bat` exists at repo root — runs all test projects sequentially

## Test File Organization

**Location:**
- Separate test projects mirroring src structure:
  - `src/BFGA.Core/` → `tests/BFGA.Core.Tests/`
  - `src/BFGA.Network/` → `tests/BFGA.Network.Tests/`
  - `src/BFGA.App/` → `tests/BFGA.App.Tests/`
- **No `BFGA.Canvas.Tests` project** — canvas logic tested from `BFGA.Core.Tests` (which references BFGA.Canvas)

**Naming:**
- Test files: `{ClassName}Tests.cs`
- Test classes: `{ClassName}Tests`
- Test methods: `Method_Scenario_ExpectedResult` or `Scenario_ExpectedOutcome`

**Structure:**
```
tests/
├── BFGA.Core.Tests/
│   ├── BFGA.Core.Tests.csproj
│   ├── AssemblyInfo.cs           # DisableTestParallelization + collection definitions
│   ├── BoardStateTests.cs
│   ├── BoardFileStoreTests.cs
│   ├── SerializationTests.cs
│   ├── BoardToolControllerTests.cs
│   ├── CanvasMathTests.cs
│   ├── PointerToToolTests.cs
│   ├── DotGridRenderingTests.cs
│   └── CollaboratorOverlayTests.cs
├── BFGA.Network.Tests/
│   ├── BFGA.Network.Tests.csproj
│   ├── NetworkTests.cs
│   ├── ProtocolTests.cs
│   └── UndoRedoManagerTests.cs
└── BFGA.App.Tests/
    ├── BFGA.App.Tests.csproj
    ├── AssemblyInfo.cs           # DisableTestParallelization
    ├── MainViewModelTests.cs     # ~1400 lines, includes embedded fakes
    ├── BoardScreenViewModelTests.cs
    ├── BoardViewPipelineTests.cs
    ├── PropertyPanelTests.cs
    ├── MainWindowShortcutTests.cs
    ├── StartupSmokeTests.cs
    ├── RosterOverlayTests.cs
    └── BoardDebugLoggerTests.cs
```

## Test Structure

**Suite Organization:**
```csharp
// Standard pattern observed across all test files
namespace BFGA.Core.Tests;

public class BoardStateTests
{
    [Fact]
    public void AddElement_IncreasesCount()
    {
        // Arrange
        var state = new BoardState();
        var element = new StrokeElement { /* ... */ };

        // Act
        state.AddElement(element);

        // Assert
        Assert.Equal(1, state.Elements.Count);
    }

    [Theory]
    [InlineData(ShapeType.Rectangle)]
    [InlineData(ShapeType.Ellipse)]
    public void CreateShape_WithType_ReturnsCorrectShape(ShapeType type)
    {
        // ...
    }
}
```

**Patterns:**
- AAA pattern (Arrange/Act/Assert) followed consistently
- `[Fact]` for single-case tests, `[Theory]` + `[InlineData]` for parameterized
- No shared setup (`IClassFixture`, constructor injection) — each test creates its own state
- No `IAsyncLifetime` or `IDisposable` on test classes (cleanup done in `finally` blocks where needed)

## Parallelization

**Disabled globally** in BFGA.Core.Tests and BFGA.App.Tests:
```csharp
// AssemblyInfo.cs
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

**Test collections** used for shared-resource tests:
```csharp
// Tests that manipulate BFGA_BOARD_DEBUG_LOG environment variable
[Collection("BFGA_BOARD_DEBUG_LOG")]
public class BoardDebugLoggerTests { ... }
```

## Mocking / Test Doubles

**Framework:** None — all hand-written fakes

**Patterns:**
```csharp
// Fake embedded directly in test file (MainViewModelTests.cs)
private class FakeFileDialogService : IFileDialogService
{
    public string? OpenResult { get; set; }
    public string? SaveResult { get; set; }

    public Task<string?> ShowOpenFileDialogAsync(string title, string filter)
        => Task.FromResult(OpenResult);

    public Task<string?> ShowSaveFileDialogAsync(string title, string filter, string defaultExt)
        => Task.FromResult(SaveResult);
}

// Throwing fake for error path testing
private class ThrowingFileDialogService : IFileDialogService
{
    public Task<string?> ShowOpenFileDialogAsync(string title, string filter)
        => throw new InvalidOperationException("Dialog error");
    // ...
}

// Fake session factory
private class FakeGameSessionFactory : IGameSessionFactory
{
    public FakeGameHostSession? LastHost { get; private set; }
    public FakeGameClientSession? LastClient { get; private set; }

    public IGameHostSession CreateHostSession(/* params */)
    {
        LastHost = new FakeGameHostSession(/* ... */);
        return LastHost;
    }
    // ...
}
```

**What to mock:**
- Platform services: `IFileDialogService`, `IClipboardService`
- Network sessions: `IGameHostSession`, `IGameClientSession`, `IGameSessionFactory`
- File system I/O (via temp files + cleanup, not mocks)

**What NOT to mock:**
- Domain models (`BoardState`, `BoardElement` subtypes) — always use real instances
- Serialization — test actual MessagePack round-trips
- Static helper classes (`ElementBoundsHelper`, `HitTestHelper`) — pure functions, test directly
- `BoardToolController` — test with real state, not mocked

## Fixtures and Factories

**Test Data — created inline:**
```csharp
// Stroke element creation (common across tests)
var stroke = new StrokeElement
{
    Id = Guid.NewGuid(),
    Points = [new Vector2(0, 0), new Vector2(100, 100)],
    Color = SKColors.Black,
    StrokeWidth = 2f
};

// Shape element creation
var shape = new ShapeElement
{
    Id = Guid.NewGuid(),
    ShapeType = ShapeType.Rectangle,
    Position = new Vector2(10, 10),
    Size = new Vector2(50, 50),
    FillColor = SKColors.Blue
};

// Board state with elements
var state = new BoardState();
state.AddElement(stroke);
```

**No shared fixture files** — all test data constructed inline within each test method.

**Temp file handling:**
```csharp
// Pattern for file I/O tests (BoardFileStoreTests)
var tempPath = Path.GetTempFileName();
try
{
    var store = new BoardFileStore();
    store.Save(state, tempPath);
    var loaded = store.Load(tempPath);
    Assert.Equal(state.Elements.Count, loaded.Elements.Count);
}
finally
{
    File.Delete(tempPath);
}
```

## Coverage

**Requirements:** None enforced — no coverage thresholds configured

**No coverage tooling** configured in .csproj files (no coverlet, no ReportGenerator)

**View Coverage:**
```bash
# Not configured — would need to add coverlet:
dotnet test --collect:"XPlat Code Coverage"
```

## Test Types

**Unit Tests:**
- All tests are unit tests
- Test individual classes/methods in isolation
- Fakes for external dependencies (file dialogs, network, clipboard)
- Real domain objects (no mocking of models or pure functions)

**Integration Tests:**
- Network tests (`NetworkTests.cs`) test host↔client communication with real LiteNetLib over loopback
- Use `ManualResetEventSlim` with timeout for async polling:
```csharp
var connected = new ManualResetEventSlim(false);
client.Connected += (_, _) => connected.Set();
// Poll network events
for (int i = 0; i < 50; i++)
{
    host.PollEvents();
    client.PollEvents();
    if (connected.Wait(50)) break;
}
Assert.True(connected.IsSet);
```

**E2E Tests:**
- Not present. No UI automation or Avalonia headless testing.

**XAML Validation:**
- `StartupSmokeTests.cs` validates XAML files by reading them as strings and checking structure
- Pattern: read .axaml file content, assert it contains expected elements
- Located in `tests/BFGA.App.Tests/StartupSmokeTests.cs`

## Common Patterns

**Async Testing:**
```csharp
// AsyncRelayCommand testing pattern
[Fact]
public async Task SaveCommand_WithValidPath_SavesFile()
{
    var fakeDialog = new FakeFileDialogService { SaveResult = tempPath };
    var vm = new MainViewModel(fakeDialog, /* ... */);

    await vm.SaveCommand.ExecuteAsync(null);

    Assert.True(File.Exists(tempPath));
}
```

**Error Testing:**
```csharp
// Exception assertion
[Fact]
public void Load_WithInvalidPath_ThrowsException()
{
    var store = new BoardFileStore();
    Assert.Throws<InvalidOperationException>(() => store.Load("nonexistent.bfga"));
}

// Error state assertion (ViewModel absorbs exceptions)
[Fact]
public async Task LoadCommand_WhenFileFails_ShowsErrorStatus()
{
    var fakeDialog = new ThrowingFileDialogService();
    var vm = new MainViewModel(fakeDialog, /* ... */);

    await vm.LoadCommand.ExecuteAsync(null);

    Assert.Contains("Error", vm.StatusText);
}
```

**Serialization Round-trip:**
```csharp
// Pattern used in SerializationTests.cs
[Fact]
public void StrokeElement_RoundTrips_ThroughMessagePack()
{
    var original = new StrokeElement { /* properties */ };
    var bytes = MessagePackSerializer.Serialize(original, MessagePackSetup.Options);
    var deserialized = MessagePackSerializer.Deserialize<BoardElement>(bytes, MessagePackSetup.Options);

    Assert.IsType<StrokeElement>(deserialized);
    // Assert properties match
}
```

**Tool Controller Testing:**
```csharp
// Pattern in BoardToolControllerTests.cs — simulate pointer events
[Fact]
public void PenTool_PointerSequence_CreatesStroke()
{
    var state = new BoardState();
    var controller = new BoardToolController(state);
    controller.ActiveTool = BoardToolType.Pen;

    var input1 = new ToolInput(new Vector2(0, 0), /* ... */);
    controller.HandlePointerPressed(input1);

    var input2 = new ToolInput(new Vector2(50, 50), /* ... */);
    controller.HandlePointerMoved(input2);

    var result = controller.HandlePointerReleased(input2);

    Assert.Single(result.Operations);
    Assert.IsType<AddElementOperation>(result.Operations[0]);
}
```

**InternalsVisibleTo for testing internals:**
```csharp
// In src/BFGA.Network project (enables testing UndoRedoManager which is internal)
[assembly: InternalsVisibleTo("BFGA.Network.Tests")]
```

## Test Coverage Map

**Well-covered areas:**
- `BoardState` — add/remove/update elements, element ordering
- `BoardFileStore` — save/load round-trips, error handling, temp files
- Serialization — all `BoardElement` subtypes, all `BoardOperation` subtypes, custom formatters
- `BoardToolController` — all tool types (pen, shape, eraser, select, move, resize, rotate, delete, text)
- Hit testing — point-in-bounds, selection handle detection
- Canvas math — coordinate transforms, bounds calculations, stroke smoothing
- Network protocol — operation serialization, host↔client message exchange
- `UndoRedoManager` — undo/redo stacks, max depth, per-user isolation
- `MainViewModel` — command flows (new/save/load/undo/redo), error handling, status updates
- `BoardScreenViewModel` — tool selection, state transitions
- XAML structure — smoke tests validate view files parse correctly

**Coverage gaps:**
- No `BFGA.Canvas.Tests` project — canvas rendering not directly tested (only math/helpers via Core.Tests)
- No visual regression testing for SkiaSharp rendering output
- No clipboard operation testing (mock exists but limited test scenarios)
- `SettingsService` — no tests for persistent settings
- `ConnectionScreenViewModel` — no dedicated test file
- Network edge cases — reconnection, timeout, partial message delivery
- Image loading/decoding (`ImageDecodeCache`) — not tested
- Theme/color constants (`ThemeColors`) — not tested
- Avalonia converters (`src/BFGA.App/Converters/`) — not tested

## Adding New Tests

**For a new domain model:**
1. Add test file to `tests/BFGA.Core.Tests/{ModelName}Tests.cs`
2. Follow `[Fact]`/`[Theory]` pattern with AAA structure
3. Test serialization round-trip if model is `[MessagePackObject]`

**For a new ViewModel:**
1. Add test file to `tests/BFGA.App.Tests/{ViewModelName}Tests.cs`
2. Create hand-written fakes for any new service interfaces
3. Embed fakes as private classes within test class
4. Test command execution, property changes, error states

**For a new network operation:**
1. Add serialization test in `tests/BFGA.Network.Tests/ProtocolTests.cs`
2. Add undo/redo test in `tests/BFGA.Network.Tests/UndoRedoManagerTests.cs` if operation is undoable
3. Test round-trip through `MessagePackSerializer`

**For a new rendering helper:**
1. Add test file to `tests/BFGA.Core.Tests/` (no Canvas.Tests project exists)
2. Test pure function inputs→outputs, edge cases for bounds/coordinates

---

*Testing analysis: 2026-04-15*
