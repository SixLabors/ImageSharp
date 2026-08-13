// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four 16-bit floating-point values typically ranging from 0 to 1.
/// The color components are stored in red, green, blue, and alpha order.
/// </summary>
/// <remarks>
/// <see cref="ToVector4"/> and scaled vector conversions return the same component values in the nominal color range
/// <c>[0, 1]</c>. The packed representation is binary-compatible with <c>DXGI_FORMAT_R16G16B16A16_FLOAT</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public partial struct RgbaHalf : IPixel<RgbaHalf>, IPackedVector<ulong>
{
    /// <summary>
    /// Gets or sets the red component.
    /// </summary>
    public Half R;

    /// <summary>
    /// Gets or sets the green component.
    /// </summary>
    public Half G;

    /// <summary>
    /// Gets or sets the blue component.
    /// </summary>
    public Half B;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    public Half A;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaHalf"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    public RgbaHalf(float r, float g, float b)
        : this(r, g, b, 1F)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaHalf"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <param name="a">The alpha component.</param>
    public RgbaHalf(float r, float g, float b, float a)
    {
        this.R = (Half)r;
        this.G = (Half)g;
        this.B = (Half)b;
        this.A = (Half)a;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaHalf"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the component values.</param>
    public RgbaHalf(Vector4 vector)
        : this(vector.X, vector.Y, vector.Z, vector.W)
    {
    }

    /// <inheritdoc />
    public ulong PackedValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => Unsafe.As<RgbaHalf, ulong>(ref Unsafe.AsRef(in this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Unsafe.As<RgbaHalf, ulong>(ref this) = value;
    }

    /// <summary>
    /// Compares two <see cref="RgbaHalf"/> values for equality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(RgbaHalf left, RgbaHalf right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="RgbaHalf"/> values for inequality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are not equal.</returns>
    public static bool operator !=(RgbaHalf left, RgbaHalf right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new((float)this.R, (float)this.G, (float)this.B, (float)this.A);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<RgbaHalf>(
            PixelComponentInfo.Create<RgbaHalf>(4, 16, 16, 16, 16),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<RgbaHalf> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4()
    {
        Vector4 vector = this.ToScaledVector4();
        Numerics.Premultiply(ref vector);
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToAssociatedScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalf FromUnassociatedScaledVector4(Vector4 source) => FromScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalf FromAssociatedScaledVector4(Vector4 source)
    {
        Numerics.UnPremultiply(ref source);
        return FromScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalf FromUnassociatedVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalf FromAssociatedVector4(Vector4 source) => FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalf FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalf FromVector4(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);
        return new RgbaHalf(source);
    }

    /// <inheritdoc />
    public static RgbaHalf FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalf FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is RgbaHalf other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(RgbaHalf other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString() => FormattableString.Invariant($"RgbaHalf({(float)this.R:#0.##}, {(float)this.G:#0.##}, {(float)this.B:#0.##}, {(float)this.A:#0.##})");
}
