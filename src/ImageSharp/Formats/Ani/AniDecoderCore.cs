// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Ani;

internal class AniDecoderCore : ImageDecoderCore
{
    public AniDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    protected override Image<TPixel> Decode<TPixel>(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        this.ReadHeader(stream);
        Span<byte> buffer = stackalloc byte[4];
        _ = stream.Read(buffer);
        uint type = BitConverter.ToUInt32(buffer);
        switch (type)
        {
            case 0x73_65_71_20: // seq
                break;
            case 0x72_61_74_65: // rate
                break;
            case 0x4C_49_53_54: // list
                break;
            default:
                break;
        }

        throw new NotImplementedException();
    }

    protected override ImageInfo Identify(BufferedReadStream stream, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private void ReadHeader(Stream stream)
    {
        // Skip the identifier
        stream.Skip(12);
        Span<byte> buffer = stackalloc byte[36];
        _ = stream.Read(buffer);
        AniHeader header = AniHeader.Parse(buffer);
    }
}
