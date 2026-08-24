// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing four associated 16-bit floating-point values typically ranging from 0 to 1.
/// The color components are stored in red, green, blue, and alpha order.
/// </summary>
/// <remarks>
/// <see cref="ToVector4"/> and scaled vector conversions return the same associated component values in the nominal
/// color range <c>[0, 1]</c>. The packed representation is binary-compatible with
/// <c>DXGI_FORMAT_R16G16B16A16_FLOAT</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public partial struct RgbaHalfP : IPixel<RgbaHalfP>, IPackedVector<ulong>
{
    /// <summary>
    /// Gets or sets the associated red component.
    /// </summary>
    public Half R;

    /// <summary>
    /// Gets or sets the associated green component.
    /// </summary>
    public Half G;

    /// <summary>
    /// Gets or sets the associated blue component.
    /// </summary>
    public Half B;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    public Half A;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaHalfP"/> struct from associated components.
    /// </summary>
    /// <param name="r">The associated red component.</param>
    /// <param name="g">The associated green component.</param>
    /// <param name="b">The associated blue component.</param>
    public RgbaHalfP(float r, float g, float b)
        : this(r, g, b, 1F)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaHalfP"/> struct from associated components.
    /// </summary>
    /// <param name="r">The associated red component.</param>
    /// <param name="g">The associated green component.</param>
    /// <param name="b">The associated blue component.</param>
    /// <param name="a">The alpha component.</param>
    public RgbaHalfP(float r, float g, float b, float a)
    {
        this.R = (Half)r;
        this.G = (Half)g;
        this.B = (Half)b;
        this.A = (Half)a;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaHalfP"/> struct from an associated vector.
    /// </summary>
    /// <param name="vector">The associated vector.</param>
    public RgbaHalfP(Vector4 vector)
        : this() => this = FromAssociatedScaledVector4(vector);

    /// <inheritdoc />
    public ulong PackedValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => Unsafe.As<RgbaHalfP, ulong>(ref Unsafe.AsRef(in this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Unsafe.As<RgbaHalfP, ulong>(ref this) = value;
    }

    /// <summary>
    /// Compares two <see cref="RgbaHalfP"/> values for equality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(RgbaHalfP left, RgbaHalfP right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="RgbaHalfP"/> values for inequality.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> when the values are not equal.</returns>
    public static bool operator !=(RgbaHalfP left, RgbaHalfP right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new((float)this.R, (float)this.G, (float)this.B, (float)this.A);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4()
    {
        Vector4 vector = this.ToScaledVector4();
        Numerics.UnPremultiply(ref vector);
        return vector;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4() => this.ToUnassociatedScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<RgbaHalfP>(
            PixelComponentInfo.Create<RgbaHalfP>(4, 16, 16, 16, 16),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Associated);

    /// <inheritdoc />
    public static PixelOperations<RgbaHalfP> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalfP FromScaledVector4(Vector4 source) => FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalfP FromVector4(Vector4 source) => FromAssociatedVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalfP FromUnassociatedScaledVector4(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);

        // RGB must be associated with the alpha value that binary16 storage can reproduce, not the higher-precision input alpha.
        source.W = HalfTypeHelper.Unpack(HalfTypeHelper.Pack(source.W));
        Numerics.Premultiply(ref source);
        return new RgbaHalfP(source.X, source.Y, source.Z, source.W);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalfP FromAssociatedScaledVector4(Vector4 source)
    {
        float alpha = source.W;

        if (alpha <= 0F)
        {
            return default;
        }

        float storedAlpha = HalfTypeHelper.Unpack(HalfTypeHelper.Pack(Numerics.Clamp(alpha, 0F, 1F)));

        // Preserve the represented straight color when binary16 rounds alpha, then restore the associated RGB <= alpha invariant.
        source *= storedAlpha / alpha;
        source.W = storedAlpha;
        Numerics.ClampRgbToAlpha(ref source);
        return new RgbaHalfP(source.X, source.Y, source.Z, source.W);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalfP FromUnassociatedVector4(Vector4 source) => FromUnassociatedScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaHalfP FromAssociatedVector4(Vector4 source) => FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    public static RgbaHalfP FromAbgr32(Abgr32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromArgb32(Argb32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromBgra5551(Bgra5551 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromBgr24(Bgr24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromBgra32(Bgra32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromL8(L8 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromL16(L16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromLa16(La16 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromLa32(La32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromRgb24(Rgb24 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromRgba32(Rgba32 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromRgb48(Rgb48 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public static RgbaHalfP FromRgba64(Rgba64 source) => FromUnassociatedScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is RgbaHalfP other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(RgbaHalfP other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString() => FormattableString.Invariant($"RgbaHalfP({(float)this.R:#0.##}, {(float)this.G:#0.##}, {(float)this.B:#0.##}, {(float)this.A:#0.##})");
}
