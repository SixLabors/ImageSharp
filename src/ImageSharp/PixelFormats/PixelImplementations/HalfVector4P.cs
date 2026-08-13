// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four associated 16-bit floating-point values.
/// </summary>
/// <remarks>
/// <see cref="ToVector4"/> returns the stored associated IEEE 754 binary16 values directly. Scaled vector conversions
/// normalize the finite range <c>[-65504, 65504]</c> to <c>[0, 1]</c> while preserving associated alpha. The packed
/// representation is binary-compatible with <c>DXGI_FORMAT_R16G16B16A16_FLOAT</c>.
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
    public readonly Vector4 ToScaledVector4() => HalfTypeHelper.ToScaled(this.ToVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4()
    {
        Vector4 vector = this.ToScaledVector4();
        Numerics.UnPremultiply(ref vector);
        return vector;
    }

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
        Vector4 vector = this.ToUnassociatedScaledVector4();
        return HalfTypeHelper.FromScaled(vector);
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
        source = HalfTypeHelper.ToScaled(source);
        return FromUnassociatedScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4P FromAssociatedVector4(Vector4 source)
    {
        source = HalfTypeHelper.ToScaled(source);
        return FromAssociatedScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4P FromUnassociatedScaledVector4(Vector4 source) => PackAssociatedScaledVector4(Associate(source));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HalfVector4P FromAssociatedScaledVector4(Vector4 source) => PackAssociatedScaledVector4(Reassociate(source));

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
    /// Converts an unassociated scaled vector to the associated representation of a half-precision destination.
    /// </summary>
    /// <param name="source">The unassociated scaled vector.</param>
    /// <returns>The associated scaled vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 Associate(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);

        // RGB must use the scaled alpha that the binary16 representation can reproduce.
        source.W = QuantizeScaledAlpha(source.W);
        Numerics.Premultiply(ref source);
        return source;
    }

    /// <summary>
    /// Reassociates a scaled vector with the alpha value the destination stores.
    /// </summary>
    /// <param name="source">The associated scaled vector.</param>
    /// <returns>The reassociated scaled vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 Reassociate(Vector4 source)
    {
        float alpha = source.W;

        if (alpha <= 0)
        {
            return Vector4.Zero;
        }

        float storedAlpha = QuantizeScaledAlpha(alpha);

        // Associated RGB scales by the same ratio as alpha. Applying that ratio directly avoids the extra division and multiplication of an unpremultiply/premultiply round trip and preserves exact midpoints when alpha needs no quantization.
        source *= storedAlpha / alpha;
        source.W = storedAlpha;
        Numerics.ClampRgbToAlpha(ref source);
        return source;
    }

    /// <summary>
    /// Packs an associated scaled vector into the native binary16 representation.
    /// </summary>
    /// <param name="source">The associated scaled vector.</param>
    /// <returns>The packed pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static HalfVector4P PackAssociatedScaledVector4(Vector4 source)
    {
        source = HalfTypeHelper.FromScaled(source);
        return new HalfVector4P { PackedValue = Pack(source) };
    }

    /// <summary>
    /// Quantizes scaled alpha through the native binary16 representation.
    /// </summary>
    /// <param name="alpha">The scaled alpha value.</param>
    /// <returns>The scaled value represented by the stored binary16 component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float QuantizeScaledAlpha(float alpha)
    {
        float nativeAlpha = HalfTypeHelper.FromScaled(Numerics.Clamp(alpha, 0F, 1F));
        return HalfTypeHelper.ToScaled(HalfTypeHelper.Unpack(HalfTypeHelper.Pack(nativeAlpha)));
    }

    /// <summary>
    /// Packs native half-precision components into a 64-bit value.
    /// </summary>
    /// <param name="vector">The native component values.</param>
    /// <returns>The packed value.</returns>
    private static ulong Pack(Vector4 vector)
    {
        ulong x = HalfTypeHelper.Pack(vector.X);
        ulong y = (ulong)HalfTypeHelper.Pack(vector.Y) << 0x10;
        ulong z = (ulong)HalfTypeHelper.Pack(vector.Z) << 0x20;
        ulong w = (ulong)HalfTypeHelper.Pack(vector.W) << 0x30;
        return x | y | z | w;
    }
}
