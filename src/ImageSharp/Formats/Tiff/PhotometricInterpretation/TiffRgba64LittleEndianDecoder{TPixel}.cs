// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

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

        if (!this.associated)
        {
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                PixelOperations<TPixel>.Instance.FromRgba64Bytes(this.configuration, data.Slice((y - top) * rowBytes, rowBytes), pixelRow, width);
            }

            return;
        }

        // Native-order Rgba64 rows feed the associated pixel operation in one pass.
        // Zero-alpha RGB codes remain available to destination alpha conversion.
        using IMemoryOwner<Vector4> owner = this.configuration.MemoryAllocator.Allocate<Vector4>(width);
        Span<Vector4> vectors = owner.GetSpan();

        for (int y = top; y < top + height; y++)
        {
            int offset = (y - top) * rowBytes;
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            ReadOnlySpan<Rgba64> samples = MemoryMarshal.Cast<byte, Rgba64>(data.Slice(offset, rowBytes));
            PixelOperations<Rgba64>.Instance.ToVector4(this.configuration, samples, vectors, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectors, pixelRow, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
        }
    }
}
