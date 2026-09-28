// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and with 32 bits for each channel.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbaFloat32323232TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool isBigEndian;

    private readonly TiffExtraSampleType? extraSamplesType;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaFloat32323232TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration used by bulk pixel conversion.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    /// <param name="extraSamplesType">The alpha representation declared by the TIFF extra samples field.</param>
    public RgbaFloat32323232TiffColor(Configuration configuration, bool isBigEndian, TiffExtraSampleType? extraSamplesType)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
        this.extraSamplesType = extraSamplesType;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        const int BlockSize = 64;
        Span<Vector4> vectors = stackalloc Vector4[BlockSize];
        bool reverseEndianness = this.isBigEndian == BitConverter.IsLittleEndian;
        PixelConversionModifiers modifiers = PixelConversionModifiers.Scale |
            (this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData
                ? PixelConversionModifiers.Premultiply
                : PixelConversionModifiers.UnPremultiply);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < width; x += BlockSize)
            {
                int count = Math.Min(BlockSize, width - x);
                int byteCount = count * 4 * sizeof(float);
                Span<Vector4> block = vectors[..count];

                // The packed four-component row already matches Vector4 order.
                // The pixel operation receives the alpha representation declared
                // by ExtraSamples and performs any destination conversion in bulk.
                CopyFloatQuads(data.Slice(offset, byteCount), block, reverseEndianness);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(
                    this.configuration,
                    block,
                    pixelRow.Slice(x, count),
                    modifiers);

                offset += byteCount;
            }
        }
    }

    /// <summary>
    /// Copies packed four-component floating-point pixels into vectors, reversing each
    /// sample's byte order when the stored byte order differs from the host.
    /// </summary>
    /// <param name="source">The packed 32-bit component samples.</param>
    /// <param name="destination">The destination vectors.</param>
    /// <param name="reverseEndianness">Whether each stored 32-bit sample needs its bytes reversed.</param>
    private static void CopyFloatQuads(ReadOnlySpan<byte> source, Span<Vector4> destination, bool reverseEndianness)
    {
        if (!reverseEndianness)
        {
            // Matching byte order needs only the runtime's bulk copy. This also
            // preserves every sample bit, including nonfinite payloads.
            source.CopyTo(MemoryMarshal.AsBytes(destination));
            return;
        }

        ReadOnlySpan<uint> samples = MemoryMarshal.Cast<byte, uint>(source);
        Span<uint> converted = MemoryMarshal.Cast<Vector4, uint>(destination);
        ref uint s = ref MemoryMarshal.GetReference(samples);
        ref uint d = ref MemoryMarshal.GetReference(converted);
        Vector128<byte> byteReverse = Vector128.Create(
            (byte)3, 2, 1, 0, 7, 6, 5, 4, 11, 10, 9, 8, 15, 14, 13, 12);

        int i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            // A register holds one four-component pixel. The byte shuffle reverses
            // each component independently; component order and bit patterns remain intact.
            for (; i <= samples.Length - 4; i += 4)
            {
                Vector128<byte> packed = Vector128.LoadUnsafe(ref s, (nuint)i).AsByte();
                Vector128.StoreUnsafe(Vector128.ShuffleNative(packed, byteReverse).AsUInt32(), ref d, (nuint)i);
            }
        }

        for (; i < samples.Length; i++)
        {
            converted[i] = BinaryPrimitives.ReverseEndianness(samples[i]);
        }
    }
}
