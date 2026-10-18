// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Packs one unsigned 16-bit grayscale sample per pixel, with optional WhiteIsZero inversion.
/// </summary>
/// <typeparam name="TPixel">The source pixel format.</typeparam>
internal sealed class TiffGrayL16Writer<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool whiteIsZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffGrayL16Writer{TPixel}"/> class.
    /// </summary>
    /// <param name="image">The source frame.</param>
    /// <param name="encodingSize">The encoded region size.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="configuration">The pixel conversion configuration.</param>
    /// <param name="entriesCollector">The TIFF directory entries.</param>
    /// <param name="converter">The converter from sRGB to the retained profile, or null.</param>
    /// <param name="clearTransparentPixels">Whether to clear RGB where alpha is zero in the output row.</param>
    /// <param name="whiteIsZero">Whether zero represents white.</param>
    public TiffGrayL16Writer(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector,
        ColorProfileConverter? converter,
        bool clearTransparentPixels,
        bool whiteIsZero)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector, converter, clearTransparentPixels)
        => this.whiteIsZero = whiteIsZero;

    /// <inheritdoc />
    public override int BitsPerPixel => 16;

    /// <inheritdoc />
    protected override void EncodePixels<TPixelFrom>(ReadOnlySpan<TPixelFrom> source, Span<byte> destination)
    {
        PixelOperations<TPixelFrom>.Instance.ToL16Bytes(this.Configuration, source, destination, source.Length);

        // WhiteIsZero stores 65535 minus the grayscale code. Complementing both bytes performs that subtraction in either byte order.
        if (this.whiteIsZero)
        {
            TensorPrimitives.OnesComplement<byte>(destination, destination);
        }
    }
}
