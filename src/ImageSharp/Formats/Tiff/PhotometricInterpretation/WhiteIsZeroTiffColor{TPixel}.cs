// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'WhiteIsZero' photometric interpretation (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class WhiteIsZeroTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ushort bitsPerSample0;
    private readonly float factor;

    public WhiteIsZeroTiffColor(TiffBitsPerSample bitsPerSample)
    {
        this.bitsPerSample0 = bitsPerSample.Channel0;
        this.factor = (float)Math.Pow(2, this.bitsPerSample0) - 1.0f;
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
                int value = bitReader.ReadBits(this.bitsPerSample0);
                float intensity = 1f - (value / this.factor);
                pixelRow[x] = TPixel.FromScaledVector4(new(intensity, intensity, intensity, 1f));
            }

            bitReader.NextRow();
        }
    }
}
