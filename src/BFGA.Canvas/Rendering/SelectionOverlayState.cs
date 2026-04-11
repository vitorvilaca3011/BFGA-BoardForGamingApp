using BFGA.Canvas.Tools;
using SkiaSharp;

namespace BFGA.Canvas.Rendering;

public sealed record SelectionOverlayState(
    IReadOnlyCollection<Guid> SelectedElementIds,
    Guid? ActiveElementId,
    IReadOnlyList<SelectionHandle> Handles,
    SKRect? SelectionBox);
