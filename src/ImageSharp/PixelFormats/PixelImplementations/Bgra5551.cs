// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four unsigned normalized values.
/// The x, y, and z components use 5 bits, and the w component uses 1 bit.
/// </summary>
/// <remarks>
/// <see cref="ToVector4"/> and scaled vector conversions return all components in <c>[0, 1]</c>. The storage layout matches
/// <c>DXGI_FORMAT_B5G5R5A1_UNORM</c>.
/// </remarks>
public partial struct Bgra5551 : IPixel<Bgra5551>, IPackedVector<ushort>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Bgra5551"/> struct.
    /// </summary>
    /// <param name="x">The x-component</param>
    /// <param name="y">The y-component</param>
    /// <param name="z">The z-component</param>
    /// <param name="w">The w-component</param>
    public Bgra5551(float x, float y, float z, float w)
        : this(new Vector4(x, y, z, w))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Bgra5551"/> struct.
    /// </summary>
    /// <param name="vector">
    /// The vector containing the components for the packed vector.
    /// </param>
    public Bgra5551(Vector4 vector) => this.PackedValue = Pack(vector);

    /// <summary>
    /// Initializes a new instance of the <see cref="Bgra5551"/> struct from its packed representation.
    /// </summary>
    /// <remarks>
    /// Bits 0-4 store blue, bits 5-9 store green, bits 10-14 store red, and bit 15 stores alpha.
    /// This constructor copies the stored bits without numeric conversion, rounding, or clamping.
    /// </remarks>
    /// <param name="packed">The packed component bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bgra5551(ushort packed)
        : this() => this.PackedValue = packed;

    /// <inheritdoc/>
    public ushort PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="Bgra5551"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgra5551"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgra5551"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Bgra5551 left, Bgra5551 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Bgra5551"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgra5551"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgra5551"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Bgra5551 left, Bgra5551 right) => !left.Equals(right);

    /// <inheritdoc />
    public static Bgra5551 FromAbgr32P(Abgr32P source) => FromUnassociatedScaledVector4(source.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    public static Bgra5551 FromArgb32P(Argb32P source) => FromUnassociatedScaledVector4(source.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    public static Bgra5551 FromBgra32P(Bgra32P source) => FromUnassociatedScaledVector4(source.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    public static Bgra5551 FromRgba32P(Rgba32P source) => FromUnassociatedScaledVector4(source.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    public readonly Rgba32P ToRgba32P() => Rgba32P.FromUnassociatedScaledVector4(this.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32()
    {
        Unpack8(this.PackedValue, out uint r, out uint g, out uint b, out uint a);
        return new Rgba32((byte)r, (byte)g, (byte)b, (byte)a);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(
                    ((this.PackedValue >> 10) & 0x1F) / 31F,
                    ((this.PackedValue >> 5) & 0x1F) / 31F,
                    ((this.PackedValue >> 0) & 0x1F) / 31F,
                    (this.PackedValue >> 15) & 0x01);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Bgra5551>(
            PixelComponentInfo.Create<Bgra5551>(4, 5, 5, 5, 1),
            PixelColorType.BGR | PixelColorType.Alpha,
            PixelColorRange.Sdr,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<Bgra5551> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4() => PixelOperations.ToAssociatedScaledVector4(this);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToAssociatedScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromUnassociatedScaledVector4(Vector4 source) => FromScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromAssociatedScaledVector4(Vector4 source)
        => PixelOperations.FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromUnassociatedVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromAssociatedVector4(Vector4 source) => FromAssociatedScaledVector4(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromVector4(Vector4 source) => new() { PackedValue = Pack(source) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromAbgr32(Abgr32 source) => new(Pack8(source.R, source.G, source.B, source.A));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromArgb32(Argb32 source) => new(Pack8(source.R, source.G, source.B, source.A));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromBgra5551(Bgra5551 source) => source;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromBgr24(Bgr24 source) => new(Pack8(source.R, source.G, source.B, byte.MaxValue));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromBgra32(Bgra32 source) => new(Pack8(source.R, source.G, source.B, source.A));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromL8(L8 source) => new(Pack8(source.PackedValue, source.PackedValue, source.PackedValue, byte.MaxValue));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromL16(L16 source) => new(Pack16(source.PackedValue, source.PackedValue, source.PackedValue, ushort.MaxValue));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromLa16(La16 source) => new(Pack8(source.L, source.L, source.L, source.A));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromLa32(La32 source) => new(Pack16(source.L, source.L, source.L, source.A));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromRgb24(Rgb24 source) => new(Pack8(source.R, source.G, source.B, byte.MaxValue));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromRgba32(Rgba32 source) => new(Pack8(source.R, source.G, source.B, source.A));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromRgb48(Rgb48 source) => new(Pack16(source.R, source.G, source.B, ushort.MaxValue));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromRgba64(Rgba64 source) => new(Pack16(source.R, source.G, source.B, source.A));

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Bgra5551 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(Bgra5551 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector4 vector = this.ToVector4();
        return FormattableString.Invariant($"Bgra5551({vector.Z:#0.##}, {vector.Y:#0.##}, {vector.X:#0.##}, {vector.W:#0.##})");
    }

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <summary>
    /// Packs 8-bit channel values to the 5-5-5-1 layout.
    /// </summary>
    /// <remarks>
    /// The Direct3D UNORM rules give each color as <c>round(c * 31 / 255)</c> and alpha as <c>round(a / 255)</c>, the same
    /// values as <see cref="Pack(Vector4)"/>. <c>c * 31 / 255</c> is never a half, because <c>62 * c</c> is even and
    /// <c>255 * (2k + 1)</c> is odd. Thus <c>(c * 31 + 127) / 255</c> is the rounded value. <c>a &gt;&gt; 7</c> is 1 for
    /// 128 or more.
    /// </remarks>
    /// <param name="r">The red value.</param>
    /// <param name="g">The green value.</param>
    /// <param name="b">The blue value.</param>
    /// <param name="a">The alpha value.</param>
    /// <returns>The packed value: blue in bits 0..4, green in bits 5..9, red in bits 10..14 and alpha in bit 15.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Pack8(uint r, uint g, uint b, uint a)
        => (ushort)((Scale8To5(r) << 10) | (Scale8To5(g) << 5) | Scale8To5(b) | ((a >> 7) << 15));

    /// <summary>
    /// Packs 16-bit channel values to the 5-5-5-1 layout.
    /// </summary>
    /// <remarks>
    /// Each color is <c>round(c * 31 / 65535)</c> and alpha is <c>round(a / 65535)</c>, as in
    /// <see cref="Pack8(uint, uint, uint, uint)"/>. <c>62 * c</c> is even and <c>65535 * (2k + 1)</c> is odd, so
    /// <c>(c * 31 + 32767) / 65535</c> is the rounded value. <c>a &gt;&gt; 15</c> is 1 for 32768 or more.
    /// </remarks>
    /// <param name="r">The red value.</param>
    /// <param name="g">The green value.</param>
    /// <param name="b">The blue value.</param>
    /// <param name="a">The alpha value.</param>
    /// <returns>The packed value: blue in bits 0..4, green in bits 5..9, red in bits 10..14 and alpha in bit 15.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Pack16(uint r, uint g, uint b, uint a)
        => (ushort)((Scale16To5(r) << 10) | (Scale16To5(g) << 5) | Scale16To5(b) | ((a >> 15) << 15));

    /// <summary>
    /// Unpacks the 5-5-5-1 layout to 8-bit channel values.
    /// </summary>
    /// <remarks>
    /// The Direct3D UNORM rules give each color as <c>round(c * 255 / 31)</c> and alpha as 0 or 255, the byte values of
    /// <see cref="ToVector4"/>. <c>c * 255 / 31</c> is never a half, because <c>510 * c</c> is even and
    /// <c>31 * (2k + 1)</c> is odd. Thus <c>(c * 255 + 15) / 31</c> is the rounded value.
    /// </remarks>
    /// <param name="packed">The packed value: blue in bits 0..4, green in bits 5..9, red in bits 10..14 and alpha in bit 15.</param>
    /// <param name="r">The red value.</param>
    /// <param name="g">The green value.</param>
    /// <param name="b">The blue value.</param>
    /// <param name="a">The alpha value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Unpack8(uint packed, out uint r, out uint g, out uint b, out uint a)
    {
        r = Scale5To8((packed >> 10) & 0x1F);
        g = Scale5To8((packed >> 5) & 0x1F);
        b = Scale5To8(packed & 0x1F);
        a = ((packed >> 15) & 1) * 255;
    }

    /// <summary>
    /// Scales an 8-bit value to a 5-bit value: <c>round(value * 31 / 255)</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Pack8(uint, uint, uint, uint)"/> explains the rounding.
    /// </remarks>
    /// <param name="value">The 8-bit value.</param>
    /// <returns>The 5-bit value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Scale8To5(uint value) => ((value * 31) + 127) / 255;

    /// <summary>
    /// Scales a 16-bit value to a 5-bit value: <c>round(value * 31 / 65535)</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Pack16(uint, uint, uint, uint)"/> explains the rounding.
    /// </remarks>
    /// <param name="value">The 16-bit value.</param>
    /// <returns>The 5-bit value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Scale16To5(uint value) => ((value * 31) + 32767) / 65535;

    /// <summary>
    /// Scales a 5-bit value to an 8-bit value: <c>round(value * 255 / 31)</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Unpack8(uint, out uint, out uint, out uint, out uint)"/> explains the rounding.
    /// </remarks>
    /// <param name="value">The 5-bit value.</param>
    /// <returns>The 8-bit value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Scale5To8(uint value) => ((value * 255) + 15) / 31;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Pack(Vector4 vector)
    {
        vector = ColorNumerics.ClampToFinite(vector, Vector4.Zero, Vector4.One);
        return (ushort)(
               (((int)Math.Round(vector.X * 31F) & 0x1F) << 10)
               | (((int)Math.Round(vector.Y * 31F) & 0x1F) << 5)
               | (((int)Math.Round(vector.Z * 31F) & 0x1F) << 0)
               | (((int)Math.Round(vector.W) & 0x1) << 15));
    }
}
