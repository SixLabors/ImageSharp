// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Pixel type containing four 32-bit unsigned normalized values ranging from 0 to 4294967295.
/// The color components are stored in red, green, blue and alpha.
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public partial struct Rgba128 : IPixel<Rgba128>, IEquatable<Rgba128>
{
    private const float InvMax = 1.0f / uint.MaxValue;

    private const float Max = uint.MaxValue;

    /// <summary>
    /// Gets the red component.
    /// </summary>
    public uint R;

    /// <summary>
    /// Gets the green component.
    /// </summary>
    public uint G;

    /// <summary>
    /// Gets the blue component.
    /// </summary>
    public uint B;

    /// <summary>
    /// Gets the alpha channel.
    /// </summary>
    public uint A;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba128"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <param name="a">The alpha component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba128(uint r, uint g, uint b, uint a)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    /// <summary>
    /// Compares two <see cref="Rgba128"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgba128"/> on the left side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    /// <param name="right">The <see cref="Rgba128"/> on the right side of the operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Rgba128 left, Rgba128 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Rgba128"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgba128"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgba128"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Rgba128 left, Rgba128 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(
            this.R * InvMax,
            this.G * InvMax,
            this.B * InvMax,
            this.A * InvMax);

    /// <inheritdoc/>
    public static PixelOperations<Rgba128> CreatePixelOperations() => new();

    /// <inheritdoc/>
    public static Rgba128 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc/>
    public static Rgba128 FromVector4(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One) * Max;
        return new Rgba128((uint)MathF.Round(source.X), (uint)MathF.Round(source.Y), (uint)MathF.Round(source.Z), (uint)MathF.Round(source.W));
    }

    /// <inheritdoc/>
    public static Rgba128 FromAbgr32(Abgr32 source)
        => new(ColorNumerics.From8BitTo32Bit(source.R), ColorNumerics.From8BitTo32Bit(source.G), ColorNumerics.From8BitTo32Bit(source.B), ColorNumerics.From8BitTo32Bit(source.A));

    /// <inheritdoc/>
    public static Rgba128 FromArgb32(Argb32 source)
        => new(ColorNumerics.From8BitTo32Bit(source.R), ColorNumerics.From8BitTo32Bit(source.G), ColorNumerics.From8BitTo32Bit(source.B), ColorNumerics.From8BitTo32Bit(source.A));

    /// <inheritdoc/>
    public static Rgba128 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    public static Rgba128 FromBgr24(Bgr24 source)
        => new(ColorNumerics.From8BitTo32Bit(source.R), ColorNumerics.From8BitTo32Bit(source.G), ColorNumerics.From8BitTo32Bit(source.B), uint.MaxValue);

    /// <inheritdoc/>
    public static Rgba128 FromBgra32(Bgra32 source)
        => new(ColorNumerics.From8BitTo32Bit(source.R), ColorNumerics.From8BitTo32Bit(source.G), ColorNumerics.From8BitTo32Bit(source.B), ColorNumerics.From8BitTo32Bit(source.A));

    /// <inheritdoc/>
    public static Rgba128 FromL8(L8 source)
    {
        uint rgb = ColorNumerics.From8BitTo32Bit(source.PackedValue);
        return new Rgba128(rgb, rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    public static Rgba128 FromL16(L16 source)
    {
        uint rgb = ColorNumerics.From16BitTo32Bit(source.PackedValue);
        return new(rgb, rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    public static Rgba128 FromLa16(La16 source)
    {
        uint rgb = ColorNumerics.From8BitTo32Bit((byte)source.PackedValue);
        return new(rgb, rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    public static Rgba128 FromLa32(La32 source)
    {
        uint rgb = ColorNumerics.From16BitTo32Bit(source.L);
        return new(rgb, rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    public static Rgba128 FromRgb24(Rgb24 source)
        => new(ColorNumerics.From8BitTo32Bit(source.R), ColorNumerics.From8BitTo32Bit(source.G), ColorNumerics.From8BitTo32Bit(source.B), uint.MaxValue);

    /// <inheritdoc/>
    public static Rgba128 FromRgba32(Rgba32 source)
        => new(ColorNumerics.From8BitTo32Bit(source.R), ColorNumerics.From8BitTo32Bit(source.G), ColorNumerics.From8BitTo32Bit(source.B), ColorNumerics.From8BitTo32Bit(source.A));

    /// <inheritdoc/>
    public static Rgba128 FromRgb48(Rgb48 source)
        => new(ColorNumerics.From16BitTo32Bit(source.R), ColorNumerics.From16BitTo32Bit(source.G), ColorNumerics.From16BitTo32Bit(source.B), uint.MaxValue);

    /// <inheritdoc/>
    public static Rgba128 FromRgba64(Rgba64 source)
        => new(ColorNumerics.From16BitTo32Bit(source.R), ColorNumerics.From16BitTo32Bit(source.G), ColorNumerics.From16BitTo32Bit(source.B), ColorNumerics.From16BitTo32Bit(source.A));

    /// <inheritdoc/>
    public static PixelTypeInfo GetPixelTypeInfo() => PixelTypeInfo.Create<Rgba128>(
            PixelComponentInfo.Create<Rgba128>(4, 32, 32, 32, 32),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc/>
    public readonly Rgba32 ToRgba32() => Rgba32.FromRgba128(this);

    /// <inheritdoc/>
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override readonly int GetHashCode() => HashCode.Combine(this.R, this.G, this.B, this.A);

    /// <inheritdoc />
    public override readonly string ToString() => FormattableString.Invariant($"Rgba128({this.R}, {this.G}, {this.B}, {this.A})");

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Rgba128 rgb && rgb.R == this.R && rgb.G == this.G && rgb.B == this.B && rgb.A == this.A;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Rgba128 other) => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B) && this.A.Equals(other.A);
}
