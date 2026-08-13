// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Icon;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Decodes ICO containers and maps directory metadata to ICO metadata.
/// </summary>
internal sealed class IcoDecoderCore : IconDecoderCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IcoDecoderCore"/> class.
    /// </summary>
    /// <param name="options">The decoder options.</param>
    public IcoDecoderCore(DecoderOptions options)
        : base(options, IconFileType.ICO)
    {
    }

    /// <inheritdoc/>
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
            IcoMetadata icoMetadata = imageMetadata.GetIcoMetadata();
            icoMetadata.Compression = compression;
            icoMetadata.BmpBitsPerPixel = bitsPerPixel;
            icoMetadata.ColorTable = colorTable;
        }
    }
}
