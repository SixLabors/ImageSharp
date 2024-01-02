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
/// <remarks>
/// Initializes a new instance of the <see cref="RgbaDouble"/> struct.
/// </remarks>
/// <param name="r">The red component.</param>
/// <param name="g">The green component.</param>
/// <param name="b">The blue component.</param>
/// <param name="a">The alpha component.</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(InliningOptions.ShortMethod)]
public partial struct RgbaDouble(double r, double g, double b, double a = 1) : IPixel<RgbaDouble>
{
    /// <summary>
    /// Gets or sets the red component.
    /// </summary>
    public double R = r;

    /// <summary>
    /// Gets or sets the green component.
    /// </summary>
    public double G = g;

    /// <summary>
    /// Gets or sets the blue component.
    /// </summary>
    public double B = b;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    public double A = a;

    private const float MaxBytes = byte.MaxValue;
    private static readonly Vector4 Max = new(MaxBytes);
    private static readonly Vector4 Half = new(0.5F);

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
    public static PixelTypeInfo GetPixelTypeInfo() => PixelTypeInfo.Create<RgbaDouble>(PixelComponentInfo.Create<RgbaDouble>(4, 64, 64, 64, 64), PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public readonly PixelOperations<RgbaDouble> CreatePixelOperations() => new();

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromScaledVector4(Vector4 vector) => this.FromVector4(vector);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromVector4(Vector4 vector)
    {
        vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
        this.R = vector.X;
        this.G = vector.Y;
        this.B = vector.Z;
        this.A = vector.W;
    }

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public readonly Vector4 ToVector4() => new((float)this.R, (float)this.G, (float)this.B, (float)this.A);

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromArgb32(Argb32 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromBgr24(Bgr24 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromBgra32(Bgra32 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromAbgr32(Abgr32 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromBgra5551(Bgra5551 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromL8(L8 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromL16(L16 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromLa16(La16 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromLa32(La32 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromRgb24(Rgb24 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromRgba32(Rgba32 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(InliningOptions.ShortMethod)]
    public void ToRgba32(ref Rgba32 dest) => dest.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromRgb48(Rgb48 source) => this.FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void FromRgba64(Rgba64 source) => this.FromScaledVector4(source.ToScaledVector4());

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
