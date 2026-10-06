// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an CMYK (cyan, magenta, yellow, keyline) color.
/// <see href="https://en.wikipedia.org/wiki/CMYK_color_model"/>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Cmyk : IColorProfile<Cmyk, Rgb>
{
    private static readonly Vector4 Min = Vector4.Zero;
    private static readonly Vector4 Max = Vector4.One;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmyk"/> struct.
    /// </summary>
    /// <param name="c">The cyan component.</param>
    /// <param name="m">The magenta component.</param>
    /// <param name="y">The yellow component.</param>
    /// <param name="k">The keyline black component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmyk(float c, float m, float y, float k)
        : this(new Vector4(c, m, y, k))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cmyk"/> struct.
    /// </summary>
    /// <param name="vector">The vector representing the c, m, y, k components.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cmyk(Vector4 vector)
    {
        vector = Vector4.Clamp(vector, Min, Max);
        this.C = vector.X;
        this.M = vector.Y;
        this.Y = vector.Z;
        this.K = vector.W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private Cmyk(Vector4 vector, bool _)
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter
    {
        this.C = vector.X;
        this.M = vector.Y;
        this.Y = vector.Z;
        this.K = vector.W;
    }

    /// <summary>
    /// Gets the cyan color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float C { get; }

    /// <summary>
    /// Gets the magenta color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float M { get; }

    /// <summary>
    /// Gets the yellow color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the keyline black color component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float K { get; }

    /// <summary>
    /// Compares two <see cref="Cmyk"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Cmyk left, Cmyk right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Cmyk"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Cmyk left, Cmyk right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4()
    {
        Vector4 v4 = default;
        v4 += this.AsVector4Unsafe();
        return v4;
    }

    /// <inheritdoc/>
    public static Cmyk FromScaledVector4(Vector4 source)
        => new(source, true);

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Cmyk> source, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        MemoryMarshal.Cast<Cmyk, Vector4>(source).CopyTo(destination);
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Cmyk> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        MemoryMarshal.Cast<Vector4, Cmyk>(source).CopyTo(destination);
    }

    /// <inheritdoc/>
    public static Cmyk FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        // To CMY
        Vector3 cmy = Vector3.One - source.AsVector3Unsafe();

        // To CMYK
        Vector3 k = new(MathF.Min(cmy.X, MathF.Min(cmy.Y, cmy.Z)));

        if (k.X >= 1F - Constants.Epsilon)
        {
            return new Cmyk(0, 0, 0, 1F);
        }

        cmy = (cmy - k) / (Vector3.One - k);

        return new Cmyk(cmy.X, cmy.Y, cmy.Z, k.X);
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<Cmyk> destination)
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
        Vector3 rgb = (Vector3.One - new Vector3(this.C, this.M, this.Y)) * (1F - this.K);
        return Rgb.FromScaledVector3(rgb);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Cmyk> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        int i = 0;
        ReadOnlySpan<Vector4> packed = MemoryMarshal.Cast<Cmyk, Vector4>(source);
        ReadOnlySpan<float> components = MemoryMarshal.Cast<Cmyk, float>(source);

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> one = Vector512.Create(1F);

            // Four packed component registers contain sixteen complete CMYK pixels.
            nuint vectorCount = components.Vector512Count<float>() / 4;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave4(packed, i, out Vector512<float> c, out Vector512<float> m, out Vector512<float> y, out Vector512<float> k);

                // Keep the scalar subtraction before multiplication and leave the RGB range unclipped.
                Vector512<float> factor = one - k;
                Vector512<float> r = (one - c) * factor;
                Vector512<float> g = (one - m) * factor;
                Vector512<float> b = (one - y) * factor;
                SimdUtils.Interleave3(r, g, b, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> one = Vector256.Create(1F);

            // Four packed component registers contain eight complete CMYK pixels.
            nuint vectorCount = components[(i * 4)..].Vector256Count<float>() / 4;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave4(packed, i, out Vector256<float> c, out Vector256<float> m, out Vector256<float> y, out Vector256<float> k);
                Vector256<float> factor = one - k;
                Vector256<float> r = (one - c) * factor;
                Vector256<float> g = (one - m) * factor;
                Vector256<float> b = (one - y) * factor;
                SimdUtils.Interleave3(r, g, b, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> one = Vector128.Create(1F);

            // Four packed component registers contain four complete CMYK pixels.
            nuint vectorCount = components[(i * 4)..].Vector128Count<float>() / 4;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<float>.Count)
            {
                // Four CMYK pixels transpose into channel lanes so the scalar (1 - channel) * (1 - K) order is preserved.
                Vector128<float> pixel0 = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.AsRef(in packed[i]));
                Vector128<float> pixel1 = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.AsRef(in packed[i + 1]));
                Vector128<float> pixel2 = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.AsRef(in packed[i + 2]));
                Vector128<float> pixel3 = Unsafe.As<Vector4, Vector128<float>>(ref Unsafe.AsRef(in packed[i + 3]));
                SimdUtils.Transpose4(pixel0, pixel1, pixel2, pixel3, out Vector128<float> c, out Vector128<float> m, out Vector128<float> y, out Vector128<float> k);
                Vector128<float> factor = one - k;
                Vector128<float> r = (one - c) * factor;
                Vector128<float> g = (one - m) * factor;
                Vector128<float> b = (one - y) * factor;
                SimdUtils.Interleave3(r, g, b, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = source[i].ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => HashCode.Combine(this.C, this.M, this.Y, this.K);

    /// <inheritdoc/>
    public override string ToString()
        => FormattableString.Invariant($"Cmyk({this.C:#0.##}, {this.M:#0.##}, {this.Y:#0.##}, {this.K:#0.##})");

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is Cmyk other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Cmyk other)
        => this.AsVector4Unsafe() == other.AsVector4Unsafe();

    private Vector4 AsVector4Unsafe() => Unsafe.As<Cmyk, Vector4>(ref Unsafe.AsRef(in this));
}
