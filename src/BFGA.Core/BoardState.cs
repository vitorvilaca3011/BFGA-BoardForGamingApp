using BFGA.Core.Models;
using MessagePack;

namespace BFGA.Core;

[MessagePackObject]
public class BoardState
{
    [Key(0)]
    public Guid BoardId { get; set; } = Guid.NewGuid();

    [Key(1)]
    public string BoardName { get; set; } = "Untitled";

    [Key(2)]
    public List<BoardElement> Elements { get; set; } = new();

    [Key(3)]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
