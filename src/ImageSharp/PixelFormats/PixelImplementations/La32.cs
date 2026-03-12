// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing two 16-bit normalized values representing luminance and alpha.
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public partial struct La32 : IPixel<La32>, IPackedVector<uint>
{
    private const float Max = ushort.MaxValue;

    /// <summary>
    /// Gets or sets the luminance component.
    /// </summary>
    [FieldOffset(0)]
    public ushort L;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    [FieldOffset(2)]
    public ushort A;

    /// <summary>
    /// Initializes a new instance of the <see cref="La32"/> struct.
    /// </summary>
    /// <param name="l">The luminance component.</param>
    /// <param name="a">The alpha component.</param>
    public La32(ushort l, ushort a)
    {
        this.L = l;
        this.A = a;
    }

    /// <inheritdoc/>
    public uint PackedValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => Unsafe.As<La32, uint>(ref Unsafe.AsRef(in this));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => Unsafe.As<La32, uint>(ref this) = value;
    }

    /// <summary>
    /// Compares two <see cref="La32"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="La32"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="La32"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(La32 left, La32 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="La32"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="La32"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="La32"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(La32 left, La32 right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32()
    {
        byte rgb = ColorNumerics.From16BitTo8Bit(this.L);
        return new Rgba32(rgb, rgb, rgb, ColorNumerics.From16BitTo8Bit(this.A));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4()
    {
        float rgb = this.L / Max;
        return new Vector4(rgb, rgb, rgb, this.A / Max);
    }

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<La32>(
            PixelComponentInfo.Create<La32>(2, 16, 16),
            PixelColorType.Luminance | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc/>
    public static PixelOperations<La32> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromScaledVector4(Vector4 source) => Pack(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromVector4(Vector4 source) => Pack(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromAbgr32(Abgr32 source)
    {
        ushort l = ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B);
        ushort a = ColorNumerics.From8BitTo16Bit(source.A);
        return new La32(l, a);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromArgb32(Argb32 source)
    {
        ushort l = ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B);
        ushort a = ColorNumerics.From8BitTo16Bit(source.A);
        return new La32(l, a);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromBgr24(Bgr24 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B), ushort.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromBgra32(Bgra32 source)
    {
        ushort l = ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B);
        ushort a = ColorNumerics.From8BitTo16Bit(source.A);
        return new La32(l, a);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromL16(L16 source) => new(source.PackedValue, ushort.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromL8(L8 source) => new(ColorNumerics.From8BitTo16Bit(source.PackedValue), ushort.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromLa16(La16 source)
    {
        ushort l = ColorNumerics.From8BitTo16Bit(source.L);
        ushort a = ColorNumerics.From8BitTo16Bit(source.A);
        return new La32(l, a);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromLa32(La32 source) => new(source.L, source.A);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromRgb24(Rgb24 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B), ushort.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromRgb48(Rgb48 source)
    {
        ushort l = ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B);
        return new La32(l, ushort.MaxValue);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromRgba32(Rgba32 source)
    {
        ushort l = ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B);
        ushort a = ColorNumerics.From8BitTo16Bit(source.A);
        return new La32(l, a);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static La32 FromRgba64(Rgba64 source) => new(ColorNumerics.Get16BitBT709Luminance(source.R, source.G, source.B), source.A);

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is La32 other && this.Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(La32 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly string ToString() => $"La32({this.L}, {this.A})";

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static La32 Pack(Vector4 vector)
    {
        vector = Numerics.Clamp(vector, Vector4.Zero, Vector4.One) * Max;
        ushort l = ColorNumerics.Get16BitBT709Luminance(vector.X, vector.Y, vector.Z);
        ushort a = (ushort)MathF.Round(vector.W);
        return new La32(l, a);
    }
}
