// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes packed little-endian RGBA64 samples through native bulk pixel operations.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal sealed class TiffRgba64LittleEndianDecoder<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba64LittleEndianDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="associated">Whether stored RGB components have already been multiplied by alpha.</param>
    public TiffRgba64LittleEndianDecoder(Configuration configuration, bool associated)
    {
        this.configuration = configuration;
        this.associated = associated;
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int rowBytes = width * 8;
        Span<Vector4> vectors = this.GetColorBuffer(this.configuration, width, []);

        if (!this.associated)
        {
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                ReadOnlySpan<byte> row = data.Slice((y - top) * rowBytes, rowBytes);
                ReadOnlySpan<Rgba64> samples = MemoryMarshal.Cast<byte, Rgba64>(row);

                if (!this.TryStoreColorPixels(this.configuration, samples, vectors, 0))
                {
                    PixelOperations<TPixel>.Instance.FromRgba64Bytes(this.configuration, row, pixelRow, width);
                }

                this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.Unassociated);
            }

            return;
        }

        // Native-order Rgba64 rows feed the associated pixel operation in one pass.
        // Zero-alpha RGB codes remain available to destination alpha conversion.
        vectors = this.GetVectorBuffer(this.configuration.MemoryAllocator, width);

        for (int y = top; y < top + height; y++)
        {
            int offset = (y - top) * rowBytes;
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            ReadOnlySpan<Rgba64> samples = MemoryMarshal.Cast<byte, Rgba64>(data.Slice(offset, rowBytes));
            PixelOperations<Rgba64>.Instance.ToUnassociatedScaledVector4(this.configuration, samples, vectors);
            this.StoreColorVectors(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.Associated);
            this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.Associated);
        }
    }
}
