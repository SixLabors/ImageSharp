// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Unpacked pixel type containing four 32-bit floating-point values typically ranging from 0 to 1.
/// The color components are stored in red, green, blue, and alpha order.
/// <para>
/// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
/// </para>
/// </summary>
/// <remarks>
/// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
/// as it avoids the need to create new values for modification operations.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public partial struct RgbaVector : IPixel<RgbaVector>
{
    /// <summary>
    /// Gets or sets the red component.
    /// </summary>
    public float R;

    /// <summary>
    /// Gets or sets the green component.
    /// </summary>
    public float G;

    /// <summary>
    /// Gets or sets the blue component.
    /// </summary>
    public float B;

    /// <summary>
    /// Gets or sets the alpha component.
    /// </summary>
    public float A;

    private const float MaxBytes = byte.MaxValue;
    private static readonly Vector4 Max = new(MaxBytes);
    private static readonly Vector4 Half = new(0.5F);

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaVector"/> struct.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <param name="a">The alpha component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RgbaVector(float r, float g, float b, float a = 1)
    {
        this.R = r;
        this.G = g;
        this.B = b;
        this.A = a;
    }

    /// <summary>
    /// Compares two <see cref="RgbaVector"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="RgbaVector"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="RgbaVector"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(RgbaVector left, RgbaVector right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="RgbaVector"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="RgbaVector"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="RgbaVector"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(RgbaVector left, RgbaVector right) => !left.Equals(right);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Rgba32 ToRgba32() => Rgba32.FromScaledVector4(this.ToScaledVector4());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToScaledVector4() => this.ToVector4();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector4 ToVector4() => new(this.R, this.G, this.B, this.A);

    /// <inheritdoc />
    public static PixelTypeInfo GetPixelTypeInfo()
        => PixelTypeInfo.Create<RgbaVector>(
            PixelComponentInfo.Create<RgbaVector>(4, 32, 32, 32, 32),
            PixelColorType.RGB | PixelColorType.Alpha,
            PixelAlphaRepresentation.Unassociated);

    /// <inheritdoc />
    public static PixelOperations<RgbaVector> CreatePixelOperations() => new PixelOperations();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromScaledVector4(Vector4 source) => FromVector4(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromVector4(Vector4 source)
    {
        source = Numerics.Clamp(source, Vector4.Zero, Vector4.One);
        return new RgbaVector(source.X, source.Y, source.Z, source.W);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromAbgr32(Abgr32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromArgb32(Argb32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromBgra5551(Bgra5551 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromBgr24(Bgr24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromBgra32(Bgra32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromL8(L8 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromL16(L16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromLa16(La16 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromLa32(La32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromRgb24(Rgb24 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromRgba32(Rgba32 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromRgb48(Rgb48 source) => FromScaledVector4(source.ToScaledVector4());

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RgbaVector FromRgba64(Rgba64 source) => FromScaledVector4(source.ToScaledVector4());

    /// <summary>
    /// Creates a new instance of the <see cref="RgbaVector"/> struct.
    /// </summary>
    /// <param name="hex">
    /// The hexadecimal representation of the combined color components arranged
    /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
    /// </param>
    /// <returns>
    /// The <see cref="RgbaVector"/>.
    /// </returns>
    public static RgbaVector FromHex(string hex) => Color.ParseHex(hex).ToPixel<RgbaVector>();

    /// <summary>
    /// Converts the value of this instance to a hexadecimal string.
    /// </summary>
    /// <returns>A hexadecimal string representation of the value.</returns>
    public readonly string ToHex()
    {
        // Hex is RRGGBBAA
        Vector4 vector = this.ToVector4() * Max;
        vector += Half;
        uint hexOrder = (uint)((byte)vector.W | ((byte)vector.Z << 8) | ((byte)vector.Y << 16) | ((byte)vector.X << 24));
        return hexOrder.ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is RgbaVector other && this.Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(RgbaVector other) =>
        this.R.Equals(other.R)
        && this.G.Equals(other.G)
        && this.B.Equals(other.B)
        && this.A.Equals(other.A);

    /// <inheritdoc/>
    public override readonly string ToString() => FormattableString.Invariant($"RgbaVector({this.R:#0.##}, {this.G:#0.##}, {this.B:#0.##}, {this.A:#0.##})");

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(this.R, this.G, this.B, this.A);
}
