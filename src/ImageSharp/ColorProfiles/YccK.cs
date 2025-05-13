// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents a YCCK (luminance, blue chroma, red chroma, black) color.
/// YCCK is not a true color space but a reversible transform of CMYK, where the CMY components
/// are converted to YCbCr using the ITU-R BT.601 standard, and the K (black) component is preserved separately.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct YccK : IColorProfile<YccK, Rgb>
{
    private static readonly Vector4 Min = Vector4.Zero;
    private static readonly Vector4 Max = Vector4.One;

    /// <summary>
    /// Initializes a new instance of the <see cref="YccK"/> struct.
    /// </summary>
    /// <param name="y">The y luminance component.</param>
    /// <param name="cb">The cb chroma component.</param>
    /// <param name="cr">The cr chroma component.</param>
    /// <param name="k">The keyline black component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public YccK(float y, float cb, float cr, float k)
        : this(new Vector4(y, cb, cr, k))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YccK"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the c, m, y, k components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public YccK(Vector4 vector)
    {
        vector = Vector4.Clamp(vector, Min, Max);
        this.Y = vector.X;
        this.Cb = vector.Y;
        this.Cr = vector.Z;
        this.K = vector.W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private YccK(Vector4 vector, bool _)
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    {
        this.Y = vector.X;
        this.Cb = vector.Y;
        this.Cr = vector.Z;
        this.K = vector.W;
    }

    /// <summary>
    /// Gets the Y luminance component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the C (blue) chroma component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float Cb { get; }

    /// <summary>
    /// Gets the C (red) chroma component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float Cr { get; }

    /// <summary>
    /// Gets the keyline black color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float K { get; }

    /// <summary>
    /// Compares two <see cref="YccK"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="YccK"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="YccK"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(YccK left, YccK right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="YccK"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="YccK"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="YccK"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(YccK left, YccK right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
    {
        Vector4 v4 = default;
        v4 += this.AsVector4Unsafe();
        return v4;
    }

    /// <inheritdoc/>
    public static YccK FromScaledVector4(Vector4 source)
        => new(source, true);

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<YccK> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        MemoryMarshal.Cast<YccK, Vector4>(source).CopyTo(destination);
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<YccK> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        MemoryMarshal.Cast<Vector4, YccK>(source).CopyTo(destination);
    }

    /// <inheritdoc/>
    public Rgb ToProfileConnectingSpace(ColorConversionOptions options)
    {
        Matrix4x4 m = options.YCbCrMatrix.Inverse;
        Vector3 offset = options.YCbCrMatrix.Offset;
        Vector3 normalized = this.AsVector3Unsafe() - offset;

        float r = Vector3.Dot(normalized, new Vector3(m.M11, m.M12, m.M13));
        float g = Vector3.Dot(normalized, new Vector3(m.M21, m.M22, m.M23));
        float b = Vector3.Dot(normalized, new Vector3(m.M31, m.M32, m.M33));

        Vector3 rgb = new Vector3(r, g, b) * (1F - this.K);
        return Rgb.FromScaledVector3(rgb);
    }

    /// <inheritdoc/>
    public static YccK FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        Matrix4x4 m = options.YCbCrMatrix.Forward;
        Vector3 offset = options.YCbCrMatrix.Offset;

        Vector3 rgb = source.AsVector3Unsafe();
        float k = 1F - MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z));

        if (k >= 1F - Constants.Epsilon)
        {
            return new YccK(new Vector4(0F, 0.5F, 0.5F, 1F), true);
        }

        rgb /= 1F - k;

        float y = Vector3.Dot(rgb, new Vector3(m.M11, m.M12, m.M13));
        float cb = Vector3.Dot(rgb, new Vector3(m.M21, m.M22, m.M23));
        float cr = Vector3.Dot(rgb, new Vector3(m.M31, m.M32, m.M33));

        return new YccK(new Vector4(y, cb, cr, k) + new Vector4(offset, 0F));
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<YccK> source, Span<Rgb> destination)
    {
        // TODO: We can possibly optimize this by using SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<YccK> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: We can optimize this by using SIMD
        for (int i = 0; i < source.Length; i++)
        {
            Rgb rgb = source[i];
            destination[i] = FromProfileConnectingSpace(options, in rgb);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => HashCode.Combine(this.Y, this.Cb, this.Cr, this.K);

    /// <inheritdoc/>
    public override string ToString()
        => FormattableString.Invariant($"YccK({this.Y:#0.##}, {this.Cb:#0.##}, {this.Cr:#0.##}, {this.K:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is YccK other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(YccK other)
        => this.AsVector4Unsafe() == other.AsVector4Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<YccK, Vector3>(ref Unsafe.AsRef(in this));

    private Vector4 AsVector4Unsafe() => Unsafe.As<YccK, Vector4>(ref Unsafe.AsRef(in this));
}
