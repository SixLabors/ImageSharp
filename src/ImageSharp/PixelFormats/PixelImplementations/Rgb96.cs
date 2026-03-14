// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Pixel type containing three 32-bit unsigned normalized values ranging from 0 to 4294967295.
/// The color components are stored in red, green, blue.
/// <para>
/// Ranges from [0, 0, 0] to [1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public partial struct Rgb96 : IPixel<Rgb96>
{
    private const float InvMax = 1.0f / uint.MaxValue;

    private const double Max = uint.MaxValue;

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
    /// Initializes a new instance of the <see cref="Rgb96"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgb96(uint r, uint g, uint b)
    {
        this.R = r;
        this.G = g;
        this.B = b;
    }

    /// <summary>
    /// Compares two <see cref="Rgb96"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgb96"/> on the left side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    /// <param name="right">The <see cref="Rgb96"/> on the right side of the operand.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Rgb96 left, Rgb96 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Rgb96"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgb96"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgb96"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Rgb96 left, Rgb96 right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 ToVector4() => new(
            this.R * InvMax,
            this.G * InvMax,
            this.B * InvMax,
            1.0f);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(this.R, this.G, this.B);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Rgb96 other) => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B);

    /// <inheritdoc />
    public override string ToString() => FormattableString.Invariant($"Rgb96({this.R}, {this.G}, {this.B})");

    public static PixelOperations<Rgb96> CreatePixelOperations() => throw new NotImplementedException();

    public static Rgb96 FromScaledVector4(Vector4 source) => throw new NotImplementedException();

    public static Rgb96 FromVector4(Vector4 source) => throw new NotImplementedException();

    public static Rgb96 FromAbgr32(Abgr32 source) => throw new NotImplementedException();

    public static Rgb96 FromArgb32(Argb32 source) => throw new NotImplementedException();

    public static Rgb96 FromBgra5551(Bgra5551 source) => throw new NotImplementedException();

    public static Rgb96 FromBgr24(Bgr24 source) => throw new NotImplementedException();

    public static Rgb96 FromBgra32(Bgra32 source) => throw new NotImplementedException();

    public static Rgb96 FromL8(L8 source) => throw new NotImplementedException();

    public static Rgb96 FromL16(L16 source) => throw new NotImplementedException();

    public static Rgb96 FromLa16(La16 source) => throw new NotImplementedException();

    public static Rgb96 FromLa32(La32 source) => throw new NotImplementedException();

    public static Rgb96 FromRgb24(Rgb24 source) => throw new NotImplementedException();

    public static Rgb96 FromRgba32(Rgba32 source) => throw new NotImplementedException();

    public static Rgb96 FromRgb48(Rgb48 source) => throw new NotImplementedException();

    public static Rgb96 FromRgba64(Rgba64 source) => throw new NotImplementedException();

    public static PixelTypeInfo GetPixelTypeInfo() => throw new NotImplementedException();

    public Rgba32 ToRgba32() => throw new NotImplementedException();
}
