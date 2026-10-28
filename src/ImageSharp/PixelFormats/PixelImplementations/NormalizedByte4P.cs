// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four associated 8-bit signed normalized values ranging from -1 to 1.
/// </summary>
/// <remarks>
/// <see cref="ToVector4"/> returns associated components in the native signed-normalized range <c>[-1, 1]</c>.
/// Scaled vector conversions return associated components in <c>[0, 1]</c>.
/// The packed two's-complement codes <c>-128</c> and <c>-127</c> both represent <c>-1</c>,
/// matching <c>DXGI_FORMAT_R8G8B8A8_SNORM</c>.
/// </remarks>
public partial struct NormalizedByte4P : IPixel<NormalizedByte4P>, IPackedVector<uint>
{
    private const float MaxPos = 127F;
    private const float ScaledMagnitude = MaxPos * 2F;
    private static readonly Vector4 Half = Vector128.Create(MaxPos).AsVector4();
    private static readonly Vector4 Minimum = -Half;
    private static readonly Vector4 MinusOne = Vector128.Create(-1F).AsVector4();

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedByte4P"/> struct.
    /// </summary>
    /// <param name="x">The associated x-component.</param>
    /// <param name="y">The associated y-component.</param>
    /// <param name="z">The associated z-component.</param>
    /// <param name="w">The alpha component.</param>
    public NormalizedByte4P(float x, float y, float z, float w)
        : this(new Vector4(x, y, z, w))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedByte4P"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the associated component values.</param>
    public NormalizedByte4P(Vector4 vector) => this.PackedValue = Pack(vector);

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedByte4P"/> struct from its packed representation.
    /// </summary>
    /// <remarks>
    /// Two's-complement X, Y, Z, and alpha codes occupy consecutive eight-bit fields, from least to most significant.
    /// This constructor copies the stored bits without numeric conversion, rounding, or clamping.
    /// RGB codes are already associated with alpha. This constructor applies no alpha conversion.
    /// </remarks>
    /// <param name="packed">The packed component bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NormalizedByte4P(uint packed)
        : this() => this.PackedValue = packed;

    /// <inheritdoc />
    public uint PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="NormalizedByte4P"/> values for equality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(NormalizedByte4P left, NormalizedByte4P right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="NormalizedByte4P"/> values for inequality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are not equal.</returns>
    public static bool operator !=(NormalizedByte4P left, NormalizedByte4P right) => !left.Equals(right);

    /// <inheritdoc />
    public static NormalizedByte4P FromAbgr32P(Abgr32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromArgb32P(Argb32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromBgra32P(Bgra32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromRgba32P(Rgba32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public readonly Rgba32P ToRgba32P() => Rgba32P.FromAssociatedScaledVector4(this.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(PixelOperations.ToUnassociatedScaledVector4(this));

    /// <inheritdoc />
    public readonly Vector4 ToScaledVector4()
    {
        // Offset the exact signed components before division. Mapping an already normalized value through (value + 1) / 2 loses precision near -1 through cancellation.
        Vector4 scaled = new(
            (sbyte)((this.PackedValue >> 0) & 0xFF),
            (sbyte)((this.PackedValue >> 8) & 0xFF),
            (sbyte)((this.PackedValue >> 16) & 0xFF),
            (sbyte)((this.PackedValue >> 24) & 0xFF));

        // SNORM reserves both minimum two's-complement codes for -1. Clamp before offsetting so raw -128 cannot escape the scaled range.
        return (Vector4.Max(scaled, Minimum) + Half) / ScaledMagnitude;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4() => PixelOperations.ToUnassociatedScaledVector4(this);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    public readonly Vector4 ToVector4()
    {
        Vector4 vector = new(
            (sbyte)((this.PackedValue >> 0) & 0xFF),
            (sbyte)((this.PackedValue >> 8) & 0xFF),
            (sbyte)((this.PackedValue >> 16) & 0xFF),
            (sbyte)((this.PackedValue >> 24) & 0xFF));

        // DirectX SNORM maps both -128 and -127 to -1.
        return Vector4.Max(vector, Minimum) / MaxPos;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4()
    {
        // Association is defined in the common scaled domain. Signed-native W is an affine encoding of alpha, so it cannot be used as a divisor directly.
        Vector4 vector = this.ToUnassociatedScaledVector4();
        vector *= 2F;
        vector -= Vector4.One;
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<NormalizedByte4P>(
            PixelComponentInfo.Create<NormalizedByte4P>(4, 8, 8, 8, 8),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelColorRange.Sdr,
            PixelAlphaRepresentation.Associated);

    /// <inheritdoc />
    public static PixelOperations<NormalizedByte4P> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    public static NormalizedByte4P FromScaledVector4(Vector4 source) => FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    public static NormalizedByte4P FromVector4(Vector4 source) => FromAssociatedVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4P FromUnassociatedVector4(Vector4 source)
    {
        // Convert the signed-native range to the common scaled domain before associating because native W is not opacity.
        source += Vector4.One;
        source /= 2F;
        return FromUnassociatedScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4P FromAssociatedVector4(Vector4 source)
    {
        // Reassociation must also operate in scaled space so RGB follows the quantized scaled alpha rather than the signed-native W component.
        source += Vector4.One;
        source /= 2F;
        return FromAssociatedScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4P FromUnassociatedScaledVector4(Vector4 source) => PackAssociatedScaledVector4(PixelOperations.Associate(source));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4P FromAssociatedScaledVector4(Vector4 source) => PackAssociatedScaledVector4(PixelOperations.Reassociate(source));

    /// <summary>
    /// Packs an associated scaled vector into signed-normalized storage.
    /// </summary>
    /// <param name="source">The associated scaled vector.</param>
    /// <returns>The packed pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static NormalizedByte4P PackAssociatedScaledVector4(Vector4 source)
    {
        // Signed-normalized storage uses the native [-1, 1] range even though association is defined in scaled opacity space.
        source *= 2F;
        source -= Vector4.One;
        return new NormalizedByte4P { PackedValue = Pack(source) };
    }

    /// <inheritdoc />
    public static NormalizedByte4P FromAbgr32(Abgr32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromArgb32(Argb32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromBgra5551(Bgra5551 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromBgr24(Bgr24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromBgra32(Bgra32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromL8(L8 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromL16(L16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromLa16(La16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromLa32(La32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromRgb24(Rgb24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromRgba32(Rgba32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromRgb48(Rgb48 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static NormalizedByte4P FromRgba64(Rgba64 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is NormalizedByte4P other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(NormalizedByte4P other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector4 vector = this.ToVector4();
        return FormattableString.Invariant($"NormalizedByte4P({vector.X:#0.##}, {vector.Y:#0.##}, {vector.Z:#0.##}, {vector.W:#0.##})");
    }

    /// <summary>
    /// Packs native signed normalized components into a 32-bit value.
    /// </summary>
    /// <param name="vector">The native component values.</param>
    /// <returns>The packed value.</returns>
    private static uint Pack(Vector4 vector)
    {
        vector = ColorNumerics.ClampToFinite(vector, MinusOne, Vector4.One) * Half;

        uint byte4 = ((uint)Convert.ToInt16(MathF.Round(vector.X)) & 0xFF) << 0;
        uint byte3 = ((uint)Convert.ToInt16(MathF.Round(vector.Y)) & 0xFF) << 8;
        uint byte2 = ((uint)Convert.ToInt16(MathF.Round(vector.Z)) & 0xFF) << 16;
        uint byte1 = ((uint)Convert.ToInt16(MathF.Round(vector.W)) & 0xFF) << 24;

        return byte4 | byte3 | byte2 | byte1;
    }
}
