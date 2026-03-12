// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
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
        if (a == 0)
        {
            return TPixel.FromRgba64(default);
        }

        float scale = 65535f / a;
        ushort ur = (ushort)Math.Min(r * scale, 65535);
        ushort ug = (ushort)Math.Min(g * scale, 65535);
        ushort ub = (ushort)Math.Min(b * scale, 65535);

        return TPixel.FromRgba64(new Rgba64(ur, ug, ub, a));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24Bit<TPixel>(uint r, uint g, uint b)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromScaledVector4(new Vector4(r, g, b, 1f) * Scale24BitVector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24Bit<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromScaledVector4(new Vector4(r, g, b, a) * Scale24Bit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24BitPremultiplied<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4 colorVector = new Vector4(r, g, b, a) * Scale24Bit;
        return UnPremultiply<TPixel>(ref colorVector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32Bit<TPixel>(uint r, uint g, uint b)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromScaledVector4(new Vector4(r, g, b, 1f) * Scale32BitVector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32Bit<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromScaledVector4(new Vector4(r, g, b, a) * Scale32Bit);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32BitPremultiplied<TPixel>(uint r, uint g, uint b, uint a)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4 vector = new Vector4(r, g, b, a) * Scale32Bit;
        return UnPremultiply<TPixel>(ref vector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo24Bit<TPixel>(uint intensity)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromScaledVector4(new Vector4(intensity, intensity, intensity, 1f) * Scale24BitVector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel ColorScaleTo32Bit<TPixel>(uint intensity)
        where TPixel : unmanaged, IPixel<TPixel>
        => TPixel.FromScaledVector4(new Vector4(intensity, intensity, intensity, 1f) * Scale32BitVector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPixel UnPremultiply<TPixel>(ref Vector4 vector)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Numerics.UnPremultiply(ref vector);
        return TPixel.FromScaledVector4(vector);
    }

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
