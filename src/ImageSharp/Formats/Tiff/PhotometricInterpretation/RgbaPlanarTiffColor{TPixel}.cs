// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and with 'Planar' layout (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbaPlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;

    private readonly TiffBitPackedColorOperator colorOperator;

    private readonly ushort bitsPerSampleR;

    private readonly ushort bitsPerSampleG;

    private readonly ushort bitsPerSampleB;

    private readonly ushort bitsPerSampleA;

    private readonly TiffExtraSampleType? extraSampleType;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaPlanarTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="extraSampleType">The alpha sample interpretation.</param>
    /// <param name="bitsPerSample">The per-channel sample depths.</param>
    public RgbaPlanarTiffColor(Configuration configuration, TiffExtraSampleType? extraSampleType, TiffBitsPerSample bitsPerSample)
    {
        this.configuration = configuration;
        this.bitsPerSampleR = bitsPerSample.Channel0;
        this.bitsPerSampleG = bitsPerSample.Channel1;
        this.bitsPerSampleB = bitsPerSample.Channel2;
        this.bitsPerSampleA = bitsPerSample.Channel3;

        this.colorOperator = new TiffBitPackedColorOperator(this.bitsPerSampleR, this.bitsPerSampleG, this.bitsPerSampleB, this.bitsPerSampleA);

        this.extraSampleType = extraSampleType;
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
        bool hasAssociatedAlpha = this.extraSampleType.HasValue && this.extraSampleType == TiffExtraSampleType.AssociatedAlphaData;

        if (this.bitsPerSampleR == 8 && this.bitsPerSampleG == 8 && this.bitsPerSampleB == 8 && this.bitsPerSampleA == 8)
        {
            ReadOnlySpan<byte> red = data[0].GetSpan();
            ReadOnlySpan<byte> green = data[1].GetSpan();
            ReadOnlySpan<byte> blue = data[2].GetSpan();
            ReadOnlySpan<byte> alpha = data[3].GetSpan();

            // Each plane supplies one byte per pixel. Sixty-four bytes fill a 512-bit register in each channel,
            // so both buffers hold one widest group and the shorter final group without touching the next row.
            InlineArray64<Rgba32> packedBuffer = default;
            InlineArray64<Vector4> vectorBuffer = default;
            Span<Rgba32> packedBlock = packedBuffer;
            Span<Vector4> vectorBlock = vectorBuffer;
            PixelConversionModifiers destinationModifiers = PixelConversionModifiers.Scale | (hasAssociatedAlpha ? PixelConversionModifiers.Premultiply : PixelConversionModifiers.UnPremultiply);
            TiffPlanar8NativeSource source = default;
            TiffRgba32Store store = default;
            int offset = 0;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                for (int x = 0; x < width; x += vectorBlock.Length)
                {
                    // Slice every plane and the output to the final group's actual pixel count.
                    int count = Math.Min(width - x, vectorBlock.Length);

                    // SIMD interleaves R, G, B, and A into native RGBA bytes. PixelOperations then interprets
                    // the stored alpha association and packs the requested destination pixel type in bulk.
                    TiffNativeColorConverter<TiffPlanar8NativeSource, TiffRgba32Store, byte>.Convert(red.Slice(offset + x, count), green.Slice(offset + x, count), blue.Slice(offset + x, count), alpha.Slice(offset + x, count), MemoryMarshal.AsBytes(packedBlock[..count]), source, store);

                    Span<Vector4> vectors = vectorBlock[..count];
                    PixelOperations<Rgba32>.Instance.ToVector4(this.configuration, packedBlock[..count], vectors, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectors, pixelRow.Slice(x, count), destinationModifiers);
                }

                offset += width;
            }

            return;
        }

        BitReader rBitReader = new(data[0].GetSpan());
        BitReader gBitReader = new(data[1].GetSpan());
        BitReader bBitReader = new(data[2].GetSpan());
        BitReader aBitReader = new(data[3].GetSpan());
        InlineArray16<Vector4> rawBuffer = default;
        Span<Vector4> raw = rawBuffer;
        PixelConversionModifiers modifiers = PixelConversionModifiers.Scale | (hasAssociatedAlpha ? PixelConversionModifiers.Premultiply : PixelConversionModifiers.UnPremultiply);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x += raw.Length)
            {
                int count = Math.Min(raw.Length, pixelRow.Length - x);
                Span<Vector4> block = raw[..count];

                for (int i = 0; i < count; i++)
                {
                    float r = rBitReader.ReadBits(this.bitsPerSampleR);
                    float g = gBitReader.ReadBits(this.bitsPerSampleG);
                    float b = bBitReader.ReadBits(this.bitsPerSampleB);
                    float a = aBitReader.ReadBits(this.bitsPerSampleA);
                    block[i] = new Vector4(r, g, b, a);
                }

                this.colorOperator.Convert(block);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, block, pixelRow.Slice(x, count), modifiers);
            }

            rBitReader.NextRow();
            gBitReader.NextRow();
            bBitReader.NextRow();
            aBitReader.NextRow();
        }
    }
}
