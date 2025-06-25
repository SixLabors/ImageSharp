// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Pixel type containing three 8-bit unsigned normalized values ranging from 0 to 255.
/// The color components are stored in blue, green, red order (least significant to most significant byte).
/// <para>
/// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public partial struct Bgr24 : IPixel<Bgr24>
{
    /// <summary>
    /// The blue component.
    /// </summary>
    [FieldOffset(0)]
    public byte B;

    /// <summary>
    /// The green component.
    /// </summary>
    [FieldOffset(1)]
    public byte G;

    /// <summary>
    /// The red component.
    /// </summary>
    [FieldOffset(2)]
    public byte R;

    private static readonly Vector4 MaxBytes = Vector128.Create(255f).AsVector4();
    private static readonly Vector4 Half = Vector128.Create(.5f).AsVector4();

    /// <summary>
    /// Initializes a new instance of the <see cref="Bgr24"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bgr24(byte r, byte g, byte b)
    {
        this.R = r;
        this.G = g;
        this.B = b;
    }

    /// <summary>
    /// Compares two <see cref="Bgr24"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgr24"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgr24"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Bgr24 left, Bgr24 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Bgr24"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgr24"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgr24"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Bgr24 left, Bgr24 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromBgr24(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, byte.MaxValue) / MaxBytes;

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Bgr24>(
            PixelComponentInfo.Create<Bgr24>(3, 8, 8, 8),
            PixelColorType.BGR,
            PixelAlphaRepresentation.None);

    /// <inheritdoc/>
    public static PixelOperations<Bgr24> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromVector4(Vector4 source)
    {
        source *= MaxBytes;
        source += Half;
        source = Numerics.Clamp(source, Vector4.Zero, MaxBytes);

        Vector128<byte> result = Vector128.ConvertToInt32(source.AsVector128()).AsByte();
        return new Bgr24(result.GetElement(0), result.GetElement(4), result.GetElement(8));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromAbgr32(Abgr32 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromArgb32(Argb32 source) => new(source.R, source.G, source.B);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromBgr24(Bgr24 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromBgra32(Bgra32 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromL8(L8 source) => new(source.PackedValue, source.PackedValue, source.PackedValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromL16(L16 source)
    {
        byte rgb = ColorNumerics.From16BitTo8Bit(source.PackedValue);
        return new Bgr24(rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromLa16(La16 source) => new(source.L, source.L, source.L);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromLa32(La32 source)
    {
        byte rgb = ColorNumerics.From16BitTo8Bit(source.L);
        return new Bgr24(rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromRgb24(Rgb24 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromRgba32(Rgba32 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromRgb48(Rgb48 source)
        => new()
        {
            R = ColorNumerics.From16BitTo8Bit(source.R),
            G = ColorNumerics.From16BitTo8Bit(source.G),
            B = ColorNumerics.From16BitTo8Bit(source.B)
        };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr24 FromRgba64(Rgba64 source)
        => new()
        {
            R = ColorNumerics.From16BitTo8Bit(source.R),
            G = ColorNumerics.From16BitTo8Bit(source.G),
            B = ColorNumerics.From16BitTo8Bit(source.B)
        };

    /// <inheritdoc/>
    public readonly bool Equals(Bgr24 other) => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Bgr24 other && this.Equals(other);

    /// <inheritdoc />
    public override readonly string ToString() => $"Bgr24({this.B}, {this.G}, {this.R})";

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(this.R, this.B, this.G);
}
