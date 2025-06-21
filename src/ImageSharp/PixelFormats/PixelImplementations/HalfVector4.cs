// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four 16-bit floating-point values.
/// <para>
/// Ranges from [-1, -1, -1, -1] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
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
        : this(new(x, y, z, w))
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
    {
        Vector4 scaled = this.ToVector4();
        scaled += Vector4.One;
        scaled /= 2f;
        return scaled;
    }

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
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<HalfVector4> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4 FromScaledVector4(Vector4 source)
    {
        source *= 2f;
        source -= Vector4.One;
        return FromVector4(source);
    }

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
