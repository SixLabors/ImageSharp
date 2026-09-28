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
/// Decodes 32-bit floating-point TIFF grayscale samples for both color interpretations.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal abstract class TiffFloatGrayscaleColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool isBigEndian;
    private readonly bool whiteIsZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffFloatGrayscaleColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration used by bulk pixel conversion.</param>
    /// <param name="isBigEndian">Whether the TIFF samples use big-endian byte order.</param>
    /// <param name="whiteIsZero">Whether stored intensities must be inverted.</param>
    protected TiffFloatGrayscaleColor(Configuration configuration, bool isBigEndian, bool whiteIsZero)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
        this.whiteIsZero = whiteIsZero;
    }

    /// <inheritdoc />
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        const int BlockSize = 64;
        Span<Vector4> vectors = stackalloc Vector4[BlockSize];
        bool reverseEndianness = this.isBigEndian == BitConverter.IsLittleEndian;
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < width; x += BlockSize)
            {
                int count = Math.Min(BlockSize, width - x);
                int byteCount = count * sizeof(float);
                Span<Vector4> block = vectors[..count];

                // Expand the stored intensity before the pixel format applies its
                // own numeric and alpha rules to the whole block.
                ExpandFloatSingles(data.Slice(offset, byteCount), block, reverseEndianness, this.whiteIsZero);
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
    /// Expands single-component floating-point pixels into four-component vectors.
    /// </summary>
    /// <param name="source">The packed 32-bit intensity samples.</param>
    /// <param name="destination">The destination vectors.</param>
    /// <param name="reverseEndianness">Whether each stored sample needs its bytes reversed.</param>
    /// <param name="whiteIsZero">Whether the stored intensity must be inverted.</param>
    private static void ExpandFloatSingles(ReadOnlySpan<byte> source, Span<Vector4> destination, bool reverseEndianness, bool whiteIsZero)
    {
        ReadOnlySpan<uint> samples = MemoryMarshal.Cast<byte, uint>(source);
        ref uint s = ref MemoryMarshal.GetReference(samples);
        ref float d = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<byte> byteReverse = Vector128.Create(
                (byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12);

            Vector128<uint> componentMask = Vector128.Create(uint.MaxValue, uint.MaxValue, uint.MaxValue, 0U);
            Vector128<uint> opaqueFourth = Vector128.Create(0U, 0U, 0U, BitConverter.SingleToUInt32Bits(1F));

            for (; i <= destination.Length - 4; i += 4)
            {
                Vector128<byte> packed = Vector128.LoadUnsafe(ref s, (nuint)i).AsByte();
                if (reverseEndianness)
                {
                    packed = Vector128.ShuffleNative(packed, byteReverse);
                }

                Vector128<float> intensities = packed.AsSingle();
                if (whiteIsZero)
                {
                    // WhiteIsZero stores one minus intensity. Invert before broadcasting
                    // so all three color components receive the same decoded value.
                    intensities = Vector128.Create(1F) - intensities;
                }

                // Each shuffle broadcasts one sample to X, Y, Z, and W. Replace W
                // with opaque alpha without arithmetic on the stored intensity bits.
                Vector128<uint> pixel0 = Vector128_.ShuffleNative(intensities, 0x00).AsUInt32();
                Vector128<uint> pixel1 = Vector128_.ShuffleNative(intensities, 0x55).AsUInt32();
                Vector128<uint> pixel2 = Vector128_.ShuffleNative(intensities, 0xAA).AsUInt32();
                Vector128<uint> pixel3 = Vector128_.ShuffleNative(intensities, 0xFF).AsUInt32();

                Vector128.StoreUnsafe(((pixel0 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)(i * 4));
                Vector128.StoreUnsafe(((pixel1 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)((i + 1) * 4));
                Vector128.StoreUnsafe(((pixel2 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)((i + 2) * 4));
                Vector128.StoreUnsafe(((pixel3 & componentMask) | opaqueFourth).AsSingle(), ref d, (nuint)((i + 3) * 4));
            }
        }

        for (; i < destination.Length; i++)
        {
            uint bits = reverseEndianness ? BinaryPrimitives.ReverseEndianness(samples[i]) : samples[i];
            float intensity = BitConverter.Int32BitsToSingle((int)bits);
            intensity = whiteIsZero ? 1F - intensity : intensity;
            destination[i] = new Vector4(intensity, intensity, intensity, 1F);
        }
    }
}
