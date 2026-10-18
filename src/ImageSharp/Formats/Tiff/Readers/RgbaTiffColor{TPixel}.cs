// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Implements the 'RGB' photometric interpretation with alpha channel (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbaTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly TiffBitPackedColorOperator colorOperator;

    private readonly ushort bitsPerSampleR;

    private readonly ushort bitsPerSampleG;

    private readonly ushort bitsPerSampleB;

    private readonly ushort bitsPerSampleA;

    private readonly TiffExtraSampleType? extraSamplesType;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="extraSampleType">The alpha sample interpretation.</param>
    /// <param name="bitsPerSample">The per-channel sample depths.</param>
    public RgbaTiffColor(Configuration configuration, TiffExtraSampleType? extraSampleType, TiffBitsPerSample bitsPerSample)
    {
        this.configuration = configuration;
        this.bitsPerSampleR = bitsPerSample.Channel0;
        this.bitsPerSampleG = bitsPerSample.Channel1;
        this.bitsPerSampleB = bitsPerSample.Channel2;
        this.bitsPerSampleA = bitsPerSample.Channel3;

        this.colorOperator = new TiffBitPackedColorOperator(this.bitsPerSampleR, this.bitsPerSampleG, this.bitsPerSampleB, this.bitsPerSampleA);

        this.extraSamplesType = extraSampleType;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        BitReader bitReader = new(data);
        InlineArray16<Vector4> vectorBuffer = default;
        Span<Vector4> vectors = vectorBuffer;

        bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, pixelRow.Length - x);
                Span<Vector4> block = vectors[..count];

                // The bit reader owns packed sample boundaries; alpha interpretation
                // remains with the destination pixel operation for the whole block.
                for (int i = 0; i < count; i++)
                {
                    float r = bitReader.ReadBits(this.bitsPerSampleR);
                    float g = bitReader.ReadBits(this.bitsPerSampleG);
                    float b = bitReader.ReadBits(this.bitsPerSampleB);
                    float a = bitReader.ReadBits(this.bitsPerSampleA);
                    block[i] = new Vector4(r, g, b, a);
                }

                this.colorOperator.Convert(block);
                if (hasAssociatedAlpha)
                {
                    PixelOperations<TPixel>.Instance.FromAssociatedScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromUnassociatedScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
                }
            }

            bitReader.NextRow();
        }
    }
}
