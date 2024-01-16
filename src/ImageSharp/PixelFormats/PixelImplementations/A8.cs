// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing a single 8-bit normalized alpha value.
/// <para>
/// Ranges from [0, 0, 0, 0] to [0, 0, 0, 1] in vector form.
/// </para>
/// </summary>
public partial struct A8 : IPixel<A8>, IPackedVector<byte>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="A8"/> struct.
    /// </summary>
    /// <param name="alpha">The alpha component.</param>
    public A8(byte alpha) => this.PackedValue = alpha;

    /// <summary>
    /// Initializes a new instance of the <see cref="A8"/> struct.
    /// </summary>
    /// <param name="alpha">The alpha component.</param>
    public A8(float alpha) => this.PackedValue = Pack(alpha);

    /// <inheritdoc />
    public byte PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="A8"/> objects for equality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="A8"/> on the left side of the operand.
    /// </param>
    /// <param name="right">
    /// The <see cref="A8"/> on the right side of the operand.
    /// </param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(A8 left, A8 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="A8"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="A8"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="A8"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(A8 left, A8 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => new() { A = this.PackedValue };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(0, 0, 0, this.PackedValue / 255f);

    /// <inheritdoc/>
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<A8>(
            PixelComponentInfo.Create<A8>(1, 8),
            PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public readonly PixelOperations<A8> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromVector4(Vector4 source) => new(Pack(source.W));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromArgb32(Argb32 source) => new(source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromBgr24(Bgr24 source) => new(byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromBgra32(Bgra32 source) => new(source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromAbgr32(Abgr32 source) => new(source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromL8(L8 source) => new(byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromL16(L16 source) => new(byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromLa16(La16 source) => new(source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromLa32(La32 source) => new(ColorNumerics.From16BitTo8Bit(source.A));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromRgb24(Rgb24 source) => new(byte.MaxValue);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromRgba32(Rgba32 source) => new(source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromRgb48(Rgb48 source) => new(byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static A8 FromRgba64(Rgba64 source) => new(ColorNumerics.From16BitTo8Bit(source.A));

    /// <summary>
    /// Compares an object with the packed vector.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if the object is equal to the packed vector.</returns>
    public override readonly bool Equals(object? obj) => obj is A8 other && this.Equals(other);

    /// <summary>
    /// Compares another A8 packed vector with the packed vector.
    /// </summary>
    /// <param name="other">The A8 packed vector to compare.</param>
    /// <returns>True if the packed vectors are equal.</returns>
    public readonly bool Equals(A8 other) => this.PackedValue.Equals(other.PackedValue);

    /// <summary>
    /// Gets a string representation of the packed vector.
    /// </summary>
    /// <returns>A string representation of the packed vector.</returns>
    public override readonly string ToString() => $"A8({this.PackedValue})";

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <summary>
    /// Packs a <see cref="float"/> into a byte.
    /// </summary>
    /// <param name="alpha">The float containing the value to pack.</param>
    /// <returns>The <see cref="byte"/> containing the packed values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Pack(float alpha) => (byte)Math.Round(Numerics.Clamp(alpha, 0, 1f) * 255f);
}
