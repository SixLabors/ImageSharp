// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Packed pixel type containing two 16-bit signed normalized values.
/// </summary>
/// <remarks>
/// <see cref="ToVector2"/> and <see cref="ToVector4"/> return components in the native signed-normalized range
/// <c>[-1, 1]</c>. Scaled vector conversions return components in <c>[0, 1]</c>.
/// The packed two's-complement codes <c>-32768</c> and <c>-32767</c> both represent <c>-1</c>,
/// matching <c>DXGI_FORMAT_R16G16_SNORM</c>.
/// </remarks>
public partial struct NormalizedShort2 : IPixel<NormalizedShort2>, IPackedVector<uint>
{
    // Largest two byte positive number 0xFFFF >> 1;
    private const float MaxPos = 0x7FFF;
    private const float ScaledMagnitude = MaxPos * 2F;

    private static readonly Vector2 Max = new(MaxPos);
    private static readonly Vector2 Min = Vector2.Negate(Max);

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedShort2"/> struct.
    /// </summary>
    /// <param name="x">The x-component.</param>
    /// <param name="y">The y-component.</param>
    public NormalizedShort2(float x, float y)
        : this(new Vector2(x, y))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedShort2"/> struct.
    /// </summary>
    /// <param name="vector">The vector containing the component values.</param>
    public NormalizedShort2(Vector2 vector) => this.PackedValue = Pack(vector);

    /// <inheritdoc/>
    public uint PackedValue { get; set; }

    /// <summary>
    /// Compares two <see cref="NormalizedShort2"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="NormalizedShort2"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="NormalizedShort2"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(NormalizedShort2 left, NormalizedShort2 right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="NormalizedShort2"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="NormalizedShort2"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="NormalizedShort2"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(NormalizedShort2 left, NormalizedShort2 right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4()
    {
        Vector2 scaled = new(
            (short)(this.PackedValue & 0xFFFF),
            (short)(this.PackedValue >> 0x10));

        // SNORM reserves both minimum two's-complement codes for -1. Clamp before offsetting so raw -32768 cannot escape the scaled range.
        scaled = Vector2.Max(scaled, Min);
        scaled += Max;
        scaled /= ScaledMagnitude;
        return new Vector4(scaled, 0f, 1f);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.ToVector2(), 0f, 1f);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<NormalizedShort2>(
            PixelComponentInfo.Create<NormalizedShort2>(2, 16, 16),
            PixelColorType.Red | PixelColorType.Green,
            PixelAlphaRepresentation.None);

    /// <inheritdoc />
    public static PixelOperations<NormalizedShort2> CreatePixelOperations() => new PixelOperations();

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
    public static NormalizedShort2 FromUnassociatedScaledVector4(Vector4 source) => FromScaledVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromAssociatedScaledVector4(Vector4 source)
    {
        // The destination has implicit alpha one, but associated input must be restored before its alpha is discarded.
        Numerics.UnPremultiply(ref source);
        return FromScaledVector4(source);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromUnassociatedVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromAssociatedVector4(Vector4 source)
    {
        // Only the stored color components use the native [-1, 1] encoding; W remains the normalized source alpha.
        source.X = (source.X + 1F) / 2F;
        source.Y = (source.Y + 1F) / 2F;
        return FromAssociatedScaledVector4(source);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromScaledVector4(Vector4 source)
    {
        Vector2 scaled = new Vector2(source.X, source.Y) * 2f;
        scaled -= Vector2.One;
        return new NormalizedShort2 { PackedValue = Pack(scaled) };
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromVector4(Vector4 source) => new() { PackedValue = Pack(new Vector2(source.X, source.Y)) };

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizedShort2 FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <summary>
    /// Expands the packed representation into a <see cref="Vector2"/>.
    /// The vector components are typically expanded in least to greatest significance order.
    /// </summary>
    /// <returns>The <see cref="Vector2"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2 ToVector2()
    {
        Vector2 vector = new(
            (short)(this.PackedValue & 0xFFFF),
            (short)(this.PackedValue >> 0x10));

        // DirectX SNORM maps both -32768 and -32767 to -1.
        return Vector2.Max(vector, Min) / MaxPos;
    }

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is NormalizedShort2 other && this.Equals(other);

    /// <inheritdoc />
    public readonly bool Equals(NormalizedShort2 other) => this.PackedValue.Equals(other.PackedValue);

    /// <inheritdoc />
    public override readonly int GetHashCode() => this.PackedValue.GetHashCode();

    /// <inheritdoc />
    public override readonly string ToString()
    {
        Vector2 vector = this.ToVector2();
        return FormattableString.Invariant($"NormalizedShort2({vector.X:#0.##}, {vector.Y:#0.##})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(Vector2 vector)
    {
        vector *= Max;
        vector = Vector2.Clamp(vector, Min, Max);

        // Round rather than truncate.
        uint word2 = (uint)((int)MathF.Round(vector.X) & 0xFFFF);
        uint word1 = (uint)(((int)MathF.Round(vector.Y) & 0xFFFF) << 0x10);

        return word2 | word1;
    }
}
