// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

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
        Matrix4x4 m = options.TransposedYCbCrTransform.Inverse;
        Vector3 offset = options.TransposedYCbCrTransform.Offset;
        Vector3 normalized = this.AsVector3Unsafe() - offset;

        return Rgb.FromScaledVector3(Vector3.Transform(normalized, m) * (1F - this.K));
    }

    /// <inheritdoc/>
    public static YccK FromProfileConnectingSpace(ColorConversionOptions options, in Rgb source)
    {
        Matrix4x4 m = options.TransposedYCbCrTransform.Forward;
        Vector3 offset = options.TransposedYCbCrTransform.Offset;

        Vector3 rgb = source.AsVector3Unsafe();
        float k = 1F - MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z));

        if (k >= 1F - Constants.Epsilon)
        {
            return new YccK(new Vector4(0F, 0.5F, 0.5F, 1F), true);
        }

        rgb /= 1F - k;
        return new YccK(new Vector4(Vector3.Transform(rgb, m), k) + new Vector4(offset, 0F));
    }

    /// <inheritdoc/>
    public static void ToProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<YccK> source, Span<Rgb> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Matrix4x4 matrix = options.TransposedYCbCrTransform.Inverse;
        Vector3 offset = options.TransposedYCbCrTransform.Offset;
        ReadOnlySpan<Vector4> packed = MemoryMarshal.Cast<YccK, Vector4>(source);
        int i = 0;

        // Each register holds Y, Cb, Cr or K from independent colors. Remove the configured offsets,
        // restore RGB, then apply the black scale. K is a device component and never acts as pixel alpha.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> offsetY = Vector512.Create(offset.X);
            Vector512<float> offsetCb = Vector512.Create(offset.Y);
            Vector512<float> offsetCr = Vector512.Create(offset.Z);
            nuint count = (source.Length - i).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave4(packed, i, out Vector512<float> y, out Vector512<float> cb, out Vector512<float> cr, out Vector512<float> k);
                SimdUtils.Transform3(
                    in matrix,
                    y - offsetY,
                    cb - offsetCb,
                    cr - offsetCr,
                    out Vector512<float> r,
                    out Vector512<float> g,
                    out Vector512<float> b);

                Vector512<float> scale = Vector512<float>.One - k;
                SimdUtils.Interleave3(r * scale, g * scale, b * scale, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> offsetY = Vector256.Create(offset.X);
            Vector256<float> offsetCb = Vector256.Create(offset.Y);
            Vector256<float> offsetCr = Vector256.Create(offset.Z);
            nuint count = (source.Length - i).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave4(packed, i, out Vector256<float> y, out Vector256<float> cb, out Vector256<float> cr, out Vector256<float> k);
                SimdUtils.Transform3(
                    in matrix,
                    y - offsetY,
                    cb - offsetCb,
                    cr - offsetCr,
                    out Vector256<float> r,
                    out Vector256<float> g,
                    out Vector256<float> b);

                Vector256<float> scale = Vector256<float>.One - k;
                SimdUtils.Interleave3(r * scale, g * scale, b * scale, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> offsetY = Vector128.Create(offset.X);
            Vector128<float> offsetCb = Vector128.Create(offset.Y);
            Vector128<float> offsetCr = Vector128.Create(offset.Z);
            nuint count = (source.Length - i).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave4(packed, i, out Vector128<float> y, out Vector128<float> cb, out Vector128<float> cr, out Vector128<float> k);
                SimdUtils.Transform3(
                    in matrix,
                    y - offsetY,
                    cb - offsetCb,
                    cr - offsetCr,
                    out Vector128<float> r,
                    out Vector128<float> g,
                    out Vector128<float> b);

                Vector128<float> scale = Vector128<float>.One - k;
                SimdUtils.Interleave3(r * scale, g * scale, b * scale, ref Unsafe.As<Rgb, float>(ref destination[i]));
            }
        }

        // The scalar remainder preserves the same subtraction, matrix and black multiplication order.
        // RGB remains unbounded, as in the scalar model conversion.
        for (; i < source.Length; i++)
        {
            destination[i] = source[i].ToProfileConnectingSpace(options);
        }
    }

    /// <inheritdoc/>
    public static void FromProfileConnectionSpace(ColorConversionOptions options, ReadOnlySpan<Rgb> source, Span<YccK> destination)
    {
        Guard.DestinationShouldNotBeTooShort(source, destination, nameof(destination));

        Matrix4x4 matrix = options.TransposedYCbCrTransform.Forward;
        Vector3 offset = options.TransposedYCbCrTransform.Offset;
        ReadOnlySpan<Vector3> packed = MemoryMarshal.Cast<Rgb, Vector3>(source);
        int i = 0;

        // K uses the scalar model's nested maximum. Near-black lanes use the raw constructor's exact
        // [0,0.5,0.5,1] result. Other lanes divide by 1-K, transform RGB, add offsets, then clamp all four components.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> offsetY = Vector512.Create(offset.X);
            Vector512<float> offsetCb = Vector512.Create(offset.Y);
            Vector512<float> offsetCr = Vector512.Create(offset.Z);
            Vector512<float> blackThreshold = Vector512.Create(1F - Constants.Epsilon);
            Vector512<float> half = Vector512.Create(0.5F);
            nuint count = (source.Length - i).Vector512Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector512<float> r, out Vector512<float> g, out Vector512<float> b);
                Vector512<float> k = Vector512<float>.One - Vector512.Max(r, Vector512.Max(g, b));
                Vector512<float> black = Vector512.GreaterThanOrEqual(k, blackThreshold);
                Vector512<float> scale = Vector512<float>.One - k;
                SimdUtils.Transform3(in matrix, r / scale, g / scale, b / scale, out Vector512<float> y, out Vector512<float> cb, out Vector512<float> cr);

                // Selection preserves the near-black branch even if its unused division produced NaN.
                // Vector Clamp has the public constructor's NaN behavior; do not substitute finite-value clamping.
                y = Vector512.ConditionalSelect(black, Vector512<float>.Zero, Vector512.Clamp(y + offsetY, Vector512<float>.Zero, Vector512<float>.One));
                cb = Vector512.ConditionalSelect(black, half, Vector512.Clamp(cb + offsetCb, Vector512<float>.Zero, Vector512<float>.One));
                cr = Vector512.ConditionalSelect(black, half, Vector512.Clamp(cr + offsetCr, Vector512<float>.Zero, Vector512<float>.One));
                k = Vector512.ConditionalSelect(black, Vector512<float>.One, Vector512.Clamp(k, Vector512<float>.Zero, Vector512<float>.One));
                SimdUtils.Interleave4(y, cb, cr, k, ref Unsafe.As<YccK, float>(ref destination[i]));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> offsetY = Vector256.Create(offset.X);
            Vector256<float> offsetCb = Vector256.Create(offset.Y);
            Vector256<float> offsetCr = Vector256.Create(offset.Z);
            Vector256<float> blackThreshold = Vector256.Create(1F - Constants.Epsilon);
            Vector256<float> half = Vector256.Create(0.5F);
            nuint count = (source.Length - i).Vector256Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector256<float> r, out Vector256<float> g, out Vector256<float> b);
                Vector256<float> k = Vector256<float>.One - Vector256.Max(r, Vector256.Max(g, b));
                Vector256<float> black = Vector256.GreaterThanOrEqual(k, blackThreshold);
                Vector256<float> scale = Vector256<float>.One - k;
                SimdUtils.Transform3(in matrix, r / scale, g / scale, b / scale, out Vector256<float> y, out Vector256<float> cb, out Vector256<float> cr);

                // Selection preserves the near-black branch even if its unused division produced NaN.
                // Vector Clamp has the public constructor's NaN behavior; do not substitute finite-value clamping.
                y = Vector256.ConditionalSelect(black, Vector256<float>.Zero, Vector256.Clamp(y + offsetY, Vector256<float>.Zero, Vector256<float>.One));
                cb = Vector256.ConditionalSelect(black, half, Vector256.Clamp(cb + offsetCb, Vector256<float>.Zero, Vector256<float>.One));
                cr = Vector256.ConditionalSelect(black, half, Vector256.Clamp(cr + offsetCr, Vector256<float>.Zero, Vector256<float>.One));
                k = Vector256.ConditionalSelect(black, Vector256<float>.One, Vector256.Clamp(k, Vector256<float>.Zero, Vector256<float>.One));
                SimdUtils.Interleave4(y, cb, cr, k, ref Unsafe.As<YccK, float>(ref destination[i]));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> offsetY = Vector128.Create(offset.X);
            Vector128<float> offsetCb = Vector128.Create(offset.Y);
            Vector128<float> offsetCr = Vector128.Create(offset.Z);
            Vector128<float> blackThreshold = Vector128.Create(1F - Constants.Epsilon);
            Vector128<float> half = Vector128.Create(0.5F);
            nuint count = (source.Length - i).Vector128Count<float>();
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                SimdUtils.Deinterleave3(packed, i, out Vector128<float> r, out Vector128<float> g, out Vector128<float> b);
                Vector128<float> k = Vector128<float>.One - Vector128.Max(r, Vector128.Max(g, b));
                Vector128<float> black = Vector128.GreaterThanOrEqual(k, blackThreshold);
                Vector128<float> scale = Vector128<float>.One - k;
                SimdUtils.Transform3(in matrix, r / scale, g / scale, b / scale, out Vector128<float> y, out Vector128<float> cb, out Vector128<float> cr);

                // Selection preserves the near-black branch even if its unused division produced NaN.
                // Vector Clamp has the public constructor's NaN behavior; do not substitute finite-value clamping.
                y = Vector128.ConditionalSelect(black, Vector128<float>.Zero, Vector128.Clamp(y + offsetY, Vector128<float>.Zero, Vector128<float>.One));
                cb = Vector128.ConditionalSelect(black, half, Vector128.Clamp(cb + offsetCb, Vector128<float>.Zero, Vector128<float>.One));
                cr = Vector128.ConditionalSelect(black, half, Vector128.Clamp(cr + offsetCr, Vector128<float>.Zero, Vector128<float>.One));
                k = Vector128.ConditionalSelect(black, Vector128<float>.One, Vector128.Clamp(k, Vector128<float>.Zero, Vector128<float>.One));
                SimdUtils.Interleave4(y, cb, cr, k, ref Unsafe.As<YccK, float>(ref destination[i]));
            }
        }

        // Narrower widths and the final tail retain the same scalar branch and constructor clamping.
        for (; i < source.Length; i++)
        {
            destination[i] = FromProfileConnectingSpace(options, in source[i]);
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
