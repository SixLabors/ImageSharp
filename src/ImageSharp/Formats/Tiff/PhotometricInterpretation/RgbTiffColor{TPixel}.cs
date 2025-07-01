// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly float rFactor;

    private readonly float gFactor;

    private readonly float bFactor;

    private readonly ushort bitsPerSampleR;

    private readonly ushort bitsPerSampleG;

    private readonly ushort bitsPerSampleB;

    public RgbTiffColor(TiffBitsPerSample bitsPerSample)
    {
        this.bitsPerSampleR = bitsPerSample.Channel0;
        this.bitsPerSampleG = bitsPerSample.Channel1;
        this.bitsPerSampleB = bitsPerSample.Channel2;

        this.rFactor = (1 << this.bitsPerSampleR) - 1.0f;
        this.gFactor = (1 << this.bitsPerSampleG) - 1.0f;
        this.bFactor = (1 << this.bitsPerSampleB) - 1.0f;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        BitReader bitReader = new(data);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x++)
            {
                float r = bitReader.ReadBits(this.bitsPerSampleR) / this.rFactor;
                float g = bitReader.ReadBits(this.bitsPerSampleG) / this.gFactor;
                float b = bitReader.ReadBits(this.bitsPerSampleB) / this.bFactor;

                pixelRow[x] = TPixel.FromScaledVector4(new Vector4(r, g, b, 1f));
            }

            bitReader.NextRow();
        }
    }
}
