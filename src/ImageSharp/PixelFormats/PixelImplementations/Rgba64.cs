// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four 16-bit unsigned normalized values ranging from 0 to 65535.
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public partial struct Rgba64 : IPixel<Rgba64>, IPackedVector<ulong>
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
    /// Gets or sets the alpha component.
    /// </summary>
    public ushort A;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <param name="a">The alpha component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(ushort r, ushort g, ushort b, ushort a)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="source">A structure of 4 bytes in RGBA byte order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(Rgba32 source)
    {
        this.R = ColorNumerics.From8BitTo16Bit(source.R);
        this.G = ColorNumerics.From8BitTo16Bit(source.G);
        this.B = ColorNumerics.From8BitTo16Bit(source.B);
        this.A = ColorNumerics.From8BitTo16Bit(source.A);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="source">A structure of 4 bytes in BGRA byte order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(Bgra32 source)
    {
        this.R = ColorNumerics.From8BitTo16Bit(source.R);
        this.G = ColorNumerics.From8BitTo16Bit(source.G);
        this.B = ColorNumerics.From8BitTo16Bit(source.B);
        this.A = ColorNumerics.From8BitTo16Bit(source.A);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="source">A structure of 4 bytes in ARGB byte order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(Argb32 source)
    {
        this.R = ColorNumerics.From8BitTo16Bit(source.R);
        this.G = ColorNumerics.From8BitTo16Bit(source.G);
        this.B = ColorNumerics.From8BitTo16Bit(source.B);
        this.A = ColorNumerics.From8BitTo16Bit(source.A);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="source">A structure of 4 bytes in ABGR byte order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(Abgr32 source)
    {
        this.R = ColorNumerics.From8BitTo16Bit(source.R);
        this.G = ColorNumerics.From8BitTo16Bit(source.G);
        this.B = ColorNumerics.From8BitTo16Bit(source.B);
        this.A = ColorNumerics.From8BitTo16Bit(source.A);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="source">A structure of 3 bytes in RGB byte order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(Rgb24 source)
    {
        this.R = ColorNumerics.From8BitTo16Bit(source.R);
        this.G = ColorNumerics.From8BitTo16Bit(source.G);
        this.B = ColorNumerics.From8BitTo16Bit(source.B);
        this.A = ushort.MaxValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="source">A structure of 3 bytes in BGR byte order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(Bgr24 source)
    {
        this.R = ColorNumerics.From8BitTo16Bit(source.R);
        this.G = ColorNumerics.From8BitTo16Bit(source.G);
        this.B = ColorNumerics.From8BitTo16Bit(source.B);
        this.A = ushort.MaxValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba64"/> struct.
    /// </summary>
    /// <param name="vector">The <see cref="Vector4"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Rgba64(Vector4 vector)
    {
        vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
        this.R = (ushort)MathF.Round(vector.X);
        this.G = (ushort)MathF.Round(vector.Y);
        this.B = (ushort)MathF.Round(vector.Z);
        this.A = (ushort)MathF.Round(vector.W);
    }

    /// <summary>
    /// Gets or sets the RGB components of this struct as <see cref="Rgb48"/>.
    /// </summary>
    public Rgb48 Rgb
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => Unsafe.As<Rgba64, Rgb48>(ref Unsafe.AsRef(in this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Unsafe.As<Rgba64, Rgb48>(ref this) = value;
    }

    /// <inheritdoc/>
    public ulong PackedValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => Unsafe.As<Rgba64, ulong>(ref Unsafe.AsRef(in this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Unsafe.As<Rgba64, ulong>(ref this) = value;
    }

    /// <summary>
    /// Compares two <see cref="Rgba64"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgba64"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgba64"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Rgba64 left, Rgba64 right) => left.PackedValue == right.PackedValue;

    /// <summary>
    /// Compares two <see cref="Rgba64"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Rgba64"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Rgba64"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Rgba64 left, Rgba64 right) => left.PackedValue != right.PackedValue;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromRgba64(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new Vector4(this.R, this.G, this.B, this.A) / Max;

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Rgba64>(
            PixelComponentInfo.Create<Rgba64>(4, 16, 16, 16, 16),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<Rgba64> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromVector4(Vector4 source) => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromAbgr32(Abgr32 source) => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromArgb32(Argb32 source) => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromBgr24(Bgr24 source) => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromBgra32(Bgra32 source) => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromL8(L8 source)
    {
        ushort rgb = ColorNumerics.From8BitTo16Bit(source.PackedValue);
        return new Rgba64(rgb, rgb, rgb, ushort.MaxValue);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromL16(L16 source) => new(source.PackedValue, source.PackedValue, source.PackedValue, ushort.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromLa16(La16 source)
    {
        ushort rgb = ColorNumerics.From8BitTo16Bit(source.L);
        return new Rgba64(rgb, rgb, rgb, ColorNumerics.From8BitTo16Bit(source.A));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromLa32(La32 source) => new(source.L, source.L, source.L, source.A);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromRgb24(Rgb24 source) => new(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromRgba32(Rgba32 source) => new(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromRgb48(Rgb48 source) => new(source.R, source.G, source.B, ushort.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rgba64 FromRgba64(Rgba64 source) => new(source.R, source.G, source.B, source.A);

    /// <summary>
    /// Convert to <see cref="Bgra32"/>.
    /// </summary>
    /// <returns>The <see cref="Bgra32"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Bgra32 ToBgra32() => Bgra32.FromRgba64(this);

    /// <summary>
    /// Convert to <see cref="Argb32"/>.
    /// </summary>
    /// <returns>The <see cref="Argb32"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Argb32 ToArgb32() => Argb32.FromRgba64(this);

    /// <summary>
    /// Convert to <see cref="Abgr32"/>.
    /// </summary>
    /// <returns>The <see cref="Abgr32"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Abgr32 ToAbgr32() => Abgr32.FromRgba64(this);

    /// <summary>
    /// Convert to <see cref="Rgb24"/>.
    /// </summary>
    /// <returns>The <see cref="Rgb24"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgb24 ToRgb24() => Rgb24.FromRgba64(this);

    /// <summary>
    /// Convert to <see cref="Bgr24"/>.
    /// </summary>
    /// <returns>The <see cref="Bgr24"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Bgr24 ToBgr24() => Bgr24.FromRgba64(this);

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Rgba64 rgba64 && this.Equals(rgba64);

    /// <inheritdoc />
    public readonly bool Equals(Rgba64 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString() => $"Rgba64({this.R}, {this.G}, {this.B}, {this.A})";

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();
}
