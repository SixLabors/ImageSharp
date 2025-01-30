// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing unsigned normalized values ranging from 0 to 1.
/// The x and z components use 5 bits, and the y component uses 6 bits.
/// <para>
/// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Bgr565"/> struct.
/// </remarks>
/// <param name="vector">
/// The vector containing the components for the packed value.
/// </param>
public partial struct Bgr565(Vector3 vector) : IPixel<Bgr565>, IPackedVector<ushort>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Bgr565"/> struct.
    /// </summary>
    /// <param name="x">The x-component</param>
    /// <param name="y">The y-component</param>
    /// <param name="z">The z-component</param>
    public Bgr565(float x, float y, float z)
        : this(new(x, y, z))
    {
    }

    /// <inheritdoc/>
    public ushort PackedValue { get; set; } = Pack(vector);

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
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<Bgr565> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc />
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgr565 FromVector4(Vector4 source) => new() { PackedValue = Pack(new(source.X, source.Y, source.Z)) };

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
        vector = Vector3.Clamp(vector, Vector3.Zero, Vector3.One);

        return (ushort)((((int)Math.Round(vector.X * 31F) & 0x1F) << 11)
               | (((int)Math.Round(vector.Y * 63F) & 0x3F) << 5)
               | ((int)Math.Round(vector.Z * 31F) & 0x1F));
    }
}
