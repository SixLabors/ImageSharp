// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Implements the 'RGB' photometric interpretation (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly TiffBitPackedColorOperator colorOperator;

    private readonly ushort bitsPerSampleR;

    private readonly ushort bitsPerSampleG;

    private readonly ushort bitsPerSampleB;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="bitsPerSample">The per-channel sample depths.</param>
    public RgbTiffColor(Configuration configuration, TiffBitsPerSample bitsPerSample)
    {
        this.configuration = configuration;
        this.bitsPerSampleR = bitsPerSample.Channel0;
        this.bitsPerSampleG = bitsPerSample.Channel1;
        this.bitsPerSampleB = bitsPerSample.Channel2;

        this.colorOperator = new TiffBitPackedColorOperator(this.bitsPerSampleR, this.bitsPerSampleG, this.bitsPerSampleB, 0);
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        BitReader bitReader = new(data);
        InlineArray16<Vector4> vectorBuffer = default;
        Span<Vector4> vectors = this.GetColorBuffer(this.configuration, width, vectorBuffer);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < pixelRow.Length; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, pixelRow.Length - x);
                Span<Vector4> block = this.GetColorBlock(vectors, x, count);

                // The bit reader owns byte and row alignment; only sample extraction is
                // scalar while destination color packing runs on the complete block.
                for (int i = 0; i < count; i++)
                {
                    float r = bitReader.ReadBits(this.bitsPerSampleR);
                    float g = bitReader.ReadBits(this.bitsPerSampleG);
                    float b = bitReader.ReadBits(this.bitsPerSampleB);
                    block[i] = new Vector4(r, g, b, 1F);
                }

                this.colorOperator.Convert(block);
                this.StoreColorVectors(this.configuration, block, pixelRow.Slice(x, count), PixelAlphaRepresentation.None);
            }

            this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.None);

            bitReader.NextRow();
        }
    }
}
