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

    protected override void SetFrameMetadata(
        ImageMetadata imageMetadata,
        ImageFrameMetadata frameMetadata,
        int index,
        in IconDirEntry entry,
        IconFrameCompression compression,
        BmpBitsPerPixel bitsPerPixel,
        ReadOnlyMemory<Color>? colorTable)
    {
        IcoFrameMetadata icoFrameMetadata = frameMetadata.GetIcoMetadata();
        icoFrameMetadata.FromIconDirEntry(entry);
        icoFrameMetadata.Compression = compression;
        icoFrameMetadata.BmpBitsPerPixel = bitsPerPixel;
        icoFrameMetadata.ColorTable = colorTable;

        if (index == 0)
        {
            IcoMetadata curMetadata = imageMetadata.GetIcoMetadata();
            curMetadata.Compression = compression;
            curMetadata.BmpBitsPerPixel = bitsPerPixel;
            curMetadata.ColorTable = colorTable;
        }
    }
}
