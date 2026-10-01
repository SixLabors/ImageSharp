// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
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

        // TIFF stores red, green and blue as separate 16-bit planes. Match libtiff's
        // legacy detection: values all within 0..255 are byte samples, and multiplying
        // by 257 maps each byte exactly into the full 0..65535 range before Color retains it.
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ConvertToUIntBigEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt32BigEndian(buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ConvertToUIntLittleEndian(ReadOnlySpan<byte> buffer) => BinaryPrimitives.ReadUInt32LittleEndian(buffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorFromRgba64Premultiplied<TPixel>(ushort r, ushort g, ushort b, ushort a)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // TIFF 6.0 requires associated RGB to be interpreted as zero when alpha is zero. Enforce that codec contract here
        // while leaving the general associated-alpha conversion contract free to preserve hidden RGB for formats such as EXR.
        if (a == 0)
        {
            return TPixel.FromAssociatedScaledVector4(Vector4.Zero);
        }

        // TIFF marks these samples as already associated. Passing that representation directly avoids an unassociate/reassociate
        // round trip for associated destinations while unassociated formats perform the required conversion in their pixel contract.
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
