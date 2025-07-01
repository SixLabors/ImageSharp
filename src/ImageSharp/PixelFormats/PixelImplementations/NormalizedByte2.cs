// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing two 8-bit signed normalized values, ranging from −1 to 1.
/// <para>
/// Ranges from [-1, -1, 0, 1] to [1, 1, 0, 1] in vector form.
/// </para>
/// </summary>
public partial struct NormalizedByte2 : IPixel<NormalizedByte2>, IPackedVector<ushort>
{
    private const float MaxPos = 127f;
    private static readonly Vector2 Half = new(MaxPos);
    private static readonly Vector2 MinusOne = new(-1f);

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedByte2"/> struct.
    /// </summary>
    /// <param name="x">The x-component.</param>
    /// <param name="y">The y-component.</param>
    public NormalizedByte2(float x, float y)
        : this(new Vector2(x, y))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedByte2"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the component values.</param>
    public NormalizedByte2(Vector2 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc/>
    public ushort PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="NormalizedByte2"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="NormalizedByte2"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="NormalizedByte2"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(NormalizedByte2 left, NormalizedByte2 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="NormalizedByte2"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="NormalizedByte2"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="NormalizedByte2"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(NormalizedByte2 left, NormalizedByte2 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
    {
        Vector2 scaled = this.ToVector2();
        scaled += Vector2.One;
        scaled /= 2f;
        return new Vector4(scaled, 0f, 1f);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.ToVector2(), 0f, 1f);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<NormalizedByte2>(
            PixelComponentInfo.Create<NormalizedByte2>(2, 8, 8),
            PixelColorType.Red | PixelColorType.Green,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<NormalizedByte2> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromScaledVector4(Vector4 source)
    {
        Vector2 scaled = new Vector2(source.X, source.Y) * 2f;
        scaled -= Vector2.One;
        return new NormalizedByte2 { PackedValue = Pack(scaled) };
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromVector4(Vector4 source) => new() { PackedValue = Pack(new Vector2(source.X, source.Y)) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedByte2 FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <summary>
    /// Expands the packed representation into a <see cref="Vector2"/>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2 ToVector2() => new(
            (sbyte)((this.PackedValue >> 0) & 0xFF) / MaxPos,
            (sbyte)((this.PackedValue >> 8) & 0xFF) / MaxPos);

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is NormalizedByte2 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(NormalizedByte2 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector2 vector = this.ToVector2();
        return FormattableString.Invariant($"NormalizedByte2({vector.X:#0.##}, {vector.Y:#0.##})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Pack(Vector2 vector)
    {
        vector = Vector2.Clamp(vector, MinusOne, Vector2.One) * Half;

        int byte2 = ((ushort)Convert.ToInt16(Math.Round(vector.X)) & 0xFF) << 0;
        int byte1 = ((ushort)Convert.ToInt16(Math.Round(vector.Y)) & 0xFF) << 8;

        return (ushort)(byte2 | byte1);
    }
}
