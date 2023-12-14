// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;

// TODO: flatten namespace.
// namespace SixLabors.ImageSharp.Formats.Cur;
namespace SixLabors.ImageSharp.Formats.Icon.Cur;

internal sealed class CurDecoderCore : IconDecoderCore
{
    public CurDecoderCore(DecoderOptions options)
        : base(options)
    {
    }

    protected override void SetFrameMetadata(ImageFrameMetadata metadata, in IconDirEntry entry, IconFrameCompression compression, Bmp.BmpBitsPerPixel bitsPerPixel)
    {
        CurFrameMetadata curFrameMetadata = metadata.GetCurMetadata();
        curFrameMetadata.FromIconDirEntry(entry);
        curFrameMetadata.Compression = compression;
        curFrameMetadata.BitsPerPixel = bitsPerPixel;
    }
}
