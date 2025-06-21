// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing a single 16 bit floating point value.
/// <para>
/// Ranges from [-1, 0, 0, 1] to [1, 0, 0, 1] in vector form.
/// </para>
/// </summary>
public partial struct HalfSingle : IPixel<HalfSingle>, IPackedVector<ushort>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HalfSingle"/> struct.
    /// </summary>
    /// <param name="value">The single component value.</param>
    public HalfSingle(float value) => this.PackedValue = HalfTypeHelper.Pack(value);

    /// <inheritdoc/>
    public ushort PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="HalfSingle"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="HalfSingle"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="HalfSingle"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(HalfSingle left, HalfSingle right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="HalfSingle"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="HalfSingle"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="HalfSingle"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(HalfSingle left, HalfSingle right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
    {
        float single = this.ToSingle() + 1F;
        single /= 2F;
        return new(single, 0, 0, 1F);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.ToSingle(), 0, 0, 1F);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<HalfSingle>(
            PixelComponentInfo.Create<HalfSingle>(1, 16),
            PixelColorType.Red,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<HalfSingle> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromScaledVector4(Vector4 source)
    {
        float scaled = source.X;
        scaled *= 2F;
        scaled--;
        return new() { PackedValue = HalfTypeHelper.Pack(scaled) };
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromVector4(Vector4 source) => new() { PackedValue = HalfTypeHelper.Pack(source.X) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <summary>
    /// Expands the packed representation into a <see cref="float"/>.
    /// </summary>
    /// <returns>The <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ToSingle() => HalfTypeHelper.Unpack(this.PackedValue);

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is HalfSingle other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(HalfSingle other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString() => FormattableString.Invariant($"HalfSingle({this.ToSingle():#0.##})");

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();
}
