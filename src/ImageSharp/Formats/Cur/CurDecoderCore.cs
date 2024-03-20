// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Cur;

internal sealed class CurDecoderCore(DecoderOptions options) : IconDecoderCore(options)
{
    protected override void SetFrameMetadata(ImageFrameMetadata metadata, in IconDirEntry entry, IconFrameCompression compression, Bmp.BmpBitsPerPixel bitsPerPixel)
    {
        CurFrameMetadata curFrameMetadata = metadata.GetCurMetadata();
        curFrameMetadata.FromIconDirEntry(entry);
        curFrameMetadata.Compression = compression;
        curFrameMetadata.BitsPerPixel = bitsPerPixel;
    }
}
