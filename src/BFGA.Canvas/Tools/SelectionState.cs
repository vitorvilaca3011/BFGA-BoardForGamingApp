namespace BFGA.Canvas.Tools;

public sealed class SelectionState
{
    private readonly List<Guid> _selectedElementIds = new();

    public IReadOnlyCollection<Guid> SelectedElementIds => _selectedElementIds;

    public Guid? ActiveElementId { get; private set; }

    public SelectionHandleKind ActiveHandle { get; private set; } = SelectionHandleKind.None;

    public void Clear()
    {
        _selectedElementIds.Clear();
        ActiveElementId = null;
        ActiveHandle = SelectionHandleKind.None;
    }

    public void Select(Guid elementId)
    {
        _selectedElementIds.Clear();
        _selectedElementIds.Add(elementId);
        ActiveElementId = elementId;
        ActiveHandle = SelectionHandleKind.None;
    }

    public void SelectMany(IEnumerable<Guid> elementIds)
    {
        _selectedElementIds.Clear();
        _selectedElementIds.AddRange(elementIds.Distinct());
        ActiveElementId = _selectedElementIds.Count == 1 ? _selectedElementIds[0] : null;
        ActiveHandle = SelectionHandleKind.None;
    }

    public void EndInteraction()
    {
        ActiveHandle = SelectionHandleKind.None;
    }

    public bool Contains(Guid elementId) => _selectedElementIds.Contains(elementId);

    public void BeginHandleInteraction(Guid elementId, SelectionHandleKind handleKind)
    {
        ActiveElementId = elementId;
        ActiveHandle = handleKind;
    }

    public void BeginMoveInteraction()
    {
        ActiveHandle = SelectionHandleKind.Move;
    }
}
