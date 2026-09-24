// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Pixel type containing four associated IEEE 754 binary32 components.
/// Native and scaled vector conversions preserve the stored floating-point values.
/// </summary>
/// <remarks>
/// The component layout is binary-compatible with <c>DXGI_FORMAT_R32G32B32A32_FLOAT</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public partial struct RgbaVectorP : IPixel<RgbaVectorP>
{
    /// <summary>
    /// Gets or sets the first associated color component.
    /// </summary>
    public float R;

    /// <summary>
    /// Gets or sets the second associated color component.
    /// </summary>
    public float G;

    /// <summary>
    /// Gets or sets the third associated color component.
    /// </summary>
    public float B;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    public float A;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaVectorP"/> struct from associated components.
    /// </summary>
    /// <param name="r">The first associated color component.</param>
    /// <param name="g">The second associated color component.</param>
    /// <param name="b">The third associated color component.</param>
    /// <param name="a">The alpha component.</param>
    public RgbaVectorP(float r, float g, float b, float a)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    /// <summary>
    /// Compares two pixels for equality.
    /// </summary>
    /// <param name="left">The left pixel.</param>
    /// <param name="right">The right pixel.</param>
    /// <returns>Whether the components are equal.</returns>
    public static bool operator ==(RgbaVectorP left, RgbaVectorP right) => left.Equals(right);

    /// <summary>
    /// Compares two pixels for inequality.
    /// </summary>
    /// <param name="left">The left pixel.</param>
    /// <param name="right">The right pixel.</param>
    /// <returns>Whether the components differ.</returns>
    public static bool operator !=(RgbaVectorP left, RgbaVectorP right) => !left.Equals(right);

    /// <inheritdoc />
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    public readonly Vector4 ToVector4() => new(this.R, this.G, this.B, this.A);

    /// <inheritdoc />
    public readonly Vector4 ToAssociatedScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    public readonly Vector4 ToAssociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    public readonly Vector4 ToUnassociatedScaledVector4() => this.ToUnassociatedVector4();

    /// <inheritdoc />
    public readonly Vector4 ToUnassociatedVector4()
    {
        Vector4 vector = this.ToVector4();
        Numerics.UnPremultiply(ref vector);
        return vector;
    }

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<RgbaVectorP>(
            PixelComponentInfo.Create<RgbaVectorP>(4, 32, 32, 32, 32),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Associated);

    /// <inheritdoc />
    public static PixelOperations<RgbaVectorP> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    public static RgbaVectorP FromScaledVector4(Vector4 source) => FromAssociatedVector4(source);

    /// <inheritdoc />
    public static RgbaVectorP FromVector4(Vector4 source) => FromAssociatedVector4(source);

    /// <inheritdoc />
    public static RgbaVectorP FromAssociatedScaledVector4(Vector4 source) => FromAssociatedVector4(source);

    /// <inheritdoc />
    public static RgbaVectorP FromAssociatedVector4(Vector4 source) => new(source.X, source.Y, source.Z, source.W);

    /// <inheritdoc />
    public static RgbaVectorP FromUnassociatedScaledVector4(Vector4 source) => FromUnassociatedVector4(source);

    /// <inheritdoc />
    public static RgbaVectorP FromUnassociatedVector4(Vector4 source)
    {
        Numerics.Premultiply(ref source);
        return FromAssociatedVector4(source);
    }

    /// <inheritdoc />
    public static RgbaVectorP FromAbgr32(Abgr32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromArgb32(Argb32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromBgra5551(Bgra5551 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromBgr24(Bgr24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromBgra32(Bgra32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromL8(L8 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromL16(L16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromLa16(La16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromLa32(La32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromRgb24(Rgb24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromRgba32(Rgba32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromRgb48(Rgb48 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaVectorP FromRgba64(Rgba64 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is RgbaVectorP other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(RgbaVectorP other)
        => this.R.Equals(other.R) && this.G.Equals(other.G) && this.B.Equals(other.B) && this.A.Equals(other.A);

    /// <inheritdoc />
    public override readonly int GetHashCode() => HashCode.Combine(this.R, this.G, this.B, this.A);

    /// <inheritdoc />
    public override readonly string ToString()
        => FormattableString.Invariant($"RgbaVectorP({this.R:#0.##}, {this.G:#0.##}, {this.B:#0.##}, {this.A:#0.##})");
}
