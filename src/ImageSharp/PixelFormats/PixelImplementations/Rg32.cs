// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing two 16-bit unsigned normalized values ranging from 0 to 1.
/// <para>
/// Ranges from [0, 0, 0, 1] to [1, 1, 0, 1] in vector form.
/// </para>
/// </summary>
public partial struct Rg32 : IPixel<Rg32>, IPackedVector<uint>
{
    private static readonly Vector2 Max = new(ushort.MaxValue);

    /// <summary>
    /// Initializes a new instance of the <see cref="Rg32"/> struct.
    /// </summary>
    /// <param name="x">The x-component</param>
    /// <param name="y">The y-component</param>
    public Rg32(float x, float y)
        : this(new Vector2(x, y))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rg32"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the component values.</param>
    public Rg32(Vector2 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc/>
    public uint PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="Rg32"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rg32"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rg32"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Rg32 left, Rg32 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Rg32"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rg32"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rg32"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Rg32 left, Rg32 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32()
        => new(
            ColorNumerics.From16BitTo8Bit((ushort)(this.PackedValue & 0xFFFF)),
            ColorNumerics.From16BitTo8Bit((ushort)(this.PackedValue >> 16)),
            byte.MinValue,
            byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.ToVector2(), 0f, 1f);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Rg32>(
            PixelComponentInfo.Create<Rg32>(2, 16, 16),
            PixelColorType.Red | PixelColorType.Green,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<Rg32> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromVector4(Vector4 source) => new() { PackedValue = Pack(new Vector2(source.X, source.Y)) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromAbgr32(Abgr32 source) => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromArgb32(Argb32 source) => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromBgr24(Bgr24 source) => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromBgra32(Bgra32 source) => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromL8(L8 source) => new(ColorNumerics.From8BitTo16Bit(source.PackedValue), ColorNumerics.From8BitTo16Bit(source.PackedValue));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromL16(L16 source) => new(source.PackedValue, source.PackedValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromLa16(La16 source) => new(ColorNumerics.From8BitTo16Bit(source.L), ColorNumerics.From8BitTo16Bit(source.L));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromLa32(La32 source) => new(source.L, source.L);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromRgb24(Rgb24 source) => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromRgba32(Rgba32 source) => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromRgb48(Rgb48 source) => new(source.R, source.G);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rg32 FromRgba64(Rgba64 source) => new(source.R, source.G);

    /// <summary>
    /// Expands the packed representation into a <see cref="Vector2"/>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2 ToVector2() => new Vector2(this.PackedValue & 0xFFFF, (this.PackedValue >> 16) & 0xFFFF) / Max;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Rg32 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(Rg32 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector2 vector = this.ToVector2();
        return FormattableString.Invariant($"Rg32({vector.X:#0.##}, {vector.Y:#0.##})");
    }

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(Vector2 vector)
    {
        vector = Vector2.Clamp(vector, Vector2.Zero, Vector2.One) * Max;
        return (uint)(((int)Math.Round(vector.X) & 0xFFFF) | (((int)Math.Round(vector.Y) & 0xFFFF) << 16));
    }
}
