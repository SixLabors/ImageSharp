// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Cur;

internal sealed class CurDecoderCore : IconDecoderCore
{
    public CurDecoderCore(DecoderOptions options)
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
        CurFrameMetadata curFrameMetadata = frameMetadata.GetCurMetadata();
        curFrameMetadata.FromIconDirEntry(entry);
        curFrameMetadata.Compression = compression;
        curFrameMetadata.BmpBitsPerPixel = bitsPerPixel;
        curFrameMetadata.ColorTable = colorTable;

        if (index == 0)
        {
            CurMetadata curMetadata = imageMetadata.GetCurMetadata();
            curMetadata.Compression = compression;
            curMetadata.BmpBitsPerPixel = bitsPerPixel;
            curMetadata.ColorTable = colorTable;
        }
    }
}
