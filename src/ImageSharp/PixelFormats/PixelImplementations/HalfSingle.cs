// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing a single IEEE 754 half-precision floating-point value.
/// </summary>
/// <remarks>
/// Native vector conversions preserve the stored IEEE floating-point values.
/// Color-vector conversions bound color components to [-65504,65504].
/// The packed representation is binary-compatible with
/// <c>DXGI_FORMAT_R16_FLOAT</c>.
/// </remarks>
public partial struct HalfSingle : IPixel<HalfSingle>, IPackedVector<ushort>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HalfSingle"/> struct by converting a numeric value to half precision.
    /// </summary>
    /// <remarks>
    /// Use a <see cref="float"/> argument, such as <c>1F</c>, for numeric conversion.
    /// Integer constants in the <see cref="ushort"/> range select <see cref="HalfSingle(ushort)"/> and are treated as packed bits.
    /// </remarks>
    /// <param name="value">The single component value.</param>
    public HalfSingle(float value) => this.PackedValue = HalfTypeHelper.Pack(value);

    /// <summary>
    /// Initializes a new instance of the <see cref="HalfSingle"/> struct from its packed representation.
    /// </summary>
    /// <remarks>
    /// The half-precision component bit pattern occupies bits 0-15.
    /// This constructor copies the stored bits without numeric conversion, rounding, or clamping.
    /// Use <see cref="HalfSingle(float)"/> to convert a numeric value to half precision.
    /// </remarks>
    /// <param name="packed">The packed component bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HalfSingle(ushort packed)
        : this() => this.PackedValue = packed;

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
    public static HalfSingle FromAbgr32P(Abgr32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static HalfSingle FromArgb32P(Argb32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static HalfSingle FromBgra32P(Bgra32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static HalfSingle FromRgba32P(Rgba32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public readonly Rgba32P ToRgba32P() => Rgba32P.FromUnassociatedScaledVector4(this.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
        => PixelOperations.ClampToPixelRange(this.ToVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.ToSingle(), 0, 0, 1F);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<HalfSingle>(
            PixelComponentInfo.Create<HalfSingle>(1, 16),
            PixelColorType.Red,
            PixelColorRange.Extended,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<HalfSingle> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4()
        => PixelOperations.ClampToPixelRange(this.ToUnassociatedVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4()
        => PixelOperations.ClampToPixelRange(this.ToAssociatedVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromUnassociatedScaledVector4(Vector4 source)
        => FromUnassociatedVector4(PixelOperations.ClampToPixelRange(source));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromAssociatedScaledVector4(Vector4 source)
        => PixelOperations.FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromUnassociatedVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromAssociatedVector4(Vector4 source)
    {
        Numerics.UnPremultiply(ref source);
        return FromVector4(source);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfSingle FromScaledVector4(Vector4 source) => FromUnassociatedScaledVector4(source);

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
