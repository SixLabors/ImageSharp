// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Unpacked pixel type containing four 64-bit floating-point values typically ranging from 0 to 1.
/// The color components are stored in red, green, blue, and alpha order.
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
/// <remarks>
/// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
/// as it avoids the need to create new values for modification operations.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct RgbaDouble : IPixel<RgbaDouble>
{
    /// <summary>
    /// Gets or sets the red component.
    /// </summary>
    public double R;

    /// <summary>
    /// Gets or sets the green component.
    /// </summary>
    public double G;

    /// <summary>
    /// Gets or sets the blue component.
    /// </summary>
    public double B;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    public double A;

    private const float MaxBytes = byte.MaxValue;
    private static readonly Vector4 Max = new(MaxBytes);
    private static readonly Vector4 Half = new(0.5F);

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaVector"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <param name="a">The alpha component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RgbaDouble(double r, double g, double b, double a = 1)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    /// <summary>
    /// Compares two <see cref="RgbaDouble"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="RgbaDouble"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="RgbaDouble"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static bool operator ==(RgbaDouble left, RgbaDouble right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="RgbaDouble"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="RgbaDouble"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="RgbaDouble"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(InliningOptions.ShortMethod)]
    public static bool operator !=(RgbaDouble left, RgbaDouble right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new((float)this.R, (float)this.G, (float)this.B, (float)this.A);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<RgbaDouble>(
            PixelComponentInfo.Create<RgbaDouble>(4, 64, 64, 64, 64),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<RgbaDouble> CreatePixelOperations() => new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromVector4(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);
        return new RgbaDouble(source.X, source.Y, source.Z, source.W);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaDouble FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    public override readonly bool Equals(object obj) => obj is RgbaDouble other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly bool Equals(RgbaDouble other) =>
        this.R.Equals(other.R)
        && this.G.Equals(other.G)
        && this.B.Equals(other.B)
        && this.A.Equals(other.A);

    /// <inheritdoc/>
    public override readonly string ToString() => FormattableString.Invariant($"RgbaDouble({this.R:#0.##}, {this.G:#0.##}, {this.B:#0.##}, {this.A:#0.##})");

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(this.R, this.G, this.B, this.A);
}
