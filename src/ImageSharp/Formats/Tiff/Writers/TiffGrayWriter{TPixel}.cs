// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

internal sealed class TiffGrayWriter<TPixel> : TiffCompositeColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    public TiffGrayWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
    }

    /// <inheritdoc />
    public override int BitsPerPixel => 8;

    /// <inheritdoc />
    protected override void EncodePixels(Span<TPixel> pixels, Span<byte> buffer)
        => PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixels, buffer, pixels.Length);
}
