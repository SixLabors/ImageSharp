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
        Span<byte> buffer = stackalloc byte[AniHeader.Size];
        _ = stream.Read(buffer);
        AniHeader header = AniHeader.Parse(buffer);
    }
}
