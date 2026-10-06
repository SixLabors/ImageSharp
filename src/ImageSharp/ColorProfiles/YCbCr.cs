// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Represents an YCbCr (luminance, blue chroma, red chroma) color.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct YCbCr : IColorProfile<YCbCr, Rgb>
{
    private static readonly Vector3 Min = Vector3.Zero;
    private static readonly Vector3 Max = Vector3.One;

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
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float Y { get; }

    /// <summary>
    /// Gets the Cb chroma component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
    /// </summary>
    public float Cb { get; }

    /// <summary>
    /// Gets the Cr chroma component.
    /// <remarks>A value ranging between 0 and 1.</remarks>
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
        return new Vector4(v3, 1F);
    }

    /// <inheritdoc/>
    public static YCbCr FromScaledVector4(Vector4 source)
        => new(source.AsVector3(), true);

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
        Vector3 rgb = source.AsVector3Unsafe();
        Matrix4x4 m = options.TransposedYCbCrTransform.Forward;
        Vector3 offset = options.TransposedYCbCrTransform.Offset;

        return new YCbCr(Vector3.Transform(rgb, m) + offset, true);
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
        Matrix4x4 m = options.TransposedYCbCrTransform.Inverse;
        Vector3 offset = options.TransposedYCbCrTransform.Offset;
        Vector3 normalized = this.AsVector3Unsafe() - offset;

        return Rgb.FromScaledVector3(Vector3.Transform(normalized, m));
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<YCbCr> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Matrix4x4 matrix = options.TransposedYCbCrTransform.Inverse;
        Vector3 offset = options.TransposedYCbCrTransform.Offset;
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<YCbCr, Vector3>(source);
        ReadOnlySpan<float> components = MemoryMarshal.Cast<YCbCr, float>(source);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> offsetY = Vector512.Create(offset.X);
            Vector512<float> offsetCb = Vector512.Create(offset.Y);
            Vector512<float> offsetCr = Vector512.Create(offset.Z);
            nuint vectorCount = components.Vector512Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector512<float>.Count)
            {
                // Each complete group supplies sixteen packed triples; load all channels before the aliased RGB store.
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> y, out Vector512<float> cb, out Vector512<float> cr);
                SimdUtils.Transform3(in matrix, y - offsetY, cb - offsetCb, cr - offsetCr, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b);
                SimdUtils.Interleave3(r, g, b, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> offsetY = Vector256.Create(offset.X);
            Vector256<float> offsetCb = Vector256.Create(offset.Y);
            Vector256<float> offsetCr = Vector256.Create(offset.Z);
            nuint vectorCount = components[(i * 3)..].Vector256Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector256<float>.Count)
            {
                // The remaining complete eight-pixel groups use the same configured offset and matrix.
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> y, out Vector256<float> cb, out Vector256<float> cr);
                SimdUtils.Transform3(in matrix, y - offsetY, cb - offsetCb, cr - offsetCr, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b);
                SimdUtils.Interleave3(r, g, b, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> offsetY = Vector128.Create(offset.X);
            Vector128<float> offsetCb = Vector128.Create(offset.Y);
            Vector128<float> offsetCr = Vector128.Create(offset.Z);
            nuint vectorCount = components[(i * 3)..].Vector128Count<float>() / 3;

            for (nuint vectorIndex = 0; vectorIndex < vectorCount; vectorIndex++, i += Vector128<float>.Count)
            {
                // Four-pixel groups and the scalar tail retain the same configured offset and inverse transform.
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> y, out Vector128<float> cb, out Vector128<float> cr);
                SimdUtils.Transform3(in matrix, y - offsetY, cb - offsetCb, cr - offsetCr, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b);
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
