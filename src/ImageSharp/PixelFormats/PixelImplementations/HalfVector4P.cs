// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four associated 16-bit floating-point values.
/// </summary>
/// <remarks>
/// Native and scaled vector conversions preserve the stored associated IEEE 754 binary16 values.
/// The packed representation is binary-compatible with <c>DXGI_FORMAT_R16G16B16A16_FLOAT</c>.
/// </remarks>
public partial struct HalfVector4P : IPixel<HalfVector4P>, IPackedVector<ulong>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HalfVector4P"/> struct.
    /// </summary>
    /// <param name="x">The associated x-component.</param>
    /// <param name="y">The associated y-component.</param>
    /// <param name="z">The associated z-component.</param>
    /// <param name="w">The alpha component.</param>
    public HalfVector4P(float x, float y, float z, float w)
        : this(new Vector4(x, y, z, w))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HalfVector4P"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the associated component values.</param>
    public HalfVector4P(Vector4 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc />
    public ulong PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="HalfVector4P"/> values for equality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(HalfVector4P left, HalfVector4P right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="HalfVector4P"/> values for inequality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are not equal.</returns>
    public static bool operator !=(HalfVector4P left, HalfVector4P right) => !left.Equals(right);

    /// <inheritdoc />
    public readonly Rgba32 ToRgba32()
    {
        return Rgba32.FromScaledVector4(this.ToUnassociatedScaledVector4());
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4() => this.ToUnassociatedVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    public readonly Vector4 ToVector4() => new(
        HalfTypeHelper.Unpack((ushort)this.PackedValue),
        HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x10)),
        HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x20)),
        HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x30)));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4()
    {
        Vector4 vector = this.ToVector4();
        Numerics.UnPremultiply(ref vector);
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<HalfVector4P>(
            PixelComponentInfo.Create<HalfVector4P>(4, 16, 16, 16, 16),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Associated);

    /// <inheritdoc />
    public static PixelOperations<HalfVector4P> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    public static HalfVector4P FromScaledVector4(Vector4 source) => FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    public static HalfVector4P FromVector4(Vector4 source) => FromAssociatedVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4P FromUnassociatedVector4(Vector4 source)
    {
        // Association uses the alpha that binary16 stores so the color components
        // retain their straight values when the supplied alpha rounds on packing.
        source.W = HalfTypeHelper.Unpack(HalfTypeHelper.Pack(source.W));
        Numerics.Premultiply(ref source);
        return new HalfVector4P(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4P FromAssociatedVector4(Vector4 source)
        => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4P FromUnassociatedScaledVector4(Vector4 source) => FromUnassociatedVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4P FromAssociatedScaledVector4(Vector4 source) => FromAssociatedVector4(source);

    /// <inheritdoc />
    public static HalfVector4P FromAbgr32(Abgr32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromArgb32(Argb32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromBgra5551(Bgra5551 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromBgr24(Bgr24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromBgra32(Bgra32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromL8(L8 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromL16(L16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromLa16(La16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromLa32(La32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromRgb24(Rgb24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromRgba32(Rgba32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromRgb48(Rgb48 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static HalfVector4P FromRgba64(Rgba64 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is HalfVector4P other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(HalfVector4P other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector4 vector = this.ToVector4();
        return FormattableString.Invariant($"HalfVector4P({vector.X:#0.##}, {vector.Y:#0.##}, {vector.Z:#0.##}, {vector.W:#0.##})");
    }

    /// <summary>
    /// Packs the four native binary16 components in DirectX component order.
    /// </summary>
    /// <param name="vector">The component values.</param>
    /// <returns>The packed binary16 value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Pack(Vector4 vector)
    {
        ulong x = HalfTypeHelper.Pack(vector.X);
        ulong y = (ulong)HalfTypeHelper.Pack(vector.Y) << 0x10;
        ulong z = (ulong)HalfTypeHelper.Pack(vector.Z) << 0x20;
        ulong w = (ulong)HalfTypeHelper.Pack(vector.W) << 0x30;
        return x | y | z | w;
    }
}
