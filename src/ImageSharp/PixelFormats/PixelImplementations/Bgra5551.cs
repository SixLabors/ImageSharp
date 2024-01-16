// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing unsigned normalized values ranging from 0 to 1.
/// The x , y and z components use 5 bits, and the w component uses 1 bit.
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="Bgra5551"/> struct.
/// </remarks>
/// <param name="vector">
/// The vector containing the components for the packed vector.
/// </param>
public partial struct Bgra5551(Vector4 vector) : IPixel<Bgra5551>, IPackedVector<ushort>
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

    /// <inheritdoc/>
    public ushort PackedValue { get; set; } = Pack(vector);

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
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public readonly PixelOperations<Bgra5551> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromVector4(Vector4 source) => new() { PackedValue = Pack(source) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Bgra5551 FromBgra5551(Bgra5551 source) => new() { PackedValue = source.PackedValue };

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Pack(Vector4 vector)
    {
        vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One);
        return (ushort)(
               (((int)Math.Round(vector.X * 31F) & 0x1F) << 10)
               | (((int)Math.Round(vector.Y * 31F) & 0x1F) << 5)
               | (((int)Math.Round(vector.Z * 31F) & 0x1F) << 0)
               | (((int)Math.Round(vector.W) & 0x1) << 15));
    }
}
