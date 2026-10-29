// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Packs interleaved RGB samples with 8 bits per component through the bulk pixel conversion API.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffRgbWriter<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgbWriter{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The TIFF directory entries.</param>
    /// <param name="converter">The converter from sRGB to the retained profile, or null.</param>
    /// <param name="clearTransparentPixels">Whether to clear RGB where alpha is zero in the output row.</param>
    public TiffRgbWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        ColorProfileConverter? converter,
        bool clearTransparentPixels)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector, converter, clearTransparentPixels)
    {
    }

    /// <inheritdoc />
    public override int BitsPerPixel => 24;

    /// <inheritdoc />
    protected override void EncodePixels<TPixelFrom>(ReadOnlySpan<TPixelFrom> source, Span<byte> destination)
        => PixelOperations<TPixelFrom>.Instance.ToRgb24Bytes(this.Configuration, source, destination, source.Length);
}
