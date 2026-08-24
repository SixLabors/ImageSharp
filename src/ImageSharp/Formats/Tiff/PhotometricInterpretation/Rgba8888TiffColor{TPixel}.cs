// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and 8 bits per channel.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgba8888TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;

    private readonly MemoryAllocator memoryAllocator;

    private readonly TiffExtraSampleType? extraSamplesType;

    public Rgba8888TiffColor(Configuration configuration, MemoryAllocator memoryAllocator, TiffExtraSampleType? extraSamplesType)
    {
        this.configuration = configuration;
        this.memoryAllocator = memoryAllocator;
        this.extraSamplesType = extraSamplesType;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;

        using IMemoryOwner<Vector4> vectors = hasAssociatedAlpha ? this.memoryAllocator.Allocate<Vector4>(width) : null;
        Span<Vector4> vectorsSpan = hasAssociatedAlpha ? vectors.GetSpan() : [];
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            int byteCount = pixelRow.Length * 4;

            if (hasAssociatedAlpha)
            {
                // The TIFF samples already contain associated RGB. Reinterpret them as Rgba32 only to normalize and unpack the
                // channels; asking that unassociated storage type to associate them would multiply RGB a second time.
                ReadOnlySpan<Rgba32> associatedSamples = MemoryMarshal.Cast<byte, Rgba32>(data.Slice(offset, byteCount));
                PixelOperations<Rgba32>.Instance.ToVector4(this.configuration, associatedSamples, vectorsSpan, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorsSpan, pixelRow, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
            }
            else
            {
                PixelOperations<TPixel>.Instance.FromRgba32Bytes(
                    this.configuration,
                    data.Slice(offset, byteCount),
                    pixelRow,
                    pixelRow.Length);
            }

            offset += byteCount;
        }
    }
}
