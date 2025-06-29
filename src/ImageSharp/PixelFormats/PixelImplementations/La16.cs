// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing two 8-bit normalized values representing luminance and alpha.
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public partial struct La16 : IPixel<La16>, IPackedVector<ushort>
{
    /// <summary>
    /// The maximum byte value.
    /// </summary>
    private static readonly Vector4 MaxBytes = Vector128.Create(255f).AsVector4();

    /// <summary>
    /// The half vector value.
    /// </summary>
    private static readonly Vector4 Half = Vector128.Create(.5f).AsVector4();

    /// <summary>
    /// Gets or sets the luminance component.
    /// </summary>
    [FieldOffset(0)]
    public byte L;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    [FieldOffset(1)]
    public byte A;

    /// <summary>
    /// Initializes a new instance of the <see cref="La16"/> struct.
    /// </summary>
    /// <param name="l">The luminance component.</param>
    /// <param name="a">The alpha component.</param>
    public La16(byte l, byte a)
    {
        this.L = l;
        this.A = a;
    }

    /// <inheritdoc/>
    public ushort PackedValue
    {
        readonly get => Unsafe.As<La16, ushort>(ref Unsafe.AsRef(in this));
        set => Unsafe.As<La16, ushort>(ref this) = value;
    }

    /// <summary>
    /// Compares two <see cref="La16"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="La16"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="La16"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(La16 left, La16 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="La16"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="La16"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="La16"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(La16 left, La16 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => new(this.L, this.L, this.L, this.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4()
    {
        const float max = 255f;
        float rgb = this.L / max;
        return new Vector4(rgb, rgb, rgb, this.A / max);
    }

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<La16>(
            PixelComponentInfo.Create<La16>(2, 8, 8),
            PixelColorType.Luminance | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc/>
    public static PixelOperations<La16> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromScaledVector4(Vector4 source) => Pack(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromVector4(Vector4 source) => Pack(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromAbgr32(Abgr32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromArgb32(Argb32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), source.A);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromBgr24(Bgr24 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromBgra32(Bgra32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromL16(L16 source) => new(ColorNumerics.From16BitTo8Bit(source.PackedValue), byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromL8(L8 source) => new(source.PackedValue, byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromLa16(La16 source) => new(source.L, source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromLa32(La32 source) => new(ColorNumerics.From16BitTo8Bit(source.L), ColorNumerics.From16BitTo8Bit(source.A));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromRgb24(Rgb24 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), byte.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromRgba32(Rgba32 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La16 FromRgb48(Rgb48 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), byte.MaxValue);

    /// <inheritdoc/>
    public static La16 FromRgba64(Rgba64 source) => new(ColorNumerics.Get8BitBT709Luminance(source.R, source.G, source.B), ColorNumerics.From16BitTo8Bit(source.A));

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is La16 other && this.Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(La16 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString() => $"La16({this.L}, {this.A})";

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static La16 Pack(Vector4 vector)
    {
        vector *= MaxBytes;
        vector += Half;
        vector = Numerics.Clamp(vector, Vector4.Zero, MaxBytes);

        Vector128<byte> result = Vector128.ConvertToInt32(vector.AsVector128()).AsByte();
        byte l = ColorNumerics.Get8BitBT709Luminance(result.GetElement(0), result.GetElement(4), result.GetElement(8));
        byte a = result.GetElement(12);

        return new La16(l, a);
    }
}
