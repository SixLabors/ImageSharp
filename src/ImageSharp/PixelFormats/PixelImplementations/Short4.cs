// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four 16-bit signed integer values.
/// <para>
/// Ranges from [-37267, -37267, -37267, -37267] to [37267, 37267, 37267, 37267] in vector form.
/// </para>
/// </summary>
public partial struct Short4 : IPixel<Short4>, IPackedVector<ulong>
{
    // Largest two byte positive number 0xFFFF >> 1;
    private const float MaxPos = 0x7FFF;

    // Two's complement
    private const float MinNeg = ~(int)MaxPos;

    private static readonly Vector4 Max = new(MaxPos);
    private static readonly Vector4 Min = new(MinNeg);

    /// <summary>
    /// Initializes a new instance of the <see cref="Short4"/> struct.
    /// </summary>
    /// <param name="x">The x-component.</param>
    /// <param name="y">The y-component.</param>
    /// <param name="z">The z-component.</param>
    /// <param name="w">The w-component.</param>
    public Short4(float x, float y, float z, float w)
        : this(new(x, y, z, w))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Short4"/> struct.
    /// </summary>
    /// <param name="vector">A vector containing the initial values for the components.</param>
    public Short4(Vector4 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc/>
    public ulong PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="Short4"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Short4"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Short4"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Short4 left, Short4 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Short4"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Short4"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Short4"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Short4 left, Short4 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
    {
        Vector4 scaled = this.ToVector4();
        scaled += new Vector4(32767f);
        scaled /= 65534f;
        return scaled;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4()
        => new(
            (short)(this.PackedValue & 0xFFFF),
            (short)((this.PackedValue >> 0x10) & 0xFFFF),
            (short)((this.PackedValue >> 0x20) & 0xFFFF),
            (short)((this.PackedValue >> 0x30) & 0xFFFF));

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Short4>(
            PixelComponentInfo.Create<Short4>(4, 16, 16, 16, 16),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<Short4> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromScaledVector4(Vector4 source)
    {
        source *= 65534F;
        source -= new Vector4(32767F);
        return FromVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromVector4(Vector4 source) => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short4 FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Short4 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(Short4 other) => this.PackedValue.Equals(other.PackedValue);

    /// <summary>
    /// Gets the hash code for the current instance.
    /// </summary>
    /// <returns>Hash code for the instance.</returns>
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector4 vector = this.ToVector4();
        return FormattableString.Invariant($"Short4({vector.X:#0.##}, {vector.Y:#0.##}, {vector.Z:#0.##}, {vector.W:#0.##})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Pack(Vector4 vector)
    {
        // Clamp the value between min and max values
        vector = Numerics.Clamp(vector, Min, Max);
        ulong word4 = ((ulong)Convert.ToInt32(Math.Round(vector.X)) & 0xFFFF) << 0x00;
        ulong word3 = ((ulong)Convert.ToInt32(Math.Round(vector.Y)) & 0xFFFF) << 0x10;
        ulong word2 = ((ulong)Convert.ToInt32(Math.Round(vector.Z)) & 0xFFFF) << 0x20;
        ulong word1 = ((ulong)Convert.ToInt32(Math.Round(vector.W)) & 0xFFFF) << 0x30;

        return word4 | word3 | word2 | word1;
    }
}
