// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing three unsigned normalized values.
/// The x and z components use 5 bits, and the y component uses 6 bits.
/// </summary>
/// <remarks>
/// <see cref="ToVector4"/> and scaled vector conversions return color components in <c>[0, 1]</c> with an implicit
/// alpha of <c>1</c>. The storage layout matches <c>DXGI_FORMAT_B5G6R5_UNORM</c>.
/// </remarks>
public partial struct Bgr565 : IPixel<Bgr565>, IPackedVector<ushort>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Bgr565"/> struct.
    /// </summary>
    /// <param name="x">The x-component</param>
    /// <param name="y">The y-component</param>
    /// <param name="z">The z-component</param>
    public Bgr565(float x, float y, float z)
        : this(new Vector3(x, y, z))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Bgr565"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the components for the packed value.</param>
    public Bgr565(Vector3 vector) => this.PackedValue = Pack(vector);

    /// <summary>
    /// Initializes a new instance of the <see cref="Bgr565"/> struct from its packed representation.
    /// </summary>
    /// <remarks>
    /// Bits 0-4 store blue, bits 5-10 store green, and bits 11-15 store red.
    /// This constructor copies the stored bits without numeric conversion, rounding, or clamping.
    /// </remarks>
    /// <param name="packed">The packed component bits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Bgr565(ushort packed)
        : this() => this.PackedValue = packed;

    /// <inheritdoc/>
    public ushort PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="Bgr565"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgr565"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgr565"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Bgr565 left, Bgr565 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Bgr565"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Bgr565"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Bgr565"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Bgr565 left, Bgr565 right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.ToVector3(), 1F);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<Bgr565>(
            PixelComponentInfo.Create<Bgr565>(3, 5, 6, 5),
            PixelColorType.BGR,
            PixelColorRange.Sdr,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<Bgr565> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedScaledVector4() => this.ToScaledVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToUnassociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToAssociatedVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromUnassociatedScaledVector4(Vector4 source) => FromScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromAssociatedScaledVector4(Vector4 source)
        => PixelOperations.FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromUnassociatedVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromAssociatedVector4(Vector4 source) => FromAssociatedScaledVector4(source);

    /// <inheritdoc />
    public static Bgr565 FromAbgr32P(Abgr32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static Bgr565 FromArgb32P(Argb32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static Bgr565 FromBgra32P(Bgra32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public static Bgr565 FromRgba32P(Rgba32P source) => FromAssociatedScaledVector4(source.ToAssociatedScaledVector4());

    /// <inheritdoc />
    public readonly Rgba32P ToRgba32P() => Rgba32P.FromUnassociatedScaledVector4(this.ToUnassociatedScaledVector4());

    /// <inheritdoc />
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromVector4(Vector4 source) => new() { PackedValue = Pack(new Vector3(source.X, source.Y, source.Z)) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <summary>
    /// Expands the packed representation into a <see cref="Vector3"/>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector3"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3 ToVector3() => new(
                   ((this.PackedValue >> 11) & 0x1F) * (1F / 31F),
                   ((this.PackedValue >> 5) & 0x3F) * (1F / 63F),
                   (this.PackedValue & 0x1F) * (1F / 31F));

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is Bgr565 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(Bgr565 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector3 vector = this.ToVector3();
        return FormattableString.Invariant($"Bgr565({vector.Z:#0.##}, {vector.Y:#0.##}, {vector.X:#0.##})");
    }

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Pack(Vector3 vector)
    {
        vector = new Vector3(
            ColorNumerics.ClampToFinite(vector.X, 0F, 1F),
            ColorNumerics.ClampToFinite(vector.Y, 0F, 1F),
            ColorNumerics.ClampToFinite(vector.Z, 0F, 1F));

        return (ushort)((((int)Math.Round(vector.X * 31F) & 0x1F) << 11)
               | (((int)Math.Round(vector.Y * 63F) & 0x3F) << 5)
               | ((int)Math.Round(vector.Z * 31F) & 0x1F));
    }
}
