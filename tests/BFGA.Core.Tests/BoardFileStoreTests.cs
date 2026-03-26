using System.Numerics;
using BFGA.Core.Models;
using SkiaSharp;

namespace BFGA.Core.Tests;

public class BoardFileStoreTests
{
    private static string GetTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.bfga");
    }

    [Fact]
    public async Task SaveAndLoad_BoardState_RoundTripsSuccessfully()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var board = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Test Board"
        };

        try
        {
            // Act
            await BoardFileStore.SaveAsync(board, filePath);
            var loaded = await BoardFileStore.LoadAsync(filePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(board.BoardId, loaded.BoardId);
            Assert.Equal(board.BoardName, loaded.BoardName);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_StrokeElement_PreservesAllProperties()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var boardId = Guid.NewGuid();
        var elementId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var board = new BoardState
        {
            BoardId = boardId,
            BoardName = "Stroke Test"
        };
        var stroke = new StrokeElement
        {
            Id = elementId,
            Position = new Vector2(10, 20),
            Size = new Vector2(100, 50),
            Rotation = 45f,
            ZIndex = 5,
            OwnerId = ownerId,
            IsLocked = true,
            Points = new List<Vector2> { new(0, 0), new(50, 50), new(100, 0) },
            Color = SKColors.Red,
            Thickness = 2.5f
        };
        board.Elements.Add(stroke);

        try
        {
            // Act
            await BoardFileStore.SaveAsync(board, filePath);
            var loaded = await BoardFileStore.LoadAsync(filePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Single(loaded.Elements);
            var loadedStroke = Assert.IsType<StrokeElement>(loaded.Elements[0]);
            Assert.Equal(elementId, loadedStroke.Id);
            Assert.Equal(new Vector2(10, 20), loadedStroke.Position);
            Assert.Equal(new Vector2(100, 50), loadedStroke.Size);
            Assert.Equal(45f, loadedStroke.Rotation);
            Assert.Equal(5, loadedStroke.ZIndex);
            Assert.Equal(ownerId, loadedStroke.OwnerId);
            Assert.True(loadedStroke.IsLocked);
            Assert.Equal(3, loadedStroke.Points.Count);
            Assert.Equal(SKColors.Red, loadedStroke.Color);
            Assert.Equal(2.5f, loadedStroke.Thickness);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_ShapeElement_PreservesAllProperties()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var elementId = Guid.NewGuid();
        var board = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Shape Test"
        };
        var shape = new ShapeElement
        {
            Id = elementId,
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Rotation = 90f,
            ZIndex = 1,
            OwnerId = Guid.NewGuid(),
            IsLocked = false,
            Type = ShapeType.Ellipse,
            StrokeColor = SKColors.Blue,
            FillColor = SKColors.Green.WithAlpha(128),
            StrokeWidth = 3f
        };
        board.Elements.Add(shape);

        try
        {
            // Act
            await BoardFileStore.SaveAsync(board, filePath);
            var loaded = await BoardFileStore.LoadAsync(filePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Single(loaded.Elements);
            var loadedShape = Assert.IsType<ShapeElement>(loaded.Elements[0]);
            Assert.Equal(elementId, loadedShape.Id);
            Assert.Equal(ShapeType.Ellipse, loadedShape.Type);
            Assert.Equal(SKColors.Blue, loadedShape.StrokeColor);
            Assert.Equal(SKColors.Green.WithAlpha(128), loadedShape.FillColor);
            Assert.Equal(3f, loadedShape.StrokeWidth);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_ImageElement_PreservesEmbeddedData()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var elementId = Guid.NewGuid();
        var board = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Image Test"
        };
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
        var image = new ImageElement
        {
            Id = elementId,
            Position = new Vector2(0, 0),
            Size = new Vector2(200, 150),
            OwnerId = Guid.NewGuid(),
            ImageData = imageData,
            OriginalFileName = "test.png"
        };
        board.Elements.Add(image);

        try
        {
            // Act
            await BoardFileStore.SaveAsync(board, filePath);
            var loaded = await BoardFileStore.LoadAsync(filePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Single(loaded.Elements);
            var loadedImage = Assert.IsType<ImageElement>(loaded.Elements[0]);
            Assert.Equal(elementId, loadedImage.Id);
            Assert.Equal("test.png", loadedImage.OriginalFileName);
            Assert.Equal(imageData, loadedImage.ImageData);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_LargeEmbeddedImage_1MB_PreservesData()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var board = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Large Image Test"
        };
        // Create 1MB of data
        var largeImageData = new byte[1024 * 1024];
        new Random(42).NextBytes(largeImageData);
        var image = new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(1920, 1080),
            OwnerId = Guid.NewGuid(),
            ImageData = largeImageData,
            OriginalFileName = "large_image.png"
        };
        board.Elements.Add(image);

        try
        {
            // Act
            await BoardFileStore.SaveAsync(board, filePath);
            var loaded = await BoardFileStore.LoadAsync(filePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Single(loaded.Elements);
            var loadedImage = Assert.IsType<ImageElement>(loaded.Elements[0]);
            Assert.Equal(1024 * 1024, loadedImage.ImageData.Length);
            Assert.Equal(largeImageData, loadedImage.ImageData);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_MultipleElementTypes_AllPreserved()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var board = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Multi-type Test"
        };

        board.Elements.Add(new StrokeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 0),
            Size = new Vector2(100, 100),
            Points = new List<Vector2> { new(0, 0), new(50, 50) },
            Color = SKColors.Red,
            Thickness = 2f
        });

        board.Elements.Add(new ShapeElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(100, 0),
            Size = new Vector2(50, 50),
            Type = ShapeType.Rectangle,
            StrokeColor = SKColors.Blue,
            FillColor = SKColors.Transparent,
            StrokeWidth = 1f
        });

        board.Elements.Add(new ImageElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(0, 100),
            Size = new Vector2(100, 100),
            ImageData = new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG SOI
            OriginalFileName = "photo.jpg"
        });

        board.Elements.Add(new TextElement
        {
            Id = Guid.NewGuid(),
            Position = new Vector2(50, 50),
            Size = new Vector2(100, 30),
            Text = "Hello World",
            FontSize = 16f,
            Color = SKColors.Black,
            FontFamily = "Arial"
        });

        try
        {
            // Act
            await BoardFileStore.SaveAsync(board, filePath);
            var loaded = await BoardFileStore.LoadAsync(filePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(4, loaded.Elements.Count);
            var loadedText = Assert.IsType<TextElement>(loaded.Elements[3]);
            Assert.Equal("Hello World", loadedText.Text);
            Assert.Equal(16f, loadedText.FontSize);
            Assert.Equal(SKColors.Black, loadedText.Color);
            Assert.Equal("Arial", loadedText.FontFamily);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_EmptyBoard_RoundTripsSuccessfully()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var board = new BoardState
        {
            BoardId = Guid.NewGuid(),
            BoardName = "Empty Board"
        };

        try
        {
            // Act
            await BoardFileStore.SaveAsync(board, filePath);
            var loaded = await BoardFileStore.LoadAsync(filePath);

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(board.BoardId, loaded.BoardId);
            Assert.Equal(board.BoardName, loaded.BoardName);
            Assert.Empty(loaded.Elements);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_InvalidPath_ThrowsArgumentException(string? filePath)
    {
        var board = new BoardState { BoardId = Guid.NewGuid(), BoardName = "Test" };
        await Assert.ThrowsAsync<ArgumentException>(() => BoardFileStore.SaveAsync(board, filePath!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadAsync_InvalidPath_ThrowsArgumentException(string? filePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => BoardFileStore.LoadAsync(filePath!));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".bfga");
        await Assert.ThrowsAsync<FileNotFoundException>(() => BoardFileStore.LoadAsync(nonExistentPath));
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ThrowsInvalidOperationException()
    {
        var filePath = GetTempFilePath();
        try
        {
            await File.WriteAllTextAsync(filePath, "not valid messagepack data");
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => BoardFileStore.LoadAsync(filePath));
            Assert.Contains("Failed to load board from", ex.Message);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
