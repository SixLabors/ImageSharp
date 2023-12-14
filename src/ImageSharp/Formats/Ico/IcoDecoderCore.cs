// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Ico;

internal sealed class IcoDecoderCore(DecoderOptions options) : IconDecoderCore(options)
{
    protected override void SetFrameMetadata(ImageFrameMetadata metadata, in IconDirEntry entry, IconFrameCompression compression, Bmp.BmpBitsPerPixel bitsPerPixel)
    {
        IcoFrameMetadata icoFrameMetadata = metadata.GetIcoMetadata();
        icoFrameMetadata.FromIconDirEntry(entry);
        icoFrameMetadata.Compression = compression;
        icoFrameMetadata.BitsPerPixel = bitsPerPixel;
    }
}
