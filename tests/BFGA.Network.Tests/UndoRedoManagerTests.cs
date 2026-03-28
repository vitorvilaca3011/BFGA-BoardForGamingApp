using System.Numerics;
using BFGA.Core.Models;
using BFGA.Network.Protocol;
using SkiaSharp;

namespace BFGA.Network.Tests;

public class UndoRedoManagerTests
{
    private readonly UndoRedoManager _sut = new();
    private readonly Guid _user1 = Guid.NewGuid();
    private readonly Guid _user2 = Guid.NewGuid();

    [Fact]
    public void CanUndo_FalseWhenEmpty()
    {
        Assert.False(_sut.CanUndo(_user1));
    }

    [Fact]
    public void CanRedo_FalseWhenEmpty()
    {
        Assert.False(_sut.CanRedo(_user1));
    }

    [Fact]
    public void Push_MakesCanUndoTrue()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);

        Assert.True(_sut.CanUndo(_user1));
    }

    [Fact]
    public void Undo_ReturnsInverseOp()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);

        var result = _sut.TryUndo(_user1);

        Assert.NotNull(result);
        Assert.IsType<DeleteElementOperation>(result);
    }

    [Fact]
    public void Undo_MakesCanRedoTrue()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);
        _sut.TryUndo(_user1);

        Assert.True(_sut.CanRedo(_user1));
        Assert.False(_sut.CanUndo(_user1));
    }

    [Fact]
    public void Redo_ReturnsForwardOp()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);
        _sut.TryUndo(_user1);

        var result = _sut.TryRedo(_user1);

        Assert.NotNull(result);
        Assert.IsType<AddElementOperation>(result);
    }

    [Fact]
    public void NewPush_ClearsRedoStack()
    {
        var op1 = CreateAddOp();
        _sut.Push(_user1, op1, CreateDeleteOp(op1));
        _sut.TryUndo(_user1);
        Assert.True(_sut.CanRedo(_user1));

        var op2 = CreateAddOp();
        _sut.Push(_user1, op2, CreateDeleteOp(op2));
        Assert.False(_sut.CanRedo(_user1));
    }

    [Fact]
    public void PerUser_StacksAreIsolated()
    {
        var op1 = CreateAddOp();
        _sut.Push(_user1, op1, CreateDeleteOp(op1));

        Assert.True(_sut.CanUndo(_user1));
        Assert.False(_sut.CanUndo(_user2));
    }

    [Fact]
    public void MaxDepth_DiscardsOldest()
    {
        for (int i = 0; i < 55; i++)
        {
            var op = CreateAddOp();
            _sut.Push(_user1, op, CreateDeleteOp(op));
        }

        int undoCount = 0;
        while (_sut.TryUndo(_user1) is not null)
            undoCount++;

        Assert.Equal(50, undoCount);
    }

    [Fact]
    public void ClearUser_RemovesBothStacks()
    {
        var op = CreateAddOp();
        _sut.Push(_user1, op, CreateDeleteOp(op));
        _sut.TryUndo(_user1);

        _sut.ClearUser(_user1);

        Assert.False(_sut.CanUndo(_user1));
        Assert.False(_sut.CanRedo(_user1));
    }

    [Fact]
    public void Redo_ReturnsClonedAddSnapshot_NotMutatedLiveElement()
    {
        var forward = CreateAddOp();
        var inverse = CreateDeleteOp(forward);
        _sut.Push(_user1, forward, inverse);

        _sut.TryUndo(_user1);

        ((StrokeElement)forward.Element).Points.Add(new Vector2(999, 999));

        var redone = _sut.TryRedo(_user1);

        var add = Assert.IsType<AddElementOperation>(redone);
        var stroke = Assert.IsType<StrokeElement>(add.Element);
        Assert.DoesNotContain(new Vector2(999, 999), stroke.Points);
    }

    private static AddElementOperation CreateAddOp()
    {
        var element = new StrokeElement { Id = Guid.NewGuid() };
        return new AddElementOperation { Element = element };
    }

    private static DeleteElementOperation CreateDeleteOp(AddElementOperation addOp)
        => new() { ElementId = addOp.Element.Id };
}
