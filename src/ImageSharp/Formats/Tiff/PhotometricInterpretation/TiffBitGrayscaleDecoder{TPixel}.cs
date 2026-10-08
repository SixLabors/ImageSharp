// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Decodes byte-padded rows of bit-packed TIFF grayscale samples.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal sealed class TiffBitGrayscaleDecoder<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly ushort bitsPerSample;
    private readonly bool whiteIsZero;
    private readonly uint maximumCode;
    private readonly TiffGrayBitOperator colorOperator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffBitGrayscaleDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="bitsPerSample">The stored grayscale bit depth.</param>
    /// <param name="whiteIsZero">Whether zero represents white.</param>
    public TiffBitGrayscaleDecoder(Configuration configuration, ushort bitsPerSample, bool whiteIsZero)
    {
        this.configuration = configuration;
        this.bitsPerSample = bitsPerSample;
        this.whiteIsZero = whiteIsZero;
        this.maximumCode = (uint)((1UL << bitsPerSample) - 1UL);
        this.colorOperator = new TiffGrayBitOperator(bitsPerSample, this.maximumCode);
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        InlineArray16<Vector4> vectorBuffer = default;
        Span<Vector4> vectors = vectorBuffer;
        InlineArray16<float> codeBuffer = default;
        Span<float> codes = codeBuffer;
        int rowByteCount = ((width * this.bitsPerSample) + 7) / 8;
        int rowOffset = 0;

        for (int y = top; y < top + height; y++)
        {
            // TIFF pads each row to a full byte. Start the next row after those padding bits.
            ReadOnlySpan<byte> row = data.Slice(rowOffset, rowByteCount);
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            int x = 0;

            if (Vector128.IsHardwareAccelerated && (this.bitsPerSample == 1 || this.bitsPerSample == 4 || this.bitsPerSample == 8))
            {
                // Each expansion produces sixteen pixels, regardless of the stored bit depth.
                // Count groups from the destination so padding bits cannot create an extra group.
                ReadOnlySpan<byte> pixelBytes = MemoryMarshal.AsBytes(pixelRow);
                nuint groups = pixelBytes.Vector128Count<byte>() / (uint)Unsafe.SizeOf<TPixel>();

                for (nuint group = 0; group < groups; group++, x += Vector128<byte>.Count)
                {
                    Vector128<byte> rawCodes;

                    if (this.bitsPerSample == 1)
                    {
                        // MSB-first TIFF bits become sixteen byte lanes, one bit per lane.
                        Vector128<byte> bytes = Vector128.Create(Vector64.Create(row[x / 8]), Vector64.Create(row[(x / 8) + 1]));
                        Vector128<byte> masks = Vector128.Create((byte)128, 64, 32, 16, 8, 4, 2, 1, 128, 64, 32, 16, 8, 4, 2, 1);
                        rawCodes = Vector128.Equals(bytes & masks, masks) & Vector128.Create((byte)1);
                    }
                    else if (this.bitsPerSample == 4)
                    {
                        // Duplicate each packed byte as [high nibble, low nibble] in pixel order.
                        Vector128<byte> bytes = Vector128.Create(MemoryMarshal.Read<ulong>(row.Slice(x / 2)), 0UL).AsByte();
                        Vector128<byte> high = Vector128.ShiftRightLogical(bytes.AsUInt16(), 4).AsByte() & Vector128.Create((byte)15);
                        Vector128<byte> low = bytes & Vector128.Create((byte)15);
                        rawCodes = Vector128_.UnpackLow(high, low);
                    }
                    else
                    {
                        rawCodes = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(row), (nuint)x);
                    }

                    if (this.whiteIsZero)
                    {
                        // TIFF inversion occurs in the integer code domain, before conversion to float.
                        rawCodes = Vector128.Create((byte)this.maximumCode) - rawCodes;
                    }

                    Vector128<ushort> lower = Vector128.WidenLower(rawCodes);
                    Vector128<ushort> upper = Vector128.WidenUpper(rawCodes);
                    ConvertFour(Vector128.WidenLower(lower), this.colorOperator, ref vectors[0]);
                    ConvertFour(Vector128.WidenUpper(lower), this.colorOperator, ref vectors[4]);
                    ConvertFour(Vector128.WidenLower(upper), this.colorOperator, ref vectors[8]);
                    ConvertFour(Vector128.WidenUpper(upper), this.colorOperator, ref vectors[12]);
                    PixelOperations<TPixel>.Instance.FromScaledVector4Destructive(this.configuration, vectors, pixelRow.Slice(x, 16));
                }
            }

            // Other bit depths need sequential extraction. For accelerated one-, four-, and eight-bit rows,
            // this path handles only the zero to fifteen pixels left after complete sixteen-pixel groups.
            BitReader bitReader = new(row.Slice((x * this.bitsPerSample) / 8));

            for (; x < width; x += vectors.Length)
            {
                int count = Math.Min(vectors.Length, width - x);
                Span<Vector4> block = vectors[..count];

                for (int sampleIndex = 0; sampleIndex < count; sampleIndex++)
                {
                    uint value = (uint)bitReader.ReadBits(this.bitsPerSample);
                    codes[sampleIndex] = this.whiteIsZero ? this.maximumCode - value : value;
                }

                int i = 0;

                if (Vector128.IsHardwareAccelerated)
                {
                    // Each float lane holds one extracted code. Only complete four-code groups enter SIMD.
                    ReadOnlySpan<float> blockCodes = codes[..count];
                    nuint groups = blockCodes.Vector128Count<float>();
                    ref float firstCode = ref MemoryMarshal.GetReference(codes);

                    for (nuint group = 0; group < groups; group++, i += Vector128<float>.Count)
                    {
                        ConvertFour(Vector128.LoadUnsafe(ref firstCode, (nuint)i), this.colorOperator, ref block[i]);
                    }
                }

                for (; i < count; i++)
                {
                    // The scalar path handles only a short remainder after register groups.
                    float intensity = codes[i];
                    float green = 0F;
                    float blue = 0F;
                    float alpha = 0F;
                    this.colorOperator.Convert(ref intensity, ref green, ref blue, ref alpha);
                    block[i] = new Vector4(intensity, green, blue, alpha);
                }

                PixelOperations<TPixel>.Instance.FromScaledVector4Destructive(this.configuration, block, pixelRow.Slice(x, count));
            }

            rowOffset += rowByteCount;
        }
    }

    /// <summary>
    /// Normalizes four integer codes and writes four contiguous RGBA vectors.
    /// </summary>
    /// <param name="codes">The four grayscale code lanes represented as float bits.</param>
    /// <param name="colorOperator">The grayscale normalization.</param>
    /// <param name="destination">The first destination vector.</param>
    private static void ConvertFour(Vector128<float> codes, TiffGrayBitOperator colorOperator, ref Vector4 destination)
    {
        Vector128<float> c0 = codes;
        Vector128<float> c1 = Vector128<float>.Zero;
        Vector128<float> c2 = Vector128<float>.Zero;
        Vector128<float> c3 = Vector128<float>.Zero;
        colorOperator.Convert(ref c0, ref c1, ref c2, ref c3);
        SimdUtils.Transpose4(c0, c1, c2, c3, out Vector128<float> pixel0, out Vector128<float> pixel1, out Vector128<float> pixel2, out Vector128<float> pixel3);
        ref Vector128<float> first = ref Unsafe.As<Vector4, Vector128<float>>(ref destination);
        first = pixel0;
        Unsafe.Add(ref first, 1) = pixel1;
        Unsafe.Add(ref first, 2) = pixel2;
        Unsafe.Add(ref first, 3) = pixel3;
    }

    /// <summary>
    /// Converts four unsigned grayscale codes to float lanes before normalization.
    /// </summary>
    /// <param name="codes">The four unsigned codes.</param>
    /// <param name="colorOperator">The grayscale normalization.</param>
    /// <param name="destination">The first destination vector.</param>
    private static void ConvertFour(Vector128<uint> codes, TiffGrayBitOperator colorOperator, ref Vector4 destination)
    {
        ConvertFour(Vector128.ConvertToSingle(codes), colorOperator, ref destination);
    }
}
