// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an XYB color with opponent X and Y components and a blue absorbance component.
/// Component values are not limited to the interval from zero to one.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial struct Xyb : IColorProfile<Xyb, Rgb>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Xyb"/> struct.
    /// </summary>
    /// <param name="x">The X opponent component.</param>
    /// <param name="y">The Y opponent component.</param>
    /// <param name="b">The blue absorbance component.</param>
    public Xyb(float x, float y, float b)
    {
        this.X = x;
        this.Y = y;
        this.B = b;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Xyb"/> struct from three components.
    /// </summary>
    /// <param name="source">The X, Y and B components.</param>
    public Xyb(Vector3 source)
        : this(source.X, source.Y, source.Z)
    {
    }

    /// <summary>
    /// Gets the X opponent component.
    /// </summary>
    public float X { get; }

    /// <summary>
    /// Gets the Y opponent component.
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the blue absorbance component.
    /// </summary>
    public float B { get; }

    /// <summary>
    /// Compares two XYB colors for equality.
    /// </summary>
    /// <param name="left">The first color.</param>
    /// <param name="right">The second color.</param>
    /// <returns>Whether all three components are equal.</returns>
    public static bool operator ==(Xyb left, Xyb right) => left.Equals(right);

    /// <summary>
    /// Compares two XYB colors for inequality.
    /// </summary>
    /// <param name="left">The first color.</param>
    /// <param name="right">The second color.</param>
    /// <returns>Whether any component differs.</returns>
    public static bool operator !=(Xyb left, Xyb right) => !left.Equals(right);

    /// <inheritdoc/>
    public Vector4 ToScaledVector4() => new(this.X, this.Y, this.B, 1F);

    /// <inheritdoc/>
    public static Xyb FromScaledVector4(Vector4 source) => new(source.AsVector3());

    /// <inheritdoc/>
    public static void ToScaledVector4(ReadOnlySpan<Xyb> source, Span<Vector4> destination)
    {
        // XYB and RGB are both three unbounded floats. Reuse the existing bit-preserving layout conversion.
        Rgb.ToScaledVector4(MemoryMarshal.Cast<Xyb, Rgb>(source), destination);
    }

    /// <inheritdoc/>
    public static void FromScaledVector4(ReadOnlySpan<Vector4> source, Span<Xyb> destination)
        => Rgb.FromScaledVector4(source, MemoryMarshal.Cast<Xyb, Rgb>(destination));

    /// <inheritdoc/>
    public Rgb ToProfileConnectingSpace(ColorConversionOptions options)
    {
        XybTransform transform = options.SourceXybTransform;
        Matrix4x4 matrix = transform.ScaledInverse;
        ConvertToRgb(this.X, this.Y, this.B, in matrix, transform.Biases, transform.CubeRootBiases, out float r, out float g, out float b);

        // The RGB connection space uses the selected working space's transfer function, not raw linear samples.
        return Rgb.FromScaledVector4(options.SourceRgbWorkingSpace.Compress(new Vector4(r, g, b, 1F)));
    }

    /// <inheritdoc/>
    public static Xyb FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        XybTransform transform = options.TargetXybTransform;
        Matrix4x4 matrix = transform.ScaledForward;
        Vector4 linear = options.TargetRgbWorkingSpace.Expand(source.ToScaledVector4());
        ConvertFromRgb(linear.X, linear.Y, linear.Z, in matrix, transform.Biases, transform.CubeRootBiases, out float x, out float y, out float b);
        return new(x, y, b);
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Xyb> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        XybTransform transform = options.SourceXybTransform;
        Matrix4x4 matrix = transform.ScaledInverse;
        Vector3 biases = transform.Biases;
        Vector3 cubeRootBiases = transform.CubeRootBiases;
        InlineArray16<Vector4> storage = default;
        Span<Vector4> buffer = storage;

        // One bounded stack block holds linear RGBA values for the working-space API. It is reused for every batch.
        // Each batch reads its XYB input before publishing RGB, so source and destination can have the same start.
        for (int offset = 0; offset < source.Length; offset += 16)
        {
            int count = Math.Min(16, source.Length - offset);
            ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<Xyb, Vector3>(source.Slice(offset, count));
            Span<Vector4> colors = buffer[..count];
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= count - 16; i += 16)
                {
                    SimdUtils.Deinterleave3(packed, i, out Vector512<float> x, out Vector512<float> y, out Vector512<float> b);
                    ConvertToRgb(x, y, b, in matrix, biases, cubeRootBiases, out Vector512<float> r, out Vector512<float> g, out Vector512<float> blue);
                    SimdUtils.Interleave4(r, g, blue, Vector512<float>.One, ref Unsafe.As<Vector4, float>(ref colors[i]));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= count - 8; i += 8)
                {
                    SimdUtils.Deinterleave3(packed, i, out Vector256<float> x, out Vector256<float> y, out Vector256<float> b);
                    ConvertToRgb(x, y, b, in matrix, biases, cubeRootBiases, out Vector256<float> r, out Vector256<float> g, out Vector256<float> blue);
                    SimdUtils.Interleave4(r, g, blue, Vector256<float>.One, ref Unsafe.As<Vector4, float>(ref colors[i]));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= count - 4; i += 4)
                {
                    SimdUtils.Deinterleave3(packed, i, out Vector128<float> x, out Vector128<float> y, out Vector128<float> b);
                    ConvertToRgb(x, y, b, in matrix, biases, cubeRootBiases, out Vector128<float> r, out Vector128<float> g, out Vector128<float> blue);
                    SimdUtils.Interleave4(r, g, blue, Vector128<float>.One, ref Unsafe.As<Vector4, float>(ref colors[i]));
                }
            }

            for (; i < count; i++)
            {
                Vector3 value = packed[i];
                ConvertToRgb(value.X, value.Y, value.Z, in matrix, biases, cubeRootBiases, out float r, out float g, out float b);
                colors[i] = new(r, g, b, 1F);
            }

            options.SourceRgbWorkingSpace.Compress(colors);
            Rgb.FromScaledVector4(colors, destination.Slice(offset, count));
        }
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<Xyb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));
        XybTransform transform = options.TargetXybTransform;
        Matrix4x4 matrix = transform.ScaledForward;
        Vector3 biases = transform.Biases;
        Vector3 cubeRootBiases = transform.CubeRootBiases;
        InlineArray16<Vector4> storage = default;
        Span<Vector4> buffer = storage;

        // Expand one complete RGB batch before applying the nonlinear absorbance transform.
        // Each register holds the same component from consecutive colors. Narrower widths finish the batch without padding reads.
        for (int offset = 0; offset < source.Length; offset += 16)
        {
            int count = Math.Min(16, source.Length - offset);
            Span<Vector4> colors = buffer[..count];
            Rgb.ToScaledVector4(source.Slice(offset, count), colors);
            options.TargetRgbWorkingSpace.Expand(colors);
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                for (; i <= count - 16; i += 16)
                {
                    SimdUtils.Deinterleave4(colors, i, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b, out _);
                    ConvertFromRgb(r, g, b, in matrix, biases, cubeRootBiases, out Vector512<float> x, out Vector512<float> y, out Vector512<float> blue);
                    SimdUtils.Interleave3(x, y, blue, ref Unsafe.As<Xyb, float>(ref destination[offset + i]));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; i <= count - 8; i += 8)
                {
                    SimdUtils.Deinterleave4(colors, i, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b, out _);
                    ConvertFromRgb(r, g, b, in matrix, biases, cubeRootBiases, out Vector256<float> x, out Vector256<float> y, out Vector256<float> blue);
                    SimdUtils.Interleave3(x, y, blue, ref Unsafe.As<Xyb, float>(ref destination[offset + i]));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; i <= count - 4; i += 4)
                {
                    SimdUtils.Deinterleave4(colors, i, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b, out _);
                    ConvertFromRgb(r, g, b, in matrix, biases, cubeRootBiases, out Vector128<float> x, out Vector128<float> y, out Vector128<float> blue);
                    SimdUtils.Interleave3(x, y, blue, ref Unsafe.As<Xyb, float>(ref destination[offset + i]));
                }
            }

            for (; i < count; i++)
            {
                Vector4 linear = colors[i];
                ConvertFromRgb(linear.X, linear.Y, linear.Z, in matrix, biases, cubeRootBiases, out float x, out float y, out float b);
                destination[offset + i] = new(x, y, b);
            }
        }
    }

    /// <inheritdoc/>
    public static ChromaticAdaptionWhitePointSource GetChromaticAdaptionWhitePointSource()
        => ChromaticAdaptionWhitePointSource.RgbWorkingSpace;

    /// <inheritdoc/>
    public bool Equals(Xyb other) => this.X == other.X && this.Y == other.Y && this.B == other.B;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Xyb other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.X, this.Y, this.B);

    /// <inheritdoc/>
    public override string ToString() => FormattableString.Invariant($"Xyb({this.X}, {this.Y}, {this.B})");
}
