using BFGA.Core.Serialization;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace BFGA.Core;

public static class MessagePackSetup
{
    public static readonly MessagePackSerializerOptions Options;

    static MessagePackSetup()
    {
        // CompositeResolver.Create is the recommended approach for MessagePack 2.5+
        // DynamicUnionResolver must come BEFORE StandardResolver to handle [Union] attributes
        var resolver = CompositeResolver.Create(
            new IMessagePackFormatter[]
            {
                new Vector2Formatter(),
                new SKColorFormatter(),
            },
            new IFormatterResolver[]
            {
                DynamicUnionResolver.Instance, // Handles BoardElement and BoardOperation unions
                StandardResolver.Instance,     // Fallback for standard types
            }
        );

        Options = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .WithResolver(resolver);
    }
}
