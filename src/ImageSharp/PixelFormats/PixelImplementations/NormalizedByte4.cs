// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four 8-bit signed normalized values.
/// </summary>
/// <remarks>
/// <see cref="ToVector4"/> returns components in the native signed-normalized range <c>[-1, 1]</c>.
/// Scaled vector conversions return components in <c>[0, 1]</c>.
/// The packed two's-complement codes <c>-128</c> and <c>-127</c> both represent <c>-1</c>,
/// matching <c>DXGI_FORMAT_R8G8B8A8_SNORM</c>.
/// </remarks>
public partial struct NormalizedByte4 : IPixel<NormalizedByte4>, IPackedVector<uint>
{
    private const float MaxPos = 127f;
    private const float ScaledMagnitude = MaxPos * 2F;
    private static readonly Vector4 Half = Vector128.Create(MaxPos).AsVector4();
    private static readonly Vector4 Minimum = -Half;
    private static readonly Vector4 MinusOne = Vector128.Create(-1f).AsVector4();

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedByte4"/> struct.
    /// </summary>
    /// <param name="x">The x-component.</param>
    /// <param name="y">The y-component.</param>
    /// <param name="z">The z-component.</param>
    /// <param name="w">The w-component.</param>
    public NormalizedByte4(float x, float y, float z, float w)
        : this(new Vector4(x, y, z, w))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedByte4"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the component values.</param>
    public NormalizedByte4(Vector4 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc/>
    public uint PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="NormalizedByte4"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="NormalizedByte4"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="NormalizedByte4"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(NormalizedByte4 left, NormalizedByte4 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="NormalizedByte4"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="NormalizedByte4"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="NormalizedByte4"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(NormalizedByte4 left, NormalizedByte4 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<NormalizedByte4>(
            PixelComponentInfo.Create<NormalizedByte4>(4, 8, 8, 8, 8),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<NormalizedByte4> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4()
    {
        Vector4 vector = this.ToScaledVector4();
        Numerics.Premultiply(ref vector);
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4()
    {
        Vector4 vector = this.ToAssociatedScaledVector4();

        // Native components use an affine [-1, 1] encoding, so direct multiplication would use the wrong zero point.
        vector *= 2F;
        vector -= Vector4.One;
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromUnassociatedScaledVector4(Vector4 source) => FromScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromAssociatedScaledVector4(Vector4 source)
    {
        Numerics.UnPremultiply(ref source);
        return FromScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromUnassociatedVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromAssociatedVector4(Vector4 source)
    {
        // Map the affine native encoding to logical [0, 1] space before unassociating.
        source += Vector4.One;
        source /= 2F;
        return FromAssociatedScaledVector4(source);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromScaledVector4(Vector4 source)
    {
        source *= 2f;
        source -= Vector4.One;
        return FromVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte4 FromVector4(Vector4 source) => new() { PackedValue = Pack(source) };

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is NormalizedByte4 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(NormalizedByte4 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector4 vector = this.ToVector4();
        return FormattableString.Invariant($"NormalizedByte4({vector.X:#0.##}, {vector.Y:#0.##}, {vector.Z:#0.##}, {vector.W:#0.##})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(Vector4 vector)
    {
        vector = Numerics.Clamp(vector, MinusOne, Vector4.One) * Half;

        uint byte4 = ((uint)Convert.ToInt16(MathF.Round(vector.X)) & 0xFF) << 0;
        uint byte3 = ((uint)Convert.ToInt16(MathF.Round(vector.Y)) & 0xFF) << 8;
        uint byte2 = ((uint)Convert.ToInt16(MathF.Round(vector.Z)) & 0xFF) << 16;
        uint byte1 = ((uint)Convert.ToInt16(MathF.Round(vector.W)) & 0xFF) << 24;

        return byte4 | byte3 | byte2 | byte1;
    }
}
