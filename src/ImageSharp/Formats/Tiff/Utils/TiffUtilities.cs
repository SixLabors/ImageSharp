// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Utils;

/// <summary>
/// Helper methods for TIFF decoding.
/// </summary>
internal static class TiffUtilities
{
    private const float Scale24Bit = 1f / 0xFFFFFF;
    private static readonly Vector4 Scale24BitVector = Vector128.Create(Scale24Bit, Scale24Bit, Scale24Bit, 1f).AsVector4();

    private const float Scale32Bit = 1f / 0xFFFFFFFF;
    private static readonly Vector4 Scale32BitVector = Vector128.Create(Scale32Bit, Scale32Bit, Scale32Bit, 1f).AsVector4();
    private static readonly Vector128<byte> Reverse16BitByteIndices = Vector128.Create((byte)1, 0, 3, 2, 5, 4, 7, 6, 9, 8, 11, 10, 13, 12, 15, 14);

    public static Rgba64 Rgba64Default { get; } = new(0, 0, 0, 0);

    public static L16 L16Default { get; } = new(0);

    /// <summary>
    /// Decodes the planar RGB ColorMap values for an indexed TIFF frame.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format used when applying the ICC profile.</typeparam>
    /// <param name="colorMap">The encoded ColorMap tag values.</param>
    /// <param name="bitsPerSample">The frame's index sample layout.</param>
    /// <param name="profile">The source ICC profile, if present.</param>
    /// <param name="memoryAllocator">The allocator used for color conversion.</param>
    /// <returns>The decoded colors in index order, converted to sRGB when an applicable profile is present.</returns>
    public static Color[] DecodeColorMap<TPixel>(ushort[] colorMap, TiffBitsPerSample bitsPerSample, IccProfile? profile, MemoryAllocator memoryAllocator)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int indexBits = bitsPerSample.Channel0;
        if (indexBits is < 1 or > 16)
        {
            TiffThrowHelper.ThrowNotSupported("The TIFF palette index depth is not supported.");
        }

        int colorCount = 1 << indexBits;
        if (colorMap.Length < 3 * colorCount)
        {
            throw TiffThrowHelper.ThrowInvalidImageContentException("The TIFF ColorMap is shorter than the declared index depth.");
        }

        // TIFF stores red, green, and blue in three planes of 16-bit entries.
        // For libtiff compatibility, treat the ColorMap as eight-bit when every entry is at most 255.
        // Multiplying each entry by 257 maps its 0..255 range across the full 0..65535 range.
        bool legacy8Bit = !colorMap.AsSpan().ContainsAnyExceptInRange((ushort)0, (ushort)255);
        Color[] colors = new Color[colorCount];

        for (int i = 0; i < colorCount; i++)
        {
            ushort r = colorMap[i];
            ushort g = colorMap[colorCount + i];
            ushort b = colorMap[(colorCount * 2) + i];

            Rgb48 source = legacy8Bit
                ? new Rgb48((ushort)(r * 257), (ushort)(g * 257), (ushort)(b * 257))
                : new Rgb48(r, g, b);

            colors[i] = Color.FromPixel(source);
        }

        if (IccProfile.TryGetIccProfileForColorConversion(profile, out IccProfile? convertibleProfile))
        {
            ColorConversionOptions options = new()
            {
                SourceIccProfile = convertibleProfile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                MemoryAllocator = memoryAllocator
            };

            ColorProfileConverter converter = new(options);
            converter.Convert<TPixel>(colors);
        }

