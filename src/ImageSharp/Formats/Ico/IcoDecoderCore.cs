// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Ico;

internal sealed class IcoDecoderCore : IconDecoderCore
{
    public IcoDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    protected override void SetFrameMetadata(ImageFrameMetadata metadata, in IconDirEntry entry, IconFrameCompression compression, BmpBitsPerPixel bitsPerPixel)
    {
        IcoFrameMetadata icoFrameMetadata = metadata.GetIcoMetadata();
        icoFrameMetadata.FromIconDirEntry(entry);
        icoFrameMetadata.Compression = compression;
        icoFrameMetadata.BmpBitsPerPixel = bitsPerPixel;
    }
}
