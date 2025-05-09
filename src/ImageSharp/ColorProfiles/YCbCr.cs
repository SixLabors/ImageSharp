// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification for the JFIF use with Jpeg.
/// <see href="http://en.wikipedia.org/wiki/YCbCr"/>
/// <see href="http://www.ijg.org/files/T-REC-T.871-201105-I!!PDF-E.pdf"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct YCbCr : IColorProfile<YCbCr, Rgb>
{
    private static readonly Vector3 Min = Vector3.Zero;
    private static readonly Vector3 Max = new(255);

    /// <summary>
    /// Initializes a new instance of the <see cref="YCbCr"/> struct.
    /// </summary>
    /// <param name="y">The y luminance component.</param>
    /// <param name="cb">The cb chroma component.</param>
    /// <param name="cr">The cr chroma component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public YCbCr(float y, float cb, float cr)
        : this(new Vector3(y, cb, cr))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YCbCr"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the y, cb, cr components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public YCbCr(Vector3 vector)
    {
        vector = Vector3.Clamp(vector, Min, Max);
        this.Y = vector.X;
        this.Cb = vector.Y;
        this.Cr = vector.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private YCbCr(Vector3 vector, bool _)
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    {
        this.Y = vector.X;
        this.Cb = vector.Y;
        this.Cr = vector.Z;
    }

    /// <summary>
    /// Gets the Y luminance component.
    /// <remarks>A value ranging between 0 and 255.</remarks>
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the Cb chroma component.
    /// <remarks>A value ranging between 0 and 255.</remarks>
    /// </summary>
    public float Cb { get; }

    /// <summary>
    /// Gets the Cr chroma component.
    /// <remarks>A value ranging between 0 and 255.</remarks>
    /// </summary>
    public float Cr { get; }

    /// <summary>
    /// Compares two <see cref="YCbCr"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="YCbCr"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="YCbCr"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    public static bool operator ==(YCbCr left, YCbCr right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="YCbCr"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="YCbCr"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="YCbCr"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(YCbCr left, YCbCr right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
    {
        Vector3 v3 = default;
        v3 += this.AsVector3Unsafe();
        v3 /= Max;
        return new Vector4(v3, 1F);
    }

    /// <inheritdoc/>
    public static YCbCr FromScaledVector4(Vector4 source)
    {
        Vector3 v3 = source.AsVector3();
        v3 *= Max;
        return new YCbCr(v3, true);
    }

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<YCbCr> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToScaledVector4();
        }
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<YCbCr> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: Optimize via SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = FromScaledVector4(source[i]);
        }
    }

    /// <inheritdoc/>
    public static YCbCr FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        Vector3 rgb = source.ToScaledVector3() * Max;
        float r = rgb.X;
        float g = rgb.Y;
        float b = rgb.Z;

        float y = (0.299F * r) + (0.587F * g) + (0.114F * b);
        float cb = 128F + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b));
        float cr = 128F + ((0.5F * r) - (0.418688F * g) - (0.081312F * b));

        return new YCbCr(y, cb, cr);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<YCbCr> destination)
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
    public Rgb ToProfileConnectingSpace(ColorConversionOptions options)
    {
        float y = this.Y;
        float cb = this.Cb - 128F;
        float cr = this.Cr - 128F;

        float r = MathF.Round(y + (1.402F * cr), MidpointRounding.AwayFromZero);
        float g = MathF.Round(y - (0.344136F * cb) - (0.714136F * cr), MidpointRounding.AwayFromZero);
        float b = MathF.Round(y + (1.772F * cb), MidpointRounding.AwayFromZero);

        return Rgb.FromScaledVector3(new Vector3(r, g, b) / Max);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<YCbCr> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        // TODO: We can optimize this by using SIMD
        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i].ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(this.Y, this.Cb, this.Cr);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"YCbCr({this.Y}, {this.Cb}, {this.Cr})");

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is YCbCr other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(YCbCr other)
        => this.AsVector3Unsafe() == other.AsVector3Unsafe();

    private Vector3 AsVector3Unsafe() => Unsafe.As<YCbCr, Vector3>(ref Unsafe.AsRef(in this));
}
