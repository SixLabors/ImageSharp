// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with 'Planar' layout (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbPlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly float rFactor;

    private readonly float gFactor;

    private readonly float bFactor;

    private readonly ushort bitsPerSampleR;

    private readonly ushort bitsPerSampleG;

    private readonly ushort bitsPerSampleB;

    public RgbPlanarTiffColor(TiffBitsPerSample bitsPerSample)
    {
        this.bitsPerSampleR = bitsPerSample.Channel0;
        this.bitsPerSampleG = bitsPerSample.Channel1;
        this.bitsPerSampleB = bitsPerSample.Channel2;

        this.rFactor = (1 << this.bitsPerSampleR) - 1.0f;
        this.gFactor = (1 << this.bitsPerSampleG) - 1.0f;
        this.bFactor = (1 << this.bitsPerSampleB) - 1.0f;
    }

    /// <summary>
    /// Decodes pixel data using the current photometric interpretation.
    /// </summary>
    /// <param name="data">The buffers to read image data from.</param>
    /// <param name="pixels">The image buffer to write pixels to.</param>
    /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
    /// <param name="top">The y-coordinate of the  top of the image block.</param>
    /// <param name="width">The width of the image block.</param>
    /// <param name="height">The height of the image block.</param>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        BitReader rBitReader = new(data[0].GetSpan());
        BitReader gBitReader = new(data[1].GetSpan());
        BitReader bBitReader = new(data[2].GetSpan());

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x++)
            {
                float r = rBitReader.ReadBits(this.bitsPerSampleR) / this.rFactor;
                float g = gBitReader.ReadBits(this.bitsPerSampleG) / this.gFactor;
                float b = bBitReader.ReadBits(this.bitsPerSampleB) / this.bFactor;

                pixelRow[x] = TPixel.FromScaledVector4(new Vector4(r, g, b, 1f));
            }

            rBitReader.NextRow();
            gBitReader.NextRow();
            bBitReader.NextRow();
        }
    }
}
