// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
/// The color components are stored in blue, green, red, and alpha order (least significant to most significant byte).
/// The format is binary compatible with System.Drawing.Imaging.PixelFormat.Format32bppArgb
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public partial struct Bgra32 : IPixel<Bgra32>, IPackedVector<uint>
{
    /// <summary>
    /// Gets or sets the blue component.
    /// </summary>
    public byte B;

    /// <summary>
    /// Gets or sets the green component.
    /// </summary>
    public byte G;

    /// <summary>
    /// Gets or sets the red component.
    /// </summary>
    public byte R;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    public byte A;

    private static readonly Vector4 MaxBytes = Vector128.Create(255f).AsVector4();
    private static readonly Vector4 Half = Vector128.Create(.5f).AsVector4();

    /// <summary>
    /// Initializes a new instance of the <see cref="Bgra32"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bgra32(byte r, byte g, byte b)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = byte.MaxValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Bgra32"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <param name="a">The alpha component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bgra32(byte r, byte g, byte b, byte a)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    /// <summary>
    /// Gets or sets the packed representation of the Bgra32 struct.
    /// </summary>
    public uint Bgra
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => Unsafe.As<Bgra32, uint>(ref Unsafe.AsRef(in this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Unsafe.As<Bgra32, uint>(ref this) = value;
    }

    /// <inheritdoc/>
    public uint PackedValue
    {
        readonly get => this.Bgra;
        set => this.Bgra = value;
    }

    /// <summary>
    /// Compares two <see cref="Bgra32"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgra32"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgra32"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Bgra32 left, Bgra32 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Bgra32"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgra32"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgra32"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Bgra32 left, Bgra32 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromBgra32(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Bgra32>(
            PixelComponentInfo.Create<Bgra32>(4, 8, 8, 8, 8),
            PixelColorType.BGR | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc/>
    public static PixelOperations<Bgra32> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromVector4(Vector4 source) => Pack(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromAbgr32(Abgr32 source) => new(source.R, source.G, source.B, source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromArgb32(Argb32 source) => new(source.R, source.G, source.B, source.A);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromBgr24(Bgr24 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromBgra32(Bgra32 source) => new() { PackedValue = source.PackedValue };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromL8(L8 source) => new(source.PackedValue, source.PackedValue, source.PackedValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromL16(L16 source)
    {
        byte rgb = ColorNumerics.From16BitTo8Bit(source.PackedValue);
        return new Bgra32(rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromLa16(La16 source) => new(source.L, source.L, source.L, source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromLa32(La32 source)
    {
        byte rgb = ColorNumerics.From16BitTo8Bit(source.L);
        return new Bgra32(rgb, rgb, rgb, ColorNumerics.From16BitTo8Bit(source.A));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromRgb24(Rgb24 source) => new(source.R, source.G, source.B);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromRgba32(Rgba32 source) => new(source.R, source.G, source.B, source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromRgb48(Rgb48 source)
        => new()
        {
            R = ColorNumerics.From16BitTo8Bit(source.R),
            G = ColorNumerics.From16BitTo8Bit(source.G),
            B = ColorNumerics.From16BitTo8Bit(source.B),
            A = byte.MaxValue
        };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra32 FromRgba64(Rgba64 source)
        => new()
        {
            R = ColorNumerics.From16BitTo8Bit(source.R),
            G = ColorNumerics.From16BitTo8Bit(source.G),
            B = ColorNumerics.From16BitTo8Bit(source.B),
            A = ColorNumerics.From16BitTo8Bit(source.A)
        };

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Bgra32 other && this.Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(Bgra32 other) => this.Bgra.Equals(other.Bgra);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => this.Bgra.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString() => $"Bgra32({this.B}, {this.G}, {this.R}, {this.A})";

    /// <summary>
    /// Packs a <see cref="Vector4"/> into a color.
    /// </summary>
    /// <param name="vector">The vector containing the values to pack.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Bgra32 Pack(Vector4 vector)
    {
        vector *= MaxBytes;
        vector += Half;
        vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);

        Vector128<byte> result = Vector128.ConvertToInt32(vector.AsVector128()).AsByte();
        return new Bgra32(result.GetElement(0), result.GetElement(4), result.GetElement(8), result.GetElement(12));
    }
}
