// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing a single 16-bit normalized luminance value.
/// <para>
/// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
public partial struct L16 : IPixel<L16>, IPackedVector<ushort>
{
    private const float Max = ushort.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="L16"/> struct.
    /// </summary>
    /// <param name="luminance">The luminance component</param>
    public L16(ushort luminance) => this.PackedValue = luminance;

    /// <inheritdoc />
    public ushort PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="L16"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="L16"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="L16"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(L16 left, L16 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="L16"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="L16"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="L16"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(L16 left, L16 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32()
    {
        byte rgb = ColorNumerics.From16BitTo8Bit(this.PackedValue);
        return new Rgba32(rgb, rgb, rgb);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4()
    {
        float scaled = this.PackedValue / Max;
        return new Vector4(scaled, scaled, scaled, 1f);
    }

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<L16>(
            PixelComponentInfo.Create<L16>(1, 16),
            PixelColorType.Luminance,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<L16> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromVector4(Vector4 source) => new() { PackedValue = Pack(source) };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromAbgr32(Abgr32 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromArgb32(Argb32 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromBgr24(Bgr24 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromBgra32(Bgra32 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromL8(L8 source) => new(ColorNumerics.From8BitTo16Bit(source.PackedValue));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromL16(L16 source) => new(source.PackedValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromLa16(La16 source) => new(ColorNumerics.From8BitTo16Bit(source.L));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromLa32(La32 source) => new(source.L);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromRgb24(Rgb24 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromRgba32(Rgba32 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromRgb48(Rgb48 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static L16 FromRgba64(Rgba64 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B));

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is L16 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(L16 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString() => $"L16({this.PackedValue})";

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Pack(Vector4 vector)
    {
        vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
        return ColorNumerics.Get16BitBT709Luminance(vector.X, vector.Y, vector.Z);
    }
}
