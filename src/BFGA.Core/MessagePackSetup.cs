using BFGA.Core.Serialization;
using MessagePack;
using MessagePack.Resolvers;

namespace BFGA.Core;

public static class MessagePackSetup
{
    public static readonly MessagePackSerializerOptions Options;

    static MessagePackSetup()
    {
        var resolver = CompositeResolver.Create(
            new Vector2Formatter(),
            new SKColorFormatter(),
            StandardResolver.Instance
        );

        Options = MessagePackSerializerOptions.Standard
            .WithResolver(resolver);
    }
}
