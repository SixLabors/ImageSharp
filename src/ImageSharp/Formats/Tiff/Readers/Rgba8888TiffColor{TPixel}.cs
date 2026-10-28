// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

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

        Span<Vector4> vectorsSpan = hasAssociatedAlpha || this.HasColorConverter ? this.GetVectorBuffer(this.memoryAllocator, width) : [];
        PixelAlphaRepresentation alphaRepresentation = hasAssociatedAlpha ? PixelAlphaRepresentation.Associated : PixelAlphaRepresentation.Unassociated;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            int byteCount = pixelRow.Length * 4;

            if (hasAssociatedAlpha)
            {
                // The TIFF samples already contain associated RGB. Reinterpret them as Rgba32 only to normalize and unpack the
                // channels; asking that unassociated storage type to associate them would multiply RGB a second time.
                ReadOnlySpan<Rgba32> associatedSamples = MemoryMarshal.Cast<byte, Rgba32>(data.Slice(offset, byteCount));
                PixelOperations<Rgba32>.Instance.ToUnassociatedScaledVector4(this.configuration, associatedSamples, vectorsSpan);
                this.StoreColorVectors(this.configuration, vectorsSpan, pixelRow, alphaRepresentation);
            }
            else
            {
                ReadOnlySpan<Rgba32> samples = MemoryMarshal.Cast<byte, Rgba32>(data.Slice(offset, byteCount));

                if (!this.TryStoreColorPixels(this.configuration, samples, vectorsSpan, 0))
                {
                    PixelOperations<TPixel>.Instance.FromRgba32Bytes(this.configuration, data.Slice(offset, byteCount), pixelRow, pixelRow.Length);
                }
            }

            this.CompleteColorRow(this.configuration, vectorsSpan, pixelRow, alphaRepresentation);

            offset += byteCount;
        }
    }
}
