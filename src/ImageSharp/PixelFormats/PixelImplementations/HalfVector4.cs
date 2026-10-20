// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four IEEE 754 half-precision floating-point values.
/// </summary>
/// <remarks>
/// Native vector conversions preserve the stored IEEE floating-point values.
/// Color-vector conversions bound color components to the format's finite range and alpha to [0,1].
/// The packed representation is binary-compatible with <c>DXGI_FORMAT_R16G16B16A16_FLOAT</c>.
/// </remarks>
public partial struct HalfVector4 : IPixel<HalfVector4>, IPackedVector<ulong>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HalfVector4"/> struct.
    /// </summary>
    /// <param name="x">The x-component.</param>
    /// <param name="y">The y-component.</param>
    /// <param name="z">The z-component.</param>
    /// <param name="w">The w-component.</param>
    public HalfVector4(float x, float y, float z, float w)
        : this(new Vector4(x, y, z, w))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HalfVector4"/> struct.
    /// </summary>
    /// <param name="vector">A vector containing the initial values for the components</param>
    public HalfVector4(Vector4 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc/>
    public ulong PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="HalfVector4"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="HalfVector4"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="HalfVector4"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(HalfVector4 left, HalfVector4 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="HalfVector4"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="HalfVector4"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="HalfVector4"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(HalfVector4 left, HalfVector4 right) => !left.Equals(right);

    /// <inheritdoc />
    public static HalfVector4 FromAbgr32P(Abgr32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static HalfVector4 FromArgb32P(Argb32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static HalfVector4 FromBgra32P(Bgra32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static HalfVector4 FromRgba32P(Rgba32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public readonly Rgba32P ToRgba32P() => Rgba32P.FromUnassociatedScaledVector4(this.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
        => new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue).Invoke(this.ToVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(
            HalfTypeHelper.Unpack((ushort)this.PackedValue),
            HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x10)),
            HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x20)),
            HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x30)));

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<HalfVector4>(
            PixelComponentInfo.Create<HalfVector4>(4, 16, 16, 16, 16),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelColorRange.Extended,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<HalfVector4> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4()
        => new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue).Invoke(this.ToUnassociatedVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4()
    {
        return new Vector4Converters.AssociateOperator<Vector4Converters.IdentityOperator>(
            default,
            new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)).Invoke(this.ToVector4());
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4()
    {
        Vector4 vector = this.ToVector4();
        Numerics.Premultiply(ref vector);
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromUnassociatedScaledVector4(Vector4 source)
        => FromUnassociatedVector4(new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue).Invoke(source));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromAssociatedScaledVector4(Vector4 source)
    {
        // Recover straight RGB using the supplied alpha, then bound opacity. Zero alpha retains stored RGB.
        source = new Vector4Converters.UnassociateOperator(new Vector4Converters.ColorClampOperator((float)Half.MinValue, (float)Half.MaxValue)).Invoke(source);
        return FromUnassociatedVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromUnassociatedVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromAssociatedVector4(Vector4 source)
    {
        Numerics.UnPremultiply(ref source);
        return FromVector4(source);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromScaledVector4(Vector4 source) => FromUnassociatedScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromVector4(Vector4 source) => new() { PackedValue = Pack(source) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is HalfVector4 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(HalfVector4 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector4 vector = this.ToVector4();
        return FormattableString.Invariant($"HalfVector4({vector.X:#0.##}, {vector.Y:#0.##}, {vector.Z:#0.##}, {vector.W:#0.##})");
    }

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <summary>
    /// Packs a <see cref="Vector4"/> into a <see cref="ulong"/>.
    /// </summary>
    /// <param name="vector">The vector containing the values to pack.</param>
    /// <returns>The <see cref="ulong"/> containing the packed values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Pack(Vector4 vector)
    {
        ulong num4 = HalfTypeHelper.Pack(vector.X);
        ulong num3 = (ulong)HalfTypeHelper.Pack(vector.Y) << 0x10;
        ulong num2 = (ulong)HalfTypeHelper.Pack(vector.Z) << 0x20;
        ulong num1 = (ulong)HalfTypeHelper.Pack(vector.W) << 0x30;
        return num4 | num3 | num2 | num1;
    }
}
