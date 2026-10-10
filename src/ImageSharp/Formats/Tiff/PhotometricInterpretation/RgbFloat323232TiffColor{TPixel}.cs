// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with 32 bits for each channel.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbFloat323232TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbFloat323232TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration used by bulk pixel conversion.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public RgbFloat323232TiffColor(Configuration configuration, bool isBigEndian)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        const int BlockSize = 64;
        Span<Vector4> vectors = stackalloc Vector4[BlockSize];
        bool reverseEndianness = this.isBigEndian == BitConverter.IsLittleEndian;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < width; x += BlockSize)
            {
                int count = Math.Min(BlockSize, width - x);
                int byteCount = count * 3 * sizeof(float);
                Span<Vector4> block = vectors[..count];

                // TIFF stores three consecutive samples per pixel. The row kernel
                // inserts opaque fourth components before the destination pixel
                // operation applies its own numeric and alpha representation.
                ExpandFloatTriplets(data.Slice(offset, byteCount), block, reverseEndianness);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(
                    this.configuration,
                    block,
                    pixelRow.Slice(x, count),
                    PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

                offset += byteCount;
            }
        }
    }

    /// <summary>
    /// Expands packed three-component floating-point pixels into four-component vectors.
    /// The fourth component is one, and reversing byte order does not change sample bits.
    /// </summary>
    /// <param name="source">The packed 32-bit component samples.</param>
    /// <param name="destination">The destination vectors.</param>
    /// <param name="reverseEndianness">Whether each stored 32-bit sample needs its bytes reversed.</param>
    private static void ExpandFloatTriplets(ReadOnlySpan<byte> source, Span<Vector4> destination, bool reverseEndianness)
    {
        ReadOnlySpan<uint> samples = MemoryMarshal.Cast<byte, uint>(source);
        ref uint s = ref MemoryMarshal.GetReference(samples);
        ref float d = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            // Three registers contain four pixels in packed order. Reversing bytes inside
            // each 32-bit word happens before the registers are realigned, so no sample
            // changes position when the TIFF byte order differs from the host byte order.
            Vector128<byte> byteReverse = Vector128.Create(
                (byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12);

            Vector128<uint> componentMask = Vector128.Create(uint.MaxValue, uint.MaxValue, uint.MaxValue, 0U);
            Vector128<uint> opaqueFourth = Vector128.Create(0U, 0U, 0U, BitConverter.SingleToUInt32Bits(1F));

            for (; i <= destination.Length - 4; i += 4)
            {
                nuint sample = (nuint)(i * 3);
                Vector128<byte> packed0 = Vector128.LoadUnsafe(ref s, sample).AsByte();
                Vector128<byte> packed1 = Vector128.LoadUnsafe(ref s, sample + 4).AsByte();
                Vector128<byte> packed2 = Vector128.LoadUnsafe(ref s, sample + 8).AsByte();

                if (reverseEndianness)
                {
                    packed0 = Vector128.ShuffleNative(packed0, byteReverse);
                    packed1 = Vector128.ShuffleNative(packed1, byteReverse);
                    packed2 = Vector128.ShuffleNative(packed2, byteReverse);
                }

                // Packed registers contain [c0.0,c1.0,c2.0,c0.1],
                // [c1.1,c2.1,c0.2,c1.2], and [c2.2,c0.3,c1.3,c2.3].
                // Byte alignment forms the middle two vectors; the final shuffle
                // moves the last pixel's three components to the low positions.
                Vector128<uint> pixel0 = packed0.AsUInt32();
                Vector128<uint> pixel1 = Vector128_.AlignRight(packed1, packed0, 12).AsUInt32();
                Vector128<uint> pixel2 = Vector128_.AlignRight(packed2, packed1, 8).AsUInt32();
                Vector128<uint> pixel3 = Vector128_.ShuffleNative(packed2.AsSingle(), 0b00_11_10_01).AsUInt32();

                // Replace only the unused fourth word. Bitwise selection preserves
                // signed zero, infinities, and NaN payloads in the stored components.
                Vector128.StoreUnsafe(((pixel0 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)(i * 4));
                Vector128.StoreUnsafe(((pixel1 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)((i + 1) * 4));
                Vector128.StoreUnsafe(((pixel2 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)((i + 2) * 4));
                Vector128.StoreUnsafe(((pixel3 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)((i + 3) * 4));
            }
        }

        // The short tail reads the same three 32-bit words and never reads beyond
        // the final packed pixel when fewer than four pixels remain.
        for (; i < destination.Length; i++)
        {
            uint component0 = samples[i * 3];
            uint component1 = samples[(i * 3) + 1];
            uint component2 = samples[(i * 3) + 2];

            if (reverseEndianness)
            {
                component0 = BinaryPrimitives.ReverseEndianness(component0);
                component1 = BinaryPrimitives.ReverseEndianness(component1);
                component2 = BinaryPrimitives.ReverseEndianness(component2);
            }

            destination[i] = new Vector4(
                BitConverter.Int32BitsToSingle((int)component0),
                BitConverter.Int32BitsToSingle((int)component1),
                BitConverter.Int32BitsToSingle((int)component2),
                1F);
        }
    }
}
