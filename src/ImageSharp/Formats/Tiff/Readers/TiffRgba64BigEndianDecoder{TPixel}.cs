// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes packed big-endian RGBA samples with 16 bits per channel.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal sealed class TiffRgba64BigEndianDecoder<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool associated;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgba64BigEndianDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="associated">Whether stored RGB components have already been multiplied by alpha.</param>
    public TiffRgba64BigEndianDecoder(Configuration configuration, bool associated)
    {
        this.configuration = configuration;
        this.associated = associated;
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        // Each packed pixel has four ushort words. Four 512-bit registers hold 128 words, or 32 pixels;
        // both buffers fit that widest source group and its shorter final block.
        InlineArray32<Rgba64> sampleBuffer = default;
        InlineArray32<Vector4> vectorBuffer = default;
        Span<Rgba64> samples = sampleBuffer;
        Span<Vector4> vectors = vectorBuffer;
        TiffPacked16NativeSource source = new(BitConverter.IsLittleEndian);
        TiffRgba64AssociatedOperator colorOperator = default;
        TiffPackedRgba64Store store = default;
        int rowBytes = width * 8;

        for (int y = top; y < top + height; y++)
        {
            int rowOffset = (y - top) * rowBytes;
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += samples.Length)
            {
                // Slice the last block to its actual pixels so endian conversion and packing cannot read the next row.
                int count = Math.Min(width - x, samples.Length);
                Span<Rgba64> sampleBlock = samples[..count];

                if (this.associated)
                {
                    // TIFF associated pixels with zero alpha carry zero decoded color.
                    TiffOperatedColorConverter<TiffPacked16NativeSource, TiffRgba64AssociatedOperator, TiffPackedRgba64Store, ushort, ushort>.Convert(
                        data.Slice(rowOffset + (x * 8), count * 8),
                        [],
                        [],
                        [],
                        MemoryMarshal.Cast<Rgba64, ushort>(sampleBlock),
                        source,
                        colorOperator,
                        store);

                    Span<Vector4> vectorBlock = vectors[..count];
                    PixelOperations<Rgba64>.Instance.ToUnassociatedScaledVector4(this.configuration, sampleBlock, vectorBlock);
                    PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(this.configuration, vectorBlock, pixelRow.Slice(x, count));
                }
                else
                {
                    // FromRgba64 receives exact 16-bit channel codes after byte-order correction.
                    TiffUtilities.CopyUShorts(
                        data.Slice(rowOffset + (x * 8), count * 8),
                        MemoryMarshal.Cast<Rgba64, ushort>(sampleBlock),
                        bigEndian: true,
                        invert: false);

                    PixelOperations<TPixel>.Instance.FromRgba64(this.configuration, sampleBlock, pixelRow.Slice(x, count));
                }
            }
        }
    }
}
