// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing a single 8-bit normalized luminance value.
/// <para>
/// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
public partial struct L8 : IPixel<L8>, IPackedVector<byte>
{
    private static readonly Vector4 MaxBytes = Vector128.Create(255f).AsVector4();
    private static readonly Vector4 Half = Vector128.Create(.5f).AsVector4();

    /// <summary>
    /// Initializes a new instance of the <see cref="L8"/> struct.
    /// </summary>
    /// <param name="luminance">The luminance component.</param>
    public L8(byte luminance) => this.PackedValue = luminance;

    /// <inheritdoc />
    public byte PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="L8"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="L8"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="L8"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(L8 left, L8 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="L8"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="L8"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="L8"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(L8 left, L8 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32()
    {
        byte rgb = this.PackedValue;
        return new Rgba32(rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4()
    {
        float rgb = this.PackedValue / 255f;
        return new Vector4(rgb, rgb, rgb, 1f);
    }

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<L8>(
            PixelComponentInfo.Create<L8>(1, 8),
            PixelColorType.Luminance,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<L8> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromVector4(Vector4 source) => new() { PackedValue = Pack(source) };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromAbgr32(Abgr32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromArgb32(Argb32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromBgr24(Bgr24 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromBgra32(Bgra32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromL8(L8 source) => new(source.PackedValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromL16(L16 source) => new(ColorNumerics.From16BitTo8Bit(source.PackedValue));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromLa16(La16 source) => new(source.L);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromLa32(La32 source) => new(ColorNumerics.From16BitTo8Bit(source.L));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromRgb24(Rgb24 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromRgba32(Rgba32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromRgb48(Rgb48 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L8 FromRgba64(Rgba64 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is L8 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(L8 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString() => $"L8({this.PackedValue})";

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Pack(Vector4 vector)
    {
        vector *= MaxBytes;
        vector += Half;
        vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);

        Vector128<byte> result = Vector128.ConvertToInt32(vector.AsVector128()).AsByte();
        return ColorNumerics.Get8BitBT709Luminance(result.GetElement(0), result.GetElement(4), result.GetElement(8));
    }
}
