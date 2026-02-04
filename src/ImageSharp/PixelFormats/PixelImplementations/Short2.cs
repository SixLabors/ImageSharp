// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing two 16-bit signed integer values.
/// <para>
/// Ranges from [-32767, -32767, 0, 1] to [32767, 32767, 0, 1] in vector form.
/// </para>
/// </summary>
public partial struct Short2 : IPixel<Short2>, IPackedVector<uint>
{
    // Largest two byte positive number 0xFFFF >> 1;
    private const float MaxPos = 0x7FFF;

    // Two's complement
    private const float MinNeg = ~(int)MaxPos;

    private static readonly Vector2 Max = new(MaxPos);
    private static readonly Vector2 Min = new(MinNeg);

    /// <summary>
    /// Initializes a new instance of the <see cref="Short2"/> struct.
    /// </summary>
    /// <param name="x">The x-component.</param>
    /// <param name="y">The y-component.</param>
    public Short2(float x, float y)
        : this(new Vector2(x, y))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Short2"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the component values.</param>
    public Short2(Vector2 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc/>
    public uint PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="Short2"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Short2"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Short2"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Short2 left, Short2 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Short2"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Short2"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Short2"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Short2 left, Short2 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
    {
        Vector2 scaled = this.ToVector2();
        scaled += new Vector2(32767f);
        scaled /= 65534F;
        return new Vector4(scaled, 0f, 1f);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new((short)(this.PackedValue & 0xFFFF), (short)(this.PackedValue >> 0x10), 0f, 1f);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Short2>(
            PixelComponentInfo.Create<Short2>(2, 16, 16),
            PixelColorType.Red | PixelColorType.Green,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<Short2> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromScaledVector4(Vector4 source)
    {
        Vector2 scaled = new Vector2(source.X, source.Y) * 65534F;
        scaled -= new Vector2(32767F);
        return new Short2 { PackedValue = Pack(scaled) };
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromVector4(Vector4 source) => new() { PackedValue = Pack(new Vector2(source.X, source.Y)) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Short2 FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <summary>
    /// Expands the packed representation into a <see cref="Vector2"/>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2 ToVector2() => new((short)(this.PackedValue & 0xFFFF), (short)(this.PackedValue >> 0x10));

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Short2 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(Short2 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector2 vector = this.ToVector2();
        return FormattableString.Invariant($"Short2({vector.X:#0.##}, {vector.Y:#0.##})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(Vector2 vector)
    {
        vector = Vector2.Clamp(vector, Min, Max);
        uint word2 = (uint)Convert.ToInt32(Math.Round(vector.X)) & 0xFFFF;
        uint word1 = ((uint)Convert.ToInt32(Math.Round(vector.Y)) & 0xFFFF) << 0x10;

        return word2 | word1;
    }
}
