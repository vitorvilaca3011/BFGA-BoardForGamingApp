# BFGA Avalonia 11+ Implementation Guide

A comprehensive reference for building the BFGA (Board For Gaming App) whiteboard UI using Avalonia 11+, .NET 9, and SkiaSharp.

**Document Version:** 1.0  
**Last Updated:** 2026-03-27  
**Target Framework:** Avalonia 11.x, .NET 9

---

## Table of Contents

1. [Navigation / Screen Management](#1-navigation--screen-management)
2. [Custom Drawing with SkiaSharp](#2-custom-drawing-with-skiasharp)
3. [Input Handling](#3-input-handling)
4. [Styling and Theming](#4-styling-and-theming)
5. [Custom Controls](#5-custom-controls)
6. [Layout Controls](#6-layout-controls)
7. [Data Binding and MVVM](#7-data-binding-and-mvvm)
8. [Pan and Zoom](#8-pan-and-zoom)
9. [Rendering Performance](#9-rendering-performance)
10. [Assets and Icons](#10-assets-and-icons)

---

## 1. Navigation / Screen Management

### Overview

Avalonia provides several approaches for screen/page navigation. For BFGA, we need to switch between a **Connection Screen** (lobby/join) and a **Board Screen** (the whiteboard canvas).

### Approach 1: ContentControl with UserControl Switching

The most common pattern for Avalonia desktop apps is using a `ContentControl` with data templates or direct content assignment.

#### ViewModel-Based Navigation

```csharp
// MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private bool _isConnected;

    public MainViewModel()
    {
        // Start with connection view
        CurrentView = new ConnectionViewModel(this);
    }

    [RelayCommand]
    public void NavigateToBoard()
    {
        CurrentView = new BoardViewModel();
        IsConnected = true;
    }

    [RelayCommand]
    public void NavigateToConnection()
    {
        CurrentView = new ConnectionViewModel(this);
        IsConnected = false;
    }
}
```

#### MainWindow.axaml

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:BFGA.App.ViewModels"
        xmlns:views="using:BFGA.App.Views"
        x:Class="BFGA.App.Views.MainWindow"
        x:DataType="vm:MainViewModel"
        Title="BFGA - Board For Gaming App"
        Width="1200" Height="800">

    <Design.DataContext>
        <vm:MainViewModel />
    </Design.DataContext>

    <ContentControl Content="{Binding CurrentView}">
        <ContentControl.DataTemplates>
            <DataTemplate DataType="vm:ConnectionViewModel">
                <views:ConnectionView />
            </DataTemplate>
            <DataTemplate DataType="vm:BoardViewModel">
                <views:BoardView />
            </DataTemplate>
        </ContentControl.DataTemplates>
    </ContentControl>
</Window>
```

### Approach 2: TabControl for Multiple Boards

If supporting multiple simultaneous boards:

```xml
<TabControl ItemsSource="{Binding OpenBoards}"
            SelectedItem="{Binding SelectedBoard}">
    <TabControl.ItemTemplate>
        <DataTemplate DataType="vm:BoardViewModel">
            <TextBlock Text="{Binding BoardName}" />
        </DataTemplate>
    </TabControl.ItemTemplate>
    <TabControl.ContentTemplate>
        <DataTemplate DataType="vm:BoardViewModel">
            <views:BoardView />
        </DataTemplate>
    </TabControl.ContentTemplate>
</TabControl>
```

### Application Lifetime Management

```csharp
// App.axaml.cs
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow
        {
            DataContext = new MainViewModel()
        };
    }
    else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
    {
        // For mobile/web platforms
        singleView.MainView = new MainView
        {
            DataContext = new MainViewModel()
        };
    }

    base.OnFrameworkInitializationCompleted();
}
```

---

## 2. Custom Drawing with SkiaSharp

### ICustomDrawOperation for SkiaSharp Integration

For high-performance whiteboard rendering, use `ICustomDrawOperation` to get direct SkiaSharp canvas access.

#### BoardCanvas Implementation

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

public class BoardCanvas : Control
{
    // Board property - triggers redraw when changed
    public static readonly StyledProperty<BoardState?> BoardProperty =
        AvaloniaProperty.Register<BoardCanvas, BoardState?>(nameof(Board));

    public BoardState? Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    static BoardCanvas()
    {
        // Register that changes to Board property should trigger redraw
        AffectsRender<BoardCanvas>(BoardProperty);
    }

    public override void Render(DrawingContext context)
    {
        // Create custom draw operation with current bounds
        context.Custom(new BoardDrawOperation(
            new Rect(Bounds.Size), 
            Board));
    }

    // ICustomDrawOperation implementation
    private class BoardDrawOperation : ICustomDrawOperation
    {
        private readonly BoardState? _board;

        public BoardDrawOperation(Rect bounds, BoardState? board)
        {
            Bounds = bounds;
            _board = board;
        }

        public Rect Bounds { get; }

        public void Render(ImmediateDrawingContext context)
        {
            // Get SkiaSharp API access
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;

            // Clear canvas
            canvas.Clear(SKColors.White);

            if (_board?.Elements == null) return;

            // Draw all elements in z-order
            foreach (var element in _board.Elements.OrderBy(e => e.ZIndex))
            {
                DrawElement(canvas, element);
            }
        }

        private void DrawElement(SKCanvas canvas, BoardElement element)
        {
            using var paint = new SKPaint
            {
                IsAntialias = true,
                Color = element.Color.ToSKColor(),
                StrokeWidth = element.StrokeWidth
            };

            switch (element)
            {
                case StrokeElement stroke:
                    DrawStroke(canvas, stroke, paint);
                    break;
                case ShapeElement shape:
                    DrawShape(canvas, shape, paint);
                    break;
                case ImageElement image:
                    DrawImage(canvas, image);
                    break;
                case TextElement text:
                    DrawText(canvas, text, paint);
                    break;
            }
        }

        private void DrawStroke(SKCanvas canvas, StrokeElement stroke, SKPaint paint)
        {
            if (stroke.Points.Count < 2) return;

            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeCap = SKStrokeCap.Round;
            paint.StrokeJoin = SKStrokeJoin.Round;

            using var path = new SKPath();
            path.MoveTo(stroke.Points[0].X, stroke.Points[0].Y);

            for (int i = 1; i < stroke.Points.Count; i++)
            {
                path.LineTo(stroke.Points[i].X, stroke.Points[i].Y);
            }

            canvas.DrawPath(path, paint);
        }

        private void DrawShape(SKCanvas canvas, ShapeElement shape, SKPaint paint)
        {
            paint.Style = shape.IsFilled ? SKPaintStyle.Fill : SKPaintStyle.Stroke;

            var rect = new SKRect(
                shape.X, shape.Y,
                shape.X + shape.Width,
                shape.Y + shape.Height);

            switch (shape.ShapeType)
            {
                case ShapeType.Rectangle:
                    canvas.DrawRect(rect, paint);
                    break;
                case ShapeType.Ellipse:
                    canvas.DrawOval(rect, paint);
                    break;
                case ShapeType.Line:
                    canvas.DrawLine(
                        shape.X, shape.Y,
                        shape.X + shape.Width,
                        shape.Y + shape.Height, paint);
                    break;
            }
        }

        private void DrawImage(SKCanvas canvas, ImageElement image)
        {
            if (image.Bitmap == null) return;

            var destRect = new SKRect(
                image.X, image.Y,
                image.X + image.Width,
                image.Y + image.Height);

            canvas.DrawBitmap(image.Bitmap, destRect);
        }

        private void DrawText(SKCanvas canvas, TextElement text, SKPaint paint)
        {
            paint.Style = SKPaintStyle.Fill;
            paint.TextSize = text.FontSize;
            paint.Typeface = SKTypeface.FromFamilyName(text.FontFamily);

            canvas.DrawText(text.Content, text.X, text.Y, paint);
        }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose() { }
    }
}
```

### Triggering Redraws

```csharp
// Force immediate redraw
boardCanvas.InvalidateVisual();

// Or use Dispatcher for thread-safe updates
Dispatcher.UIThread.InvokeAsync(() =>
{
    boardCanvas.InvalidateVisual();
});
```

### Rendering Overlays

For UI overlays (selection handles, cursors) on top of the canvas:

```xml
<Grid>
    <!-- Board canvas with SkiaSharp rendering -->
    <local:BoardCanvas x:Name="BoardCanvas" 
                       Board="{Binding BoardState}" />
    
    <!-- Overlay canvas for UI elements -->
    <Canvas x:Name="OverlayCanvas" 
            IsHitTestVisible="False">
        <!-- Selection rectangles, cursors, etc. -->
        <Rectangle x:Name="SelectionRect" 
                   Stroke="Blue" 
                   StrokeThickness="1"
                   StrokeDashArray="5,5"
                   IsVisible="False" />
    </Canvas>
</Grid>
```

---

## 3. Input Handling

### Pointer Events (Mouse/Touch/Pen)

```csharp
public class BoardCanvas : Control
{
    private bool _isDrawing;
    private Point _lastPoint;

    public BoardCanvas()
    {
        // Enable pointer events
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
        
        // Enable pen/stylus support
        IsHitTestVisible = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        var position = point.Position;

        // Capture pointer for drag operations
        e.Pointer.Capture(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            _isDrawing = true;
            _lastPoint = position;
            
            // Start new stroke
            StartStroke(position);
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            // Pan or context menu
            ShowContextMenu(position);
        }

        // Handle pen pressure
        if (point.Pointer.Type == PointerType.Pen)
        {
            var pressure = point.Properties.Pressure;
            var isEraser = point.Properties.IsEraser;
            // Adjust brush size based on pressure
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetCurrentPoint(this);
        var position = point.Position;

        if (_isDrawing)
        {
            ContinueStroke(position);
        }
        else
        {
            // Update cursor based on hover
            UpdateCursor(position);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDrawing)
        {
            EndStroke();
            _isDrawing = false;
        }

        // Release pointer capture
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // Zoom with Ctrl+Wheel
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            var zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            ZoomAt(e.GetPosition(this), zoomFactor);
        }
        // Pan vertically with Shift+Wheel
        else if (e.KeyModifiers == KeyModifiers.Shift)
        {
            Pan(0, e.Delta.Y * 10);
        }
        // Pan vertically with Wheel
        else
        {
            Pan(0, e.Delta.Y * 10);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDrawing = false;
    }
}
```

### Keyboard Shortcuts

#### Using KeyBindings (Global Shortcuts)

```xml
<Window xmlns="https://github.com/avaloniaui"
        x:Class="BFGA.App.Views.MainWindow">
    
    <Window.KeyBindings>
        <!-- Tool shortcuts -->
        <KeyBinding Gesture="Ctrl+1" Command="{Binding SelectToolCommand}" CommandParameter="Pen" />
        <KeyBinding Gesture="Ctrl+2" Command="{Binding SelectToolCommand}" CommandParameter="Eraser" />
        <KeyBinding Gesture="Ctrl+3" Command="{Binding SelectToolCommand}" CommandParameter="Select" />
        
        <!-- Action shortcuts -->
        <KeyBinding Gesture="Ctrl+Z" Command="{Binding UndoCommand}" />
        <KeyBinding Gesture="Ctrl+Y" Command="{Binding RedoCommand}" />
        <KeyBinding Gesture="Ctrl+S" Command="{Binding SaveCommand}" />
        
        <!-- View shortcuts -->
        <KeyBinding Gesture="Ctrl+Plus" Command="{Binding ZoomInCommand}" />
        <KeyBinding Gesture="Ctrl+Minus" Command="{Binding ZoomOutCommand}" />
        <KeyBinding Gesture="Ctrl+0" Command="{Binding ResetZoomCommand}" />
        
        <!-- Delete -->
        <KeyBinding Gesture="Delete" Command="{Binding DeleteSelectedCommand}" />
    </Window.KeyBindings>
    
    <!-- Rest of window content -->
</Window>
```

#### Using HotKey on Controls

```xml
<Button Content="Save" 
        Command="{Binding SaveCommand}"
        HotKey="Ctrl+S" />

<ToggleButton Content="Pen"
              IsChecked="{Binding IsPenTool}"
              HotKey="Ctrl+1" />
```

#### Handling Keyboard in Code

```csharp
public class BoardCanvas : Control
{
    public BoardCanvas()
    {
        KeyDown += OnKeyDown;
        Focusable = true; // Must be focusable to receive key events
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Delete:
                DeleteSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                CancelCurrentOperation();
                e.Handled = true;
                break;
            case Key.Z when e.KeyModifiers == KeyModifiers.Control:
                Undo();
                e.Handled = true;
                break;
        }
    }
}
```

### Cursor Management

```csharp
// Set cursor based on tool
void SetToolCursor(ToolType tool)
{
    Cursor = tool switch
    {
        ToolType.Pen => new Cursor(StandardCursorType.Cross),
        ToolType.Eraser => new Cursor(StandardCursorType.IBeam),
        ToolType.Select => new Cursor(StandardCursorType.Arrow),
        ToolType.Hand => new Cursor(StandardCursorType.Hand),
        ToolType.Text => new Cursor(StandardCursorType.IBeam),
        _ => Cursor.Default
    };
}
```

---

## 4. Styling and Theming

### Creating Custom Styles

```xml
<!-- App.axaml -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="BFGA.App.App">
    
    <Application.Styles>
        <!-- Base Fluent theme -->
        <FluentTheme />
        
        <!-- Custom BFGA styles -->
        <StyleInclude Source="/Styles/BFGAStyles.axaml" />
    </Application.Styles>
</Application>
```

### Modern/Minimal Whiteboard Styling

```xml
<!-- Styles/BFGAStyles.axaml -->
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- Semi-transparent floating toolbar -->
    <Style Selector="Border.toolbar">
        <Setter Property="Background" Value="#F0FFFFFF" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="BoxShadow" Value="0 2 8 0 #40000000" />
        <Setter Property="Padding" Value="8" />
        <Setter Property="Margin" Value="8" />
    </Style>

    <!-- Tool button style -->
    <Style Selector="ToggleButton.tool">
        <Setter Property="Width" Value="40" />
        <Setter Property="Height" Value="40" />
        <Setter Property="CornerRadius" Value="6" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
    </Style>
    
    <Style Selector="ToggleButton.tool:checked">
        <Setter Property="Background" Value="#E0E0E0" />
    </Style>
    
    <Style Selector="ToggleButton.tool:pointerover">
        <Setter Property="Background" Value="#F0F0F0" />
    </Style>

    <!-- Property panel style -->
    <Style Selector="Border.propertyPanel">
        <Setter Property="Background" Value="#FAFFFFFF" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="BoxShadow" Value="-2 0 8 0 #20000000" />
        <Setter Property="Width" Value="280" />
        <Setter Property="Padding" Value="16" />
    </Style>

    <!-- Connection dialog style -->
    <Style Selector="Border.connectionDialog">
        <Setter Property="Background" Value="White" />
        <Setter Property="CornerRadius" Value="12" />
        <Setter Property="BoxShadow" Value="0 8 32 0 #40000000" />
        <Setter Property="Padding" Value="32" />
        <Setter Property="MaxWidth" Value="400" />
    </Style>

    <!-- Text input style -->
    <Style Selector="TextBox.modern">
        <Setter Property="CornerRadius" Value="6" />
        <Setter Property="Padding" Value="12,8" />
        <Setter Property="BorderBrush" Value="#E0E0E0" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Background" Value="#FAFAFA" />
    </Style>
    
    <Style Selector="TextBox.modern:focus">
        <Setter Property="BorderBrush" Value="#0078D4" />
        <Setter Property="Background" Value="White" />
    </Style>

</Styles>
```

### Theme Variants (Light/Dark)

```xml
<!-- App.axaml with theme support -->
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="BFGA.App.App"
             RequestedThemeVariant="Dark">  <!-- Default to dark -->
    
    <Application.Styles>
        <FluentTheme>
            <FluentTheme.Palettes>
                <!-- Custom palette for Light variant -->
                <ColorPaletteResources x:Key="Light" 
                                       Accent="#0078D4"
                                       RegionColor="#FFFFFF"
                                       ErrorText="#E81123" />
                <!-- Custom palette for Dark variant -->
                <ColorPaletteResources x:Key="Dark"
                                       Accent="#0099FF"
                                       RegionColor="#1E1E1E"
                                       ErrorText="#FF6B6B" />
            </FluentTheme.Palettes>
        </FluentTheme>
    </Application.Styles>
</Application>
```

### Dynamic Theme Switching

```csharp
// Theme switching in code
void SetTheme(bool isDark)
{
    if (Application.Current is App app)
    {
        app.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}

// Or use ThemeVariantScope for partial theming
```

### Custom Resource Dictionary

```xml
<!-- Resources/ThemeResources.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Colors -->
    <Color x:Key="CanvasBackground">#FFFFFF</Color>
    <Color x:Key="CanvasBackgroundDark">#1E1E1E</Color>
    <Color x:Key="GridLineColor">#E0E0E0</Color>
    <Color x:Key="SelectionColor">#0078D4</Color>
    <Color x:Key="CursorColor">#FF0000</Color>
    
    <!-- Brushes -->
    <SolidColorBrush x:Key="CanvasBackgroundBrush" 
                     Color="{DynamicResource CanvasBackground}" />
    <SolidColorBrush x:Key="GridLineBrush" 
                     Color="{DynamicResource GridLineColor}" />
    
    <!-- Sizes -->
    <x:Double x:Key="ToolbarHeight">56</x:Double>
    <x:Double x:Key="PropertyPanelWidth">280</x:Double>
    <x:Double x:Key="FloatingPanelMargin">16</x:Double>
    
</ResourceDictionary>
```

---

## 5. Custom Controls

### StyledProperty vs DirectProperty

```csharp
public class WhiteboardToolbar : TemplatedControl
{
    // StyledProperty - supports styling, inheritance, animations
    public static readonly StyledProperty<ToolType> SelectedToolProperty =
        AvaloniaProperty.Register<WhiteboardToolbar, ToolType>(
            nameof(SelectedTool),
            defaultValue: ToolType.Pen,
            inherits: false,
            defaultBindingMode: BindingMode.TwoWay);

    public ToolType SelectedTool
    {
        get => GetValue(SelectedToolProperty);
        set => SetValue(SelectedToolProperty, value);
    }

    // DirectProperty - lightweight, no styling overhead
    public static readonly DirectProperty<WhiteboardToolbar, double> StrokeWidthProperty =
        AvaloniaProperty.RegisterDirect<WhiteboardToolbar, double>(
            nameof(StrokeWidth),
            o => o.StrokeWidth,
            (o, v) => o.StrokeWidth = v);

    private double _strokeWidth = 2.0;
    public double StrokeWidth
    {
        get => _strokeWidth;
        set => SetAndRaise(StrokeWidthProperty, ref _strokeWidth, value);
    }

    // Readonly property
    public static readonly DirectProperty<WhiteboardToolbar, bool> IsExpandedProperty =
        AvaloniaProperty.RegisterDirect<WhiteboardToolbar, bool>(
            nameof(IsExpanded),
            o => o.IsExpanded);

    private bool _isExpanded = true;
    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetAndRaise(IsExpandedProperty, ref _isExpanded, value);
    }

    static WhiteboardToolbar()
    {
        // Register properties that affect render
        AffectsRender<WhiteboardToolbar>(SelectedToolProperty);
    }
}
```

### TemplatedControl with Template

```xml
<!-- Themes/Generic.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="using:BFGA.App.Controls">
    
    <ControlTheme x:Key="{x:Type local:WhiteboardToolbar}"
                  TargetType="local:WhiteboardToolbar">
        <Setter Property="Template">
            <ControlTemplate>
                <Border Classes="toolbar"
                        Background="{TemplateBinding Background}">
                    <StackPanel Orientation="Horizontal" Spacing="4">
                        <!-- Pen tool -->
                        <ToggleButton Classes="tool"
                                      IsChecked="{TemplateBinding SelectedTool, 
                                          Converter={StaticResource ToolConverter},
                                          ConverterParameter=Pen}" />
                        
                        <!-- Eraser tool -->
                        <ToggleButton Classes="tool"
                                      IsChecked="{TemplateBinding SelectedTool,
                                          Converter={StaticResource ToolConverter},
                                          ConverterParameter=Eraser}" />
                        
                        <!-- Separator -->
                        <Rectangle Width="1" 
                                   Height="24" 
                                   Fill="#E0E0E0" 
                                   Margin="4,0" />
                        
                        <!-- Stroke width slider -->
                        <Slider Minimum="1" 
                                Maximum="50" 
                                Value="{TemplateBinding StrokeWidth}" 
                                Width="100" />
                    </StackPanel>
                </Border>
            </ControlTemplate>
        </Setter>
    </ControlTheme>
    
</ResourceDictionary>
```

### UserControl for Page Views

```xml
<!-- Views/BoardView.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:BFGA.App.ViewModels"
             xmlns:canvas="using:BFGA.Canvas"
             x:Class="BFGA.App.Views.BoardView"
             x:DataType="vm:BoardViewModel">
    
    <Design.DataContext>
        <vm:BoardViewModel />
    </Design.DataContext>
    
    <DockPanel>
        <!-- Top toolbar -->
        <Border DockPanel.Dock="Top" Classes="toolbar">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Button Command="{Binding UndoCommand}">
                    <PathIcon Data="{StaticResource UndoIcon}" />
                </Button>
                <Button Command="{Binding RedoCommand}">
                    <PathIcon Data="{StaticResource RedoIcon}" />
                </Button>
                <Separator />
                <ComboBox ItemsSource="{Binding AvailableTools}"
                          SelectedItem="{Binding SelectedTool}" />
            </StackPanel>
        </Border>
        
        <!-- Left toolbar -->
        <Border DockPanel.Dock="Left" Classes="toolbar">
            <StackPanel Orientation="Vertical" Spacing="4">
                <!-- Tool buttons -->
            </StackPanel>
        </Border>
        
        <!-- Main canvas area -->
        <Panel>
            <canvas:BoardViewport x:Name="Viewport"
                                  Board="{Binding BoardState}"
                                  Zoom="{Binding Zoom}"
                                  Offset="{Binding PanOffset}" />
            
            <!-- Floating panels -->
            <Border HorizontalAlignment="Right" 
                    VerticalAlignment="Top"
                    Classes="propertyPanel"
                    IsVisible="{Binding HasSelection}">
                <!-- Property editors -->
            </Border>
        </Panel>
    </DockPanel>
</UserControl>
```

---

## 6. Layout Controls

### DockPanel for Main Layout

```xml
<DockPanel>
    <!-- Top toolbar -->
    <Border DockPanel.Dock="Top" Height="56">
        <!-- Toolbar content -->
    </Border>
    
    <!-- Left tool palette -->
    <Border DockPanel.Dock="Left" Width="64">
        <!-- Tool buttons -->
    </Border>
    
    <!-- Right properties panel -->
    <Border DockPanel.Dock="Right" Width="280">
        <!-- Property editors -->
    </Border>
    
    <!-- Bottom status bar -->
    <Border DockPanel.Dock="Bottom" Height="24">
        <!-- Status info -->
    </Border>
    
    <!-- Main canvas fills remaining space -->
    <Grid>
        <local:BoardCanvas />
    </Grid>
</DockPanel>
```

### Grid for Complex Layouts

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />    <!-- Toolbar -->
        <RowDefinition Height="*" />       <!-- Canvas -->
        <RowDefinition Height="Auto" />    <!-- Status -->
    </Grid.RowDefinitions>
    
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto" />  <!-- Tools -->
        <ColumnDefinition Width="*" />     <!-- Canvas -->
        <ColumnDefinition Width="Auto" />  <!-- Properties -->
    </Grid.ColumnDefinitions>
    
    <!-- Toolbar spans all columns -->
    <Border Grid.Row="0" Grid.Column="0" Grid.ColumnSpan="3" />
    
    <!-- Tools -->
    <Border Grid.Row="1" Grid.Column="0" />
    
    <!-- Canvas -->
    <Border Grid.Row="1" Grid.Column="1">
        <local:BoardCanvas />
    </Border>
    
    <!-- Properties -->
    <Border Grid.Row="1" Grid.Column="2" />
    
    <!-- Status spans all columns -->
    <Border Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="3" />
</Grid>
```

### Flyout for Tool Sub-menus

```xml
<Button Content="Stroke Color">
    <Button.Flyout>
        <Flyout Placement="Bottom">
            <ColorPicker SelectedColor="{Binding StrokeColor}" />
        </Flyout>
    </Button.Flyout>
</Button>

<!-- Or with attached flyout for any control -->
<Border Background="Red" 
        PointerPressed="OnColorPreviewPressed"
        FlyoutBase.AttachedFlyout="{StaticResource ColorFlyout}" />
```

```csharp
// Show attached flyout programmatically
void OnColorPreviewPressed(object sender, PointerPressedEventArgs e)
{
    if (sender is Control control)
    {
        FlyoutBase.ShowAttachedFlyout(control);
    }
}
```

### Popup for Floating Panels

```xml
<Popup IsOpen="{Binding IsColorPickerOpen}"
       PlacementTarget="{Binding ElementName=ColorButton}"
       Placement="Bottom"
       StaysOpen="False">
    <Border Background="White" 
            CornerRadius="8" 
            BoxShadow="0 4 16 0 #40000000"
            Padding="16">
        <ColorPicker SelectedColor="{Binding SelectedColor}" />
    </Border>
</Popup>
```

### Canvas for Absolute Positioning (Overlays)

```xml
<!-- Use Canvas for overlays that need absolute positioning -->
<Canvas x:Name="OverlayCanvas">
    <!-- Selection rectangle -->
    <Rectangle x:Name="SelectionRect"
               Canvas.Left="{Binding SelectionBounds.X}"
               Canvas.Top="{Binding SelectionBounds.Y}"
               Width="{Binding SelectionBounds.Width}"
               Height="{Binding SelectionBounds.Height}"
               Stroke="Blue"
               StrokeThickness="1"
               StrokeDashArray="5,5"
               IsVisible="{Binding HasSelection}" />
    
    <!-- User cursors -->
    <ItemsControl ItemsSource="{Binding RemoteCursors}">
        <ItemsControl.ItemTemplate>
            <DataTemplate DataType="vm:CursorViewModel">
                <Canvas Left="{Binding X}" Top="{Binding Y}">
                    <Path Data="M0,0 L12,4 L4,12 Z" Fill="{Binding Color}" />
                    <TextBlock Text="{Binding UserName}" 
                               Margin="12,0,0,0"
                               FontSize="10" />
                </Canvas>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</Canvas>
```

---

## 7. Data Binding and MVVM

### INotifyPropertyChanged with CommunityToolkit

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// ObservableObject implements INotifyPropertyChanged
public partial class BoardViewModel : ObservableObject
{
    // Auto-generates property with change notification
    [ObservableProperty]
    private BoardState? _boardState;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private Point _panOffset;

    [ObservableProperty]
    private ToolType _selectedTool = ToolType.Pen;

    [ObservableProperty]
    private bool _hasSelection;

    // Auto-generates command
    [RelayCommand]
    private void Undo()
    {
        BoardState?.Undo();
    }

    [RelayCommand]
    private void Redo()
    {
        BoardState?.Redo();
    }

    [RelayCommand]
    private void ZoomIn()
    {
        Zoom *= 1.2;
    }

    [RelayCommand]
    private void ZoomOut()
    {
        Zoom /= 1.2;
    }

    [RelayCommand]
    private void ResetZoom()
    {
        Zoom = 1.0;
        PanOffset = new Point(0, 0);
    }

    [RelayCommand]
    private void SelectTool(ToolType tool)
    {
        SelectedTool = tool;
    }

    // Command with CanExecute
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteSelected()
    {
        // Delete selected elements
    }

    private bool CanDelete => HasSelection;
}
```

### Binding in XAML

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="BFGA.App.Views.BoardView"
             x:DataType="vm:BoardViewModel">
    
    <!-- One-way binding (display only) -->
    <TextBlock Text="{Binding Zoom, StringFormat='{}{0:P0}'}" />
    
    <!-- Two-way binding (editable) -->
    <TextBox Text="{Binding BoardName}" />
    <Slider Value="{Binding Zoom}" Minimum="0.1" Maximum="5.0" />
    
    <!-- Command binding -->
    <Button Content="Undo" Command="{Binding UndoCommand}" />
    
    <!-- Command with parameter -->
    <Button Content="Select Pen" 
            Command="{Binding SelectToolCommand}"
            CommandParameter="{x:Static local:ToolType.Pen}" />
    
    <!-- Binding with converter -->
    <TextBlock Text="{Binding ElementCount, Converter={StaticResource CountConverter}}" />
    
    <!-- Binding to ancestor -->
    <TextBlock Text="{Binding $parent[Window].DataContext.StatusMessage}" />
    
    <!-- Compiled bindings (better performance) -->
    <TextBlock Text="{Binding BoardName, Mode=OneWay}" 
               x:CompileBindings="True" />
    
</UserControl>
```

### DataContext Setup

```xml
<!-- In code-behind or View -->
<UserControl.DataContext>
    <vm:BoardViewModel />
</UserControl.DataContext>

<!-- Or set in parent -->
<ContentControl DataContext="{Binding CurrentViewModel}">
    <ContentControl.DataTemplates>
        <DataTemplate DataType="vm:BoardViewModel">
            <views:BoardView />
        </DataTemplate>
    </ContentControl.DataTemplates>
</ContentControl>
```

### Binding Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| `OneWay` | Source → Target only | Display values |
| `TwoWay` | Bidirectional | User input fields |
| `OneTime` | Source → Target once | Static content |
| `OneWayToSource` | Target → Source only | Special scenarios |
| `Default` | Property determines | Most properties |

```xml
<!-- Explicit binding modes -->
<TextBlock Text="{Binding Status, Mode=OneWay}" />
<TextBox Text="{Binding UserName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
```

---

## 8. Pan and Zoom

### Using PanAndZoom Library

The project already uses the `PanAndZoom` library via `ZoomBorder`.

```xml
<!-- BoardViewport.axaml -->
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:paz="using:Avalonia.Controls.PanAndZoom"
             x:Class="BFGA.Canvas.BoardViewport">
    
    <paz:ZoomBorder x:Name="ZoomBorder"
                    Stretch="None"
                    ZoomSpeed="1.2"
                    EnablePan="True"
                    EnableZoom="True"
                    EnableGesture="True"
                    ClipToBounds="True">
        
        <!-- The canvas content -->
        <local:BoardCanvas x:Name="BoardCanvas"
                           Board="{Binding Board, RelativeSource={RelativeSource AncestorType=UserControl}}" />
    </paz:ZoomBorder>
</UserControl>
```

### Programmatic Zoom Control

```csharp
public class BoardViewport : UserControl
{
    private ZoomBorder? _zoomBorder;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _zoomBorder = e.NameScope.Find<ZoomBorder>("ZoomBorder");
    }

    // Zoom to specific level
    public void ZoomTo(double zoomLevel)
    {
        _zoomBorder?.ZoomTo(zoomLevel);
    }

    // Zoom to fit content
    public void ZoomToFit()
    {
        _zoomBorder?.ZoomToFit();
    }

    // Zoom to fill
    public void ZoomToFill()
    {
        _zoomBorder?.ZoomToFill();
    }

    // Reset zoom and pan
    public void ResetZoom()
    {
        _zoomBorder?.ResetMatrix();
    }

    // Zoom at specific point
    public void ZoomAt(Point point, double zoomLevel)
    {
        _zoomBorder?.ZoomTo(point.X, point.Y, zoomLevel);
    }

    // Get current transform matrix
    public Matrix GetTransformMatrix()
    {
        return _zoomBorder?.Matrix ?? Matrix.Identity;
    }

    // Convert screen to board coordinates
    public Point ScreenToBoard(Point screenPoint)
    {
        if (_zoomBorder == null) return screenPoint;
        
        var matrix = _zoomBorder.Matrix;
        matrix.Invert();
        return matrix.Transform(screenPoint);
    }

    // Convert board to screen coordinates
    public Point BoardToScreen(Point boardPoint)
    {
        if (_zoomBorder == null) return boardPoint;
        
        return _zoomBorder.Matrix.Transform(boardPoint);
    }
}
```

### Coordinate Transform Helper

```csharp
public static class CoordinateTransformHelper
{
    /// <summary>
    /// Transforms a point from screen coordinates to board coordinates
    /// </summary>
    public static Point ScreenToBoard(Point screenPoint, Matrix transformMatrix)
    {
        var inverted = transformMatrix;
        inverted.Invert();
        return inverted.Transform(screenPoint);
    }

    /// <summary>
    /// Transforms a point from board coordinates to screen coordinates
    /// </summary>
    public static Point BoardToScreen(Point boardPoint, Matrix transformMatrix)
    {
        return transformMatrix.Transform(boardPoint);
    }

    /// <summary>
    /// Transforms a rectangle from screen to board coordinates
    /// </summary>
    public static Rect ScreenToBoardRect(Rect screenRect, Matrix transformMatrix)
    {
        var topLeft = ScreenToBoard(screenRect.TopLeft, transformMatrix);
        var bottomRight = ScreenToBoard(screenRect.BottomRight, transformMatrix);
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>
    /// Transforms a length from screen to board (accounts for zoom only)
    /// </summary>
    public static double ScreenToBoardLength(double screenLength, double zoom)
    {
        return screenLength / zoom;
    }

    /// <summary>
    /// Transforms a length from board to screen
    /// </summary>
    public static double BoardToScreenLength(double boardLength, double zoom)
    {
        return boardLength * zoom;
    }
}
```

---

## 9. Rendering Performance

### CompositionCustomVisual for Hardware Acceleration

For high-performance continuous rendering (e.g., during active drawing):

```csharp
using Avalonia.Rendering.Composition;

public class HighPerformanceCanvas : Control
{
    private CompositionCustomVisualHandler? _handler;
    private CompositionVisual? _customVisual;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var visual = ElementComposition.GetElementVisual(this);
        if (visual == null) return;

        var compositor = visual.Compositor;
        
        _handler = new BoardCustomVisualHandler(
            OnRenderCallback,
            OnMessageCallback);
        
        _customVisual = compositor.CreateCustomVisual(_handler);
        _customVisual.Size = new Vector2((float)Bounds.Width, (float)Bounds.Height);
    }

    private void OnRenderCallback(
        CompositionCustomVisualHandler handler,
        SKCanvas canvas,
        RenderBounds bounds)
    {
        // This runs on render thread - fast!
        // Draw with SkiaSharp directly
    }

    private void OnMessageCallback(
        CompositionCustomVisualHandler handler,
        object message)
    {
        // Handle messages from UI thread
    }
}

public class BoardCustomVisualHandler : CompositionCustomVisualHandler
{
    private readonly Action<CompositionCustomVisualHandler, SKCanvas, RenderBounds> _onRender;
    private readonly Action<CompositionCustomVisualHandler, object> _onMessage;

    public BoardCustomVisualHandler(
        Action<CompositionCustomVisualHandler, SKCanvas, RenderBounds> onRender,
        Action<CompositionCustomVisualHandler, object> onMessage)
    {
        _onRender = onRender;
        _onMessage = onMessage;
    }

    public override void OnRender(
        CompositionCustomVisualHandler handler,
        SKCanvas canvas,
        RenderBounds bounds)
    {
        _onRender(handler, canvas, bounds);
    }

    public override void OnMessage(
        CompositionCustomVisualHandler handler,
        object message)
    {
        _onMessage(handler, message);
    }
}
```

### Minimizing Redraws

```csharp
public class BoardCanvas : Control
{
    private Rect _invalidatedRegion;

    // Invalidate only specific region instead of entire canvas
    public void InvalidateRegion(Rect region)
    {
        _invalidatedRegion = region;
        InvalidateVisual();
    }

    // Use AffectsRender for properties that should trigger redraw
    static BoardCanvas()
    {
        AffectsRender<BoardCanvas>(
            BoardProperty,
            GridVisibleProperty,
            ZoomProperty);
    }

    // Batch multiple changes
    public void UpdateElements(IEnumerable<BoardElement> elements)
    {
        // Suspend rendering during batch update
        using var _ = SuspendRendering();
        
        foreach (var element in elements)
        {
            UpdateElement(element);
        }
    }

    private IDisposable SuspendRendering()
    {
        // Implementation to batch updates
        return new RenderingSuspension(this);
    }
}
```

### RenderOptions

```xml
<!-- Optimize rendering for specific scenarios -->
<local:BoardCanvas RenderOptions.BitmapInterpolationMode="LowQuality" />

<!-- Or in code for performance-critical paths -->
RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.LowQuality);
RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Aliased);
```

### Caching Strategies

```csharp
public class ElementDrawingHelper
{
    // Cache decoded images
    private readonly ConcurrentDictionary<string, SKBitmap> _imageCache = new();

    public SKBitmap? GetCachedImage(string imageId, Func<Stream> imageLoader)
    {
        return _imageCache.GetOrAdd(imageId, _ =>
        {
            using var stream = imageLoader();
            return SKBitmap.Decode(stream);
        });
    }

    // Cache rendered elements that don't change often
    private readonly ConditionalWeakSet<BoardElement, RenderTargetBitmap> _renderCache = new();
}
```

---

## 10. Assets and Icons

### Including Assets

```xml
<!-- Project file -->
<ItemGroup>
    <AvaloniaResource Include="Assets\**" />
</ItemGroup>
```

### Using SVG Icons

```xml
<!-- App.axaml - define icons as resources -->
<Application.Resources>
    <StreamGeometry x:Key="PenIcon">M20.7,7.04 ...</StreamGeometry>
    <StreamGeometry x:Key="EraserIcon">M16.24,3.56 ...</StreamGeometry>
    <StreamGeometry x:Key="UndoIcon">M12.5,8C9.85,8 7.45,8.67 5.4,9.76 ...</StreamGeometry>
</Application.Resources>
```

```xml
<!-- Using icons in controls -->
<ToggleButton Classes="tool">
    <PathIcon Data="{StaticResource PenIcon}" 
              Width="24" 
              Height="24" />
</ToggleButton>

<Button Command="{Binding UndoCommand}">
    <PathIcon Data="{StaticResource UndoIcon}" />
</Button>
```

### Loading Images

```csharp
// From Avalonia resources
var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
var uri = new Uri("avares://BFGA.App/Assets/logo.png");
using var stream = assets.Open(uri);
var bitmap = new Bitmap(stream);

// From file
var bitmap = new Bitmap("path/to/image.png");

// Async loading
public async Task<IImage?> LoadImageAsync(string path)
{
    await using var stream = File.OpenRead(path);
    return new Bitmap(stream);
}
```

### Image Binding

```xml
<Image Source="{Binding BoardThumbnail}" 
       Width="200" 
       Height="150"
       Stretch="Uniform" />

<!-- With fallback -->
<Image Source="{Binding UserAvatar, FallbackValue={StaticResource DefaultAvatar}}" />
```

### Converters for Assets

```csharp
public class PathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && File.Exists(path))
        {
            try
            {
                return new Bitmap(path);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

---

## Quick Reference

### Common Avalonia Namespaces

```xml
xmlns="https://github.com/avaloniaui"
xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
xmlns:vm="using:BFGA.App.ViewModels"
xmlns:local="using:BFGA.App"
```

### Avalonia Property System

```csharp
// StyledProperty (full styling support)
public static readonly StyledProperty<T> Property =
    AvaloniaProperty.Register<Owner, T>(nameof(Property), defaultValue);

// DirectProperty (lightweight)
public static readonly DirectProperty<Owner, T> Property =
    AvaloniaProperty.RegisterDirect<Owner, T>(
        nameof(Property),
        o => o.Property,
        (o, v) => o.Property = v);

// AttachedProperty
public static readonly AttachedProperty<T> Property =
    AvaloniaProperty.RegisterAttached<Owner, Target, T>("Property");
```

### Event Handling Patterns

```csharp
// Routed events
void OnClick(object sender, RoutedEventArgs e)

// Pointer events
void OnPointerPressed(object sender, PointerPressedEventArgs e)
void OnPointerMoved(object sender, PointerEventArgs e)
void OnPointerReleased(object sender, PointerReleasedEventArgs e)

// Keyboard events
void OnKeyDown(object sender, KeyEventArgs e)

// Property changed
void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
```

### Threading

```csharp
// UI thread operations
Dispatcher.UIThread.Post(() => { /* UI update */ });
await Dispatcher.UIThread.InvokeAsync(() => { /* UI update */ });

// Background thread
await Task.Run(() => { /* Background work */ });
```

---

## References

- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [Avalonia GitHub](https://github.com/AvaloniaUI/Avalonia)
- [SkiaSharp Documentation](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/user-interface/graphics/skiasharp/)
- [PanAndZoom Library](https://github.com/wieslawsoltes/PanAndZoom)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/windows/communitytoolkit/mvvm/)

---

*This guide is specific to the BFGA project and covers patterns needed for the collaborative whiteboard implementation.*
