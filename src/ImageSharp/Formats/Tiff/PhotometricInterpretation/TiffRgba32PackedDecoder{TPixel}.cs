// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Decodes packed TIFF RGBA pixels with 32-bit integer channels.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal sealed class TiffRgba32PackedDecoder<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool reverseEndianness;
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba32PackedDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="isBigEndian">Whether stored integer codes use big-endian byte order.</param>
    /// <param name="associated">Whether stored RGB codes have already been multiplied by alpha.</param>
    public TiffRgba32PackedDecoder(Configuration configuration, bool isBigEndian, bool associated)
    {
        this.configuration = configuration;
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
        this.associated = associated;
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        InlineArray16<Vector4> vectorBuffer = default;
        Span<Vector4> vectors = vectorBuffer;
        TiffRgba32PackedSource source = new(this.reverseEndianness);
        TiffRgba32PackedOperator colorOperator = new(this.associated);
        TiffPackedVector4Store store = default;
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                Span<Vector4> block = vectors[..count];

                // Convert up to sixteen pixels together so PixelOperations can pack them in bulk.
                // Slice the source to complete pixels so its final 32-bit read stays in this row.
                TiffOperatedColorConverter<TiffRgba32PackedSource, TiffRgba32PackedOperator, TiffPackedVector4Store, float, Vector4>.Convert(
                    data.Slice(offset, count * 16),
                    [],
                    [],
                    [],
                    block,
                    source,
                    colorOperator,
                    store);

                // The operator preserves stored alpha association, so select the matching destination packer.
                if (this.associated)
                {
                    PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                }

                offset += count * 16;
            }
        }
    }
}
