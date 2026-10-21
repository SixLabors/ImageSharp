// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Implements the 'RGB' photometric interpretation with 'Planar' layout (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbPlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;

    private readonly TiffBitPackedColorOperator colorOperator;

    private readonly ushort bitsPerSampleR;

    private readonly ushort bitsPerSampleG;

    private readonly ushort bitsPerSampleB;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbPlanarTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="bitsPerSample">The per-channel sample depths.</param>
    public RgbPlanarTiffColor(Configuration configuration, TiffBitsPerSample bitsPerSample)
    {
        this.configuration = configuration;
        this.bitsPerSampleR = bitsPerSample.Channel0;
        this.bitsPerSampleG = bitsPerSample.Channel1;
        this.bitsPerSampleB = bitsPerSample.Channel2;

        this.colorOperator = new TiffBitPackedColorOperator(this.bitsPerSampleR, this.bitsPerSampleG, this.bitsPerSampleB, 0);
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
        if (this.bitsPerSampleR == 8 && this.bitsPerSampleG == 8 && this.bitsPerSampleB == 8)
        {
            ReadOnlySpan<byte> red = data[0].GetSpan();
            ReadOnlySpan<byte> green = data[1].GetSpan();
            ReadOnlySpan<byte> blue = data[2].GetSpan();
            InlineArray64<Rgba32> packedBuffer = default;
            InlineArray64<Vector4> vectorBuffer = default;
            Span<Rgba32> packedBlock = packedBuffer;
            Span<Vector4> vectorBlock = this.GetColorBuffer(this.configuration, width, vectorBuffer);
            int offset = 0;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                for (int x = 0; x < width; x += packedBlock.Length)
                {
                    int count = Math.Min(width - x, packedBlock.Length);

                    // A full block supplies 64 byte samples per plane, enough for one Vector512 group.
                    // The packer writes exactly the supplied pixel count, including the final partial group.
                    SimdUtils.PackFromRgbPlanes(
                        red.Slice(offset + x, count),
                        green.Slice(offset + x, count),
                        blue.Slice(offset + x, count),
                        packedBlock[..count]);

                    Span<Vector4> vectors = this.GetColorBlock(vectorBlock, x, count);

                    // Opaque alpha makes scaling alone equivalent for straight and associated target pixels.
                    PixelOperations<Rgba32>.Instance.ToScaledVector4(this.configuration, packedBlock[..count], vectors);
                    this.StoreColorVectors(this.configuration, vectors, pixelRow.Slice(x, count), PixelAlphaRepresentation.None);
                }

                this.CompleteColorRow(this.configuration, vectorBlock, pixelRow, PixelAlphaRepresentation.None);

                offset += width;
            }

            return;
        }

        BitReader rBitReader = new(data[0].GetSpan());
        BitReader gBitReader = new(data[1].GetSpan());
        BitReader bBitReader = new(data[2].GetSpan());
        InlineArray16<Vector4> rawBuffer = default;
        Span<Vector4> raw = this.GetColorBuffer(this.configuration, width, rawBuffer);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < pixelRow.Length; x += raw.Length)
            {
                int count = Math.Min(raw.Length, pixelRow.Length - x);
                Span<Vector4> block = this.GetColorBlock(raw, x, count);

                // Each plane reader owns its row alignment; normalize the extracted codes
                // in registers before packing the entire destination block.
                for (int i = 0; i < count; i++)
                {
                    float r = rBitReader.ReadBits(this.bitsPerSampleR);
                    float g = gBitReader.ReadBits(this.bitsPerSampleG);
                    float b = bBitReader.ReadBits(this.bitsPerSampleB);
                    block[i] = new Vector4(r, g, b, 1F);
                }

                this.colorOperator.Convert(block);
                this.StoreColorVectors(this.configuration, block, pixelRow.Slice(x, count), PixelAlphaRepresentation.None);
            }

            this.CompleteColorRow(this.configuration, raw, pixelRow, PixelAlphaRepresentation.None);

            rBitReader.NextRow();
            gBitReader.NextRow();
            bBitReader.NextRow();
        }
    }
}
