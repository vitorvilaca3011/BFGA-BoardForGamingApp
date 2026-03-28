using BFGA.Network.Protocol;
using MessagePack;

namespace BFGA.Network;

public sealed class UndoRedoManager
{
    private const int MaxDepth = 50;

    private readonly Dictionary<Guid, LinkedList<UndoEntry>> _undoStacks = new();
    private readonly Dictionary<Guid, Stack<UndoEntry>> _redoStacks = new();

    public bool CanUndo(Guid userId)
        => _undoStacks.TryGetValue(userId, out var stack) && stack.Count > 0;

    public bool CanRedo(Guid userId)
        => _redoStacks.TryGetValue(userId, out var stack) && stack.Count > 0;

    public void Push(Guid userId, BoardOperation forward, BoardOperation inverse)
    {
        if (!_undoStacks.TryGetValue(userId, out var undoStack))
        {
            undoStack = new LinkedList<UndoEntry>();
            _undoStacks[userId] = undoStack;
        }

        undoStack.AddLast(new UndoEntry(CloneOperation(forward), CloneOperation(inverse)));

        while (undoStack.Count > MaxDepth)
            undoStack.RemoveFirst();

        if (_redoStacks.TryGetValue(userId, out var redoStack))
            redoStack.Clear();
    }

    public BoardOperation? TryUndo(Guid userId)
    {
        if (!_undoStacks.TryGetValue(userId, out var undoStack) || undoStack.Count == 0)
            return null;

        var entry = undoStack.Last!.Value;
        undoStack.RemoveLast();

        if (!_redoStacks.TryGetValue(userId, out var redoStack))
        {
            redoStack = new Stack<UndoEntry>();
            _redoStacks[userId] = redoStack;
        }

        redoStack.Push(entry);

        return CloneOperation(entry.InverseOp);
    }

    public BoardOperation? TryRedo(Guid userId)
    {
        if (!_redoStacks.TryGetValue(userId, out var redoStack) || redoStack.Count == 0)
            return null;

        var entry = redoStack.Pop();

        if (!_undoStacks.TryGetValue(userId, out var undoStack))
        {
            undoStack = new LinkedList<UndoEntry>();
            _undoStacks[userId] = undoStack;
        }

        undoStack.AddLast(entry);

        return CloneOperation(entry.ForwardOp);
    }

    public void ClearUser(Guid userId)
    {
        _undoStacks.Remove(userId);
        _redoStacks.Remove(userId);
    }

    private static BoardOperation CloneOperation(BoardOperation operation)
        => MessagePackSerializer.Deserialize<BoardOperation>(
            MessagePackSerializer.Serialize(operation, BFGA.Core.MessagePackSetup.Options),
            BFGA.Core.MessagePackSetup.Options);

    private sealed record UndoEntry(BoardOperation ForwardOp, BoardOperation InverseOp);
}