        return colors;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ConvertToUShortBigEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt16BigEndian(buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ConvertToUShortLittleEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt16LittleEndian(buffer);

    /// <summary>
    /// Copies TIFF sixteen-bit samples into native-endian storage, optionally inverting white-is-zero samples.
    /// </summary>
    /// <param name="source">The encoded sample bytes.</param>
    /// <param name="destination">The decoded sample values.</param>
    /// <param name="bigEndian">Whether the encoded samples use big-endian byte order.</param>
    /// <param name="invert">Whether to complement the decoded samples.</param>
    public static void CopyUShorts(ReadOnlySpan<byte> source, Span<ushort> destination, bool bigEndian, bool invert)
    {
        ref byte sourceRef = ref MemoryMarshal.GetReference(source);
        ref ushort destinationRef = ref MemoryMarshal.GetReference(destination);
        ReadOnlySpan<ushort> samples = destination;

        // Reverse stored bytes only when TIFF byte order differs from the host's native ushort order.
        bool reverse = bigEndian == BitConverter.IsLittleEndian;
        int i = 0;

        if (Vector128.IsHardwareAccelerated)
        {
            // Each 128-bit byte load holds eight complete ushort samples. Count destination ushort groups,
            // then use the same sample index for its 16 source bytes and eight native-endian outputs.
            nuint groups = samples.Vector128Count<ushort>();
            for (nuint group = 0; group < groups; group++, i += Vector128<ushort>.Count)
            {
                Vector128<byte> bytes = Vector128.LoadUnsafe(ref sourceRef, (nuint)(i * 2));

                // Byte swapping and white-is-zero inversion share one read/write traversal.
                if (reverse)
                {
                    bytes = Vector128.ShuffleNative(bytes, Reverse16BitByteIndices);
                }

                Vector128<ushort> values = bytes.AsUInt16();
                if (invert)
                {
                    values = ~values;
                }

                values.StoreUnsafe(ref destinationRef, (nuint)i);
            }
        }

        for (; i < destination.Length; i++)
        {
            ushort value = bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(source.Slice(i * 2)) : BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(i * 2));
            destination[i] = invert ? (ushort)~value : value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ConvertToUIntBigEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt32BigEndian(buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ConvertToUIntLittleEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt32LittleEndian(buffer);

    /// <summary>
    /// Reads one big-endian 24-bit sample using exactly its three encoded bytes.
    /// </summary>
    /// <param name="buffer">The three encoded bytes.</param>
    /// <returns>The unsigned 24-bit sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ConvertToUInt24BigEndian(ReadOnlySpan<byte> buffer)
    {
        // A strip can end after this sample; three byte reads avoid a fourth padding byte.
        return ((uint)buffer[0] << 16) | ((uint)buffer[1] << 8) | buffer[2];
    }

    /// <summary>
    /// Reads one little-endian 24-bit sample using exactly its three encoded bytes.
    /// </summary>
    /// <param name="buffer">The three encoded bytes.</param>
    /// <returns>The unsigned 24-bit sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ConvertToUInt24LittleEndian(ReadOnlySpan<byte> buffer)
    {
        // Assemble the stored bytes directly so the last sample needs no padding byte.
        return buffer[0] | ((uint)buffer[1] << 8) | ((uint)buffer[2] << 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorFromRgba64Premultiplied<TPixel>(ushort r, ushort g, ushort b, ushort a)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TIFF associated RGB is already multiplied by alpha. At zero alpha, TIFF defines decoded RGB as zero.
        // General pixel conversion may retain RGB hidden under zero alpha for other formats, such as EXR.
        if (a == 0)
        {
            return TPixel.FromAssociatedScaledVector4(Vector4.Zero);
        }

        // Pass these already-premultiplied channels to the associated pixel API.
        // Associated targets keep that representation; straight-alpha targets convert it to straight RGB.
        return TPixel.FromAssociatedScaledVector4(new Vector4(r, g, b, a) / ushort.MaxValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24Bit<TPixel>(uint r, uint g, uint b)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromUnassociatedScaledVector4(new Vector4(r, g, b, 1f) * Scale24BitVector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24Bit<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromUnassociatedScaledVector4(new Vector4(r, g, b, a) * Scale24Bit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24BitPremultiplied<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TIFF 6.0 assigns no color to a fully transparent associated sample, even when malformed input stores nonzero RGB.
        return a == 0
            ? TPixel.FromAssociatedScaledVector4(Vector4.Zero)
            : TPixel.FromAssociatedScaledVector4(new Vector4(r, g, b, a) * Scale24Bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32Bit<TPixel>(uint r, uint g, uint b)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromUnassociatedScaledVector4(new Vector4(r, g, b, 1f) * Scale32BitVector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32Bit<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromUnassociatedScaledVector4(new Vector4(r, g, b, a) * Scale32Bit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32BitPremultiplied<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TIFF 6.0 assigns no color to a fully transparent associated sample, even when malformed input stores nonzero RGB.
        return a == 0
            ? TPixel.FromAssociatedScaledVector4(Vector4.Zero)
            : TPixel.FromAssociatedScaledVector4(new Vector4(r, g, b, a) * Scale32Bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24Bit<TPixel>(uint intensity)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromUnassociatedScaledVector4(new Vector4(intensity, intensity, intensity, 1f) * Scale24BitVector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32Bit<TPixel>(uint intensity)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromUnassociatedScaledVector4(new Vector4(intensity, intensity, intensity, 1f) * Scale32BitVector);

    /// <summary>
    /// Finds the padding needed to round 'valueToRoundUp' to the next integer multiple of subSampling value.
    /// </summary>
    /// <param name="valueToRoundUp">The width or height to round up.</param>
    /// <param name="subSampling">The sub sampling.</param>
    /// <returns>The padding.</returns>
    public static int PaddingToNextInteger(int valueToRoundUp, int subSampling)
    {
        if (valueToRoundUp % subSampling == 0)
        {
            return 0;
        }

        return subSampling - (valueToRoundUp % subSampling);
    }
}
