// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing three 16-bit unsigned normalized values ranging from 0 to 65535.
/// <para>
/// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public partial struct Rgb48 : IPixel<Rgb48>
{
    private const float Max = ushort.MaxValue;

    /// <summary>
    /// Gets or sets the red component.
    /// </summary>
    public ushort R;

    /// <summary>
    /// Gets or sets the green component.
    /// </summary>
    public ushort G;

    /// <summary>
    /// Gets or sets the blue component.
    /// </summary>
    public ushort B;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb48"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    public Rgb48(ushort r, ushort g, ushort b)
        : this()
    {
        this.R = r;
        this.G = g;
        this.B = b;
    }

    /// <summary>
    /// Compares two <see cref="Rgb48"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgb48"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgb48"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Rgb48 left, Rgb48 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Rgb48"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgb48"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgb48"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Rgb48 left, Rgb48 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromRgb48(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.R / Max, this.G / Max, this.B / Max, 1f);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Rgb48>(
            PixelComponentInfo.Create<Rgb48>(3, 16, 16, 16),
            PixelColorType.RGB,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<Rgb48> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromVector4(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One) * Max;
        return new Rgb48((ushort)MathF.Round(source.X), (ushort)MathF.Round(source.Y), (ushort)MathF.Round(source.Z));
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromAbgr32(Abgr32 source)
        => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G), ColorNumerics.From8BitTo16Bit(source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromArgb32(Argb32 source)
        => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G), ColorNumerics.From8BitTo16Bit(source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromBgr24(Bgr24 source)
        => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G), ColorNumerics.From8BitTo16Bit(source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromBgra32(Bgra32 source)
        => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G), ColorNumerics.From8BitTo16Bit(source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromL8(L8 source)
    {
        ushort rgb = ColorNumerics.From8BitTo16Bit(source.PackedValue);
        return new Rgb48(rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromL16(L16 source) => new(source.PackedValue, source.PackedValue, source.PackedValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromLa16(La16 source)
    {
        ushort rgb = ColorNumerics.From8BitTo16Bit(source.L);
        return new Rgb48(rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromLa32(La32 source) => new(source.L, source.L, source.L);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromRgb24(Rgb24 source)
        => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G), ColorNumerics.From8BitTo16Bit(source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromRgba32(Rgba32 source)
        => new(ColorNumerics.From8BitTo16Bit(source.R), ColorNumerics.From8BitTo16Bit(source.G), ColorNumerics.From8BitTo16Bit(source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromRgb48(Rgb48 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgb48 FromRgba64(Rgba64 source) => new(source.R, source.G, source.B);

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Rgb48 rgb48 && this.Equals(rgb48);

    /// <inheritdoc />
    public readonly bool Equals(Rgb48 other) => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B);

    /// <inheritdoc />
    public override readonly string ToString() => $"Rgb48({this.R}, {this.G}, {this.B})";

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(this.R, this.G, this.B);
}
