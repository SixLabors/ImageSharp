// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides optimized static methods for common mathematical functions specific
/// to color processing.
/// </summary>
internal static class ColorNumerics
{
    /// <summary>
    /// The maximum RGB component used by floating-point color-vector conversions.
    /// </summary>
    /// <remarks>
    /// This is the largest finite half-precision value. Float and half color conversions use the same range.
    /// The working range leaves headroom for float sums and products. Intermediate calculations can exceed this range.
    /// Native floating-point conversions retain their storage range.
    /// </remarks>
    public const float MaxColorValue = 65504F;

    /// <summary>
    /// The minimum RGB component used by floating-point color-vector conversions.
    /// </summary>
    public const float MinColorValue = -MaxColorValue;

    // BT.601 fixes the red and blue chroma amplitudes. Derive green weights before their single conversion to float.
    // These are compile-time expressions; sample conversion uses float arithmetic.
    /// <summary>The BT.601 red contribution from zero-centered Cr, in the same units as Y.</summary>
    public const float Bt601RedCrMultiplier = 1.402F;

    /// <summary>The magnitude of the BT.601 green contribution from zero-centered Cb.</summary>
    public const float Bt601GreenCbMultiplier = (float)(0.114 * 1.772 / 0.587);

    /// <summary>The magnitude of the BT.601 green contribution from zero-centered Cr.</summary>
    public const float Bt601GreenCrMultiplier = (float)(0.299 * 1.402 / 0.587);

    /// <summary>The BT.601 blue contribution from zero-centered Cb, in the same units as Y.</summary>
    public const float Bt601BlueCbMultiplier = 1.772F;

    /// <summary>
    /// Vector for converting pixel to gray value as specified by
    /// ITU-R Recommendation BT.709.
    /// </summary>
    public static readonly Vector4 Bt709 = new(.2126f, .7152f, .0722f, 0.0f);

    /// <summary>Converts full-range BT.601 components to RGB without rounding, normalization or clipping.</summary>
    /// <param name="y">The luma component.</param>
    /// <param name="cb">The zero-centered blue chroma component, in the same units as luma.</param>
    /// <param name="cr">The zero-centered red chroma component, in the same units as luma.</param>
    /// <param name="r">The unrounded red component.</param>
    /// <param name="g">The unrounded green component.</param>
    /// <param name="b">The unrounded blue component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConvertBt601YCbCrToRgb(float y, float cb, float cr, out float r, out float g, out float b)
    {
        // Centering and component scale belong to the caller. Keep the established scalar subtraction order;
        // the destination pixel conversion applies its own bounds and final storage rounding.
        r = y + (Bt601RedCrMultiplier * cr);
        g = y - (Bt601GreenCbMultiplier * cb) - (Bt601GreenCrMultiplier * cr);
        b = y + (Bt601BlueCbMultiplier * cb);
    }

    /// <inheritdoc cref="ConvertBt601YCbCrToRgb(float, float, float, out float, out float, out float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConvertBt601YCbCrToRgb(
        Vector128<float> y,
        Vector128<float> cb,
        Vector128<float> cr,
        out Vector128<float> r,
        out Vector128<float> g,
        out Vector128<float> b)
    {
        // Each lane contains one position from the three component planes. The nested green multiply-adds retain
        // the existing SIMD order: blue chroma contributes first, then red chroma.
        r = Vector128.MultiplyAddEstimate(cr, Vector128.Create(Bt601RedCrMultiplier), y);
        Vector128<float> greenBase = Vector128.MultiplyAddEstimate(cb, Vector128.Create(-Bt601GreenCbMultiplier), y);
        g = Vector128.MultiplyAddEstimate(cr, Vector128.Create(-Bt601GreenCrMultiplier), greenBase);
        b = Vector128.MultiplyAddEstimate(cb, Vector128.Create(Bt601BlueCbMultiplier), y);
    }

    /// <inheritdoc cref="ConvertBt601YCbCrToRgb(float, float, float, out float, out float, out float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConvertBt601YCbCrToRgb(
        Vector256<float> y,
        Vector256<float> cb,
        Vector256<float> cr,
        out Vector256<float> r,
        out Vector256<float> g,
        out Vector256<float> b)
    {
        r = Vector256.MultiplyAddEstimate(cr, Vector256.Create(Bt601RedCrMultiplier), y);
        Vector256<float> greenBase = Vector256.MultiplyAddEstimate(cb, Vector256.Create(-Bt601GreenCbMultiplier), y);
        g = Vector256.MultiplyAddEstimate(cr, Vector256.Create(-Bt601GreenCrMultiplier), greenBase);
        b = Vector256.MultiplyAddEstimate(cb, Vector256.Create(Bt601BlueCbMultiplier), y);
    }

    /// <inheritdoc cref="ConvertBt601YCbCrToRgb(float, float, float, out float, out float, out float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ConvertBt601YCbCrToRgb(
        Vector512<float> y,
        Vector512<float> cb,
        Vector512<float> cr,
        out Vector512<float> r,
        out Vector512<float> g,
        out Vector512<float> b)
    {
        r = Vector512.MultiplyAddEstimate(cr, Vector512.Create(Bt601RedCrMultiplier), y);
        Vector512<float> greenBase = Vector512.MultiplyAddEstimate(cb, Vector512.Create(-Bt601GreenCbMultiplier), y);
        g = Vector512.MultiplyAddEstimate(cr, Vector512.Create(-Bt601GreenCrMultiplier), greenBase);
        b = Vector512.MultiplyAddEstimate(cb, Vector512.Create(Bt601BlueCbMultiplier), y);
    }

    /// <summary>
    /// Gets unrounded, unsaturated luminance using ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="vector">The vector to get the luminance from.</param>
    /// <returns>The unrounded luminance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetBT709Luminance(Vector4 vector) => Vector4.Dot(vector, Bt709);

    /// <summary>
    /// Gets the luminance from the rgb components using the formula
    /// as specified by ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The <see cref="byte"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Get8BitBT709Luminance(byte r, byte g, byte b)
        => (byte)((r * .2126F) + (g * .7152F) + (b * .0722F) + 0.5F);

    /// <summary>
    /// Gets the luminance from the rgb components using the formula
    /// as specified by ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The <see cref="byte"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Get8BitBT709Luminance(ushort r, ushort g, ushort b)
        => (byte)((From16BitTo8Bit(r) * .2126F) +
                  (From16BitTo8Bit(g) * .7152F) +
                  (From16BitTo8Bit(b) * .0722F) + 0.5F);

    /// <summary>
    /// Gets the luminance from the rgb components using the formula as
    /// specified by ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The <see cref="ushort"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Get16BitBT709Luminance(byte r, byte g, byte b)
        => (ushort)((From8BitTo16Bit(r) * .2126F) +
                    (From8BitTo16Bit(g) * .7152F) +
                    (From8BitTo16Bit(b) * .0722F) + 0.5F);

    /// <summary>
    /// Gets the luminance from the rgb components using the formula as
    /// specified by ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The <see cref="ushort"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Get16BitBT709Luminance(ushort r, ushort g, ushort b)
        => (ushort)((r * .2126F) + (g * .7152F) + (b * .0722F) + 0.5F);

    /// <summary>
    /// Gets the luminance from the rgb components using the formula as specified
    /// by ITU-R Recommendation BT.709.
    /// </summary>
    /// <param name="r">The red component.</param>
    /// <param name="g">The green component.</param>
    /// <param name="b">The blue component.</param>
    /// <returns>The <see cref="ushort"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort Get16BitBT709Luminance(float r, float g, float b)
        => (ushort)((r * .2126F) + (g * .7152F) + (b * .0722F) + 0.5F);

    /// <summary>
    /// Scales a value from a 16 bit <see cref="ushort"/> to an
    /// 8 bit <see cref="byte"/> equivalent.
    /// </summary>
    /// <param name="component">The 16 bit component value.</param>
    /// <returns>The <see cref="byte"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte From16BitTo8Bit(ushort component) =>

        // To scale to 8 bits From a 16-bit value V the required value (from the PNG specification) is:
        //
        //    (V * 255) / 65535
        //
        // This reduces to round(V / 257), or floor((V + 128.5)/257)
        //
        // Represent V as the two byte value vhi.vlo.  Make a guess that the
        // result is the top byte of V, vhi, then the correction to this value
        // is:
        //
        //    error = floor(((V-vhi.vhi) + 128.5) / 257)
        //          = floor(((vlo-vhi) + 128.5) / 257)
        //
        // This can be approximated using integer arithmetic (and a signed
        // shift):
        //
        //    error = (vlo-vhi+128) >> 8;
        //
        // The approximate differs from the exact answer only when (vlo-vhi) is
        // 128; it then gives a correction of +1 when the exact correction is
        // 0.  This gives 128 errors.  The exact answer (correct for all 16-bit
        // input values) is:
        //
        //    error = (vlo-vhi+128)*65535 >> 24;
        //
        // An alternative arithmetic calculation which also gives no errors is:
        //
        //    (V * 255 + 32895) >> 16
        (byte)(((component * 255) + 32895) >> 16);

    /// <summary>
    /// Scales a value from a 32 bit <see cref="uint"/> to an
    /// 8 bit <see cref="byte"/> equivalent.
    /// </summary>
    /// <param name="component">The 32 bit component value.</param>
    /// <returns>The <see cref="byte"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte From32BitTo8Bit(uint component) =>

        // To scale to 8 bits from a 32-bit value V the required value is:
        //
        //    (V * 255) / 4294967295
        //
        // Since:
        //
        //    4294967295 = 255 * 16843009
        //
        // this reduces exactly to:
        //
        //    V / 16843009
        //
        // To round to nearest using integer arithmetic we add half the divisor
        // before dividing:
        //
        //    (V + 16843009 / 2) / 16843009
        //
        // where:
        //
        //    16843009 / 2 = 8421504.5
        //
        // Using 8421504 ensures correct round-to-nearest behaviour:
        //
        //    8421504 -> 0
        //    8421505 -> 1
        //
        // The addition must be performed in 64-bit to avoid overflow for large
        // input values (for example uint.MaxValue).
        //
        // Final exact integer implementation:
        //
        //    ((ulong)V + 8421504) / 16843009
        (byte)((component + 8421504UL) / 16843009UL);

    /// <summary>
    /// Scales a value from an 8 bit <see cref="byte"/> to
    /// an 16 bit <see cref="ushort"/> equivalent.
    /// </summary>
    /// <param name="component">The 8 bit component value.</param>
    /// <returns>The <see cref="ushort"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort From8BitTo16Bit(byte component)
        => (ushort)(component * 257);

    /// <summary>
    /// Scales a value from an 16 bit <see cref="byte"/> to
    /// an 16 bit <see cref="uint"/> equivalent.
    /// </summary>
    /// <param name="component">The 16 bit component value.</param>
    /// <returns>The 32 bit <see cref="uint"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint From16BitTo32Bit(ushort component)
        => (uint)(component * 65537);

    /// <summary>
    /// Scales a value from an 8 bit <see cref="byte"/> to
    /// an 32 bit <see cref="ushort"/> equivalent.
    /// </summary>
    /// <param name="component">The 8 bit component value.</param>
    /// <returns>The 32 bit <see cref="uint"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint From8BitTo32Bit(byte component)
        => (uint)(component * 16843009);

    /// <summary>
    /// Returns how many bits are required to store the specified number of colors.
    /// Performs a Log2() on the value.
    /// </summary>
    /// <param name="colors">The number of colors.</param>
    /// <returns>
    /// The <see cref="int"/>
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBitsNeededForColorDepth(int colors)
        => Math.Max(1, (int)Math.Ceiling(Math.Log(colors, 2)));

    /// <summary>
    /// Returns how many colors will be created by the specified number of bits.
    /// </summary>
    /// <param name="bitDepth">The bit depth.</param>
    /// <returns>The <see cref="int"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetColorCountForBitDepth(int bitDepth)
        => 1 << bitDepth;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector4 Transform(Vector4 vector, in ColorMatrix.Impl matrix)
    {
        Vector4 result = matrix.X * vector.X;

        result += matrix.Y * vector.Y;
        result += matrix.Z * vector.Z;
        result += matrix.W * vector.W;
        result += matrix.V;

        return result;
    }

    /// <summary>
    /// Transforms a vector by the given color matrix.
    /// </summary>
    /// <param name="vector">The source vector.</param>
    /// <param name="matrix">The transformation color matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform(ref Vector4 vector, ref ColorMatrix matrix)
        => vector = Transform(vector, matrix.AsImpl());

    /// <summary>
    /// Bulk variant of <see cref="Transform(ref Vector4, ref ColorMatrix)"/>.
    /// </summary>
    /// <param name="vectors">The span of vectors</param>
    /// <param name="matrix">The transformation color matrix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform(Span<Vector4> vectors, ref ColorMatrix matrix)
    {
        for (int i = 0; i < vectors.Length; i++)
        {
            ref Vector4 v = ref vectors[i];
            Transform(ref v, ref matrix);
        }
    }

    /// <summary>
    /// Clamps the value to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range values retain their bits, including signed zero.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive finite value.</param>
    /// <param name="max">The maximum inclusive finite value.</param>
    /// <returns>The clamped <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ClampToFinite(float value, float min, float max) => ClampToFinite<float>(value, min, max);

    /// <summary>
    /// Clamps the value to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range values retain their bits, including signed zero.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive finite value.</param>
    /// <param name="max">The maximum inclusive finite value.</param>
    /// <returns>The clamped <see cref="double"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ClampToFinite(double value, double min, double max) => ClampToFinite<double>(value, min, max);

    /// <summary>
    /// Clamps components to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range components retain their bits, including signed zero.
    /// </summary>
    /// <param name="value">The components to clamp.</param>
    /// <param name="min">The inclusive finite lower bounds.</param>
    /// <param name="max">The inclusive finite upper bounds.</param>
    /// <returns>The clamped components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ClampToFinite(Vector2 value, Vector2 min, Vector2 max)
        => ClampToFinite(value.AsVector128(), min.AsVector128(), max.AsVector128()).AsVector2();

    /// <summary>
    /// Clamps components to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range components retain their bits, including signed zero.
    /// </summary>
    /// <param name="value">The components to clamp.</param>
    /// <param name="min">The inclusive finite lower bounds.</param>
    /// <param name="max">The inclusive finite upper bounds.</param>
    /// <returns>The clamped components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ClampToFinite(Vector3 value, Vector3 min, Vector3 max)
        => ClampToFinite(value.AsVector128(), min.AsVector128(), max.AsVector128()).AsVector3();

    /// <summary>
    /// Clamps components to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range components retain their bits, including signed zero.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="value">The components to clamp.</param>
    /// <param name="min">The inclusive finite lower bounds.</param>
    /// <param name="max">The inclusive finite upper bounds.</param>
    /// <returns>The clamped components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<T> ClampToFinite<T>(Vector128<T> value, Vector128<T> min, Vector128<T> max)
        where T : struct, IFloatingPointIeee754<T>
    {
        // Ordered comparisons map NaN to min and preserve in-range signed zero on every runtime.
        Vector128<T> lowerClamped = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(value, min), value, min);
        return Vector128.ConditionalSelect(Vector128.GreaterThan(value, max), max, lowerClamped);
    }

    /// <summary>
    /// Clamps components to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range components retain their bits, including signed zero.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="value">The components to clamp.</param>
    /// <param name="min">The inclusive finite lower bounds.</param>
    /// <param name="max">The inclusive finite upper bounds.</param>
    /// <returns>The clamped components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<T> ClampToFinite<T>(Vector256<T> value, Vector256<T> min, Vector256<T> max)
        where T : struct, IFloatingPointIeee754<T>
    {
        // Ordered comparisons map NaN to min and preserve in-range signed zero on every runtime.
        Vector256<T> lowerClamped = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(value, min), value, min);
        return Vector256.ConditionalSelect(Vector256.GreaterThan(value, max), max, lowerClamped);
    }

    /// <summary>
    /// Clamps components to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range components retain their bits, including signed zero.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="value">The components to clamp.</param>
    /// <param name="min">The inclusive finite lower bounds.</param>
    /// <param name="max">The inclusive finite upper bounds.</param>
    /// <returns>The clamped components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<T> ClampToFinite<T>(Vector512<T> value, Vector512<T> min, Vector512<T> max)
        where T : struct, IFloatingPointIeee754<T>
    {
        // Ordered comparisons map NaN to min and preserve in-range signed zero on every runtime.
        Vector512<T> lowerClamped = Vector512.ConditionalSelect(Vector512.GreaterThanOrEqual(value, min), value, min);
        return Vector512.ConditionalSelect(Vector512.GreaterThan(value, max), max, lowerClamped);
    }

    /// <summary>
    /// Clamps components to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range components retain their bits, including signed zero.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive finite value.</param>
    /// <param name="max">The maximum inclusive finite value.</param>
    /// <returns>The clamped <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ClampToFinite(Vector4 value, Vector4 min, Vector4 max)
        => ClampToFinite(value.AsVector128(), min.AsVector128(), max.AsVector128()).AsVector4();

    /// <summary>
    /// Clamps the span values to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range values retain their bits, including signed zero.
    /// </summary>
    /// <param name="span">The span containing the values to clamp.</param>
    /// <param name="min">The minimum inclusive finite value.</param>
    /// <param name="max">The maximum inclusive finite value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampToFinite(Span<float> span, float min, float max)
        => ClampToFinite<float>(span, min, max);

    /// <summary>
    /// Clamps the span values to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range values retain their bits, including signed zero.
    /// </summary>
    /// <param name="span">The span containing the values to clamp.</param>
    /// <param name="min">The minimum inclusive finite value.</param>
    /// <param name="max">The maximum inclusive finite value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampToFinite(Span<double> span, double min, double max)
        => ClampToFinite<double>(span, min, max);

    /// <summary>
    /// Clamps a component to the inclusive bounds. NaN maps to <paramref name="min"/>.
    /// In-range values retain their bits, including signed zero.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="value">The component to clamp.</param>
    /// <param name="min">The inclusive finite lower bound.</param>
    /// <param name="max">The inclusive finite upper bound.</param>
    /// <returns>The clamped component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ClampToFinite<T>(T value, T min, T max)
        where T : struct, IFloatingPointIeee754<T>
    {
        // Ordered comparisons map NaN to min; in-range values retain their original bits, including signed zero.
        return value > max ? max : value >= min ? value : min;
    }

    /// <summary>
    /// Applies the scalar NaN and signed-zero rules to floating-point spans in place.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="span">The components to clamp.</param>
    /// <param name="min">The inclusive finite lower bound.</param>
    /// <param name="max">The inclusive finite upper bound.</param>
    private static void ClampToFinite<T>(Span<T> span, T min, T max)
        where T : struct, IFloatingPointIeee754<T>
    {
        ref T start = ref MemoryMarshal.GetReference(span);
        int i = 0;

        // Each register uses the same ClampToFinite overload as individual vector callers. Descending widths consume
        // the remainder without overlapping stores, and the final components use the scalar overload.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<T> lower = Vector512.Create(min);
            Vector512<T> upper = Vector512.Create(max);

            for (; i <= span.Length - Vector512<T>.Count; i += Vector512<T>.Count)
            {
                Vector512<T> value = Vector512.LoadUnsafe(ref start, (nuint)i);
                ClampToFinite(value, lower, upper).StoreUnsafe(ref start, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<T> lower = Vector256.Create(min);
            Vector256<T> upper = Vector256.Create(max);

            for (; i <= span.Length - Vector256<T>.Count; i += Vector256<T>.Count)
            {
                Vector256<T> value = Vector256.LoadUnsafe(ref start, (nuint)i);
                ClampToFinite(value, lower, upper).StoreUnsafe(ref start, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<T> lower = Vector128.Create(min);
            Vector128<T> upper = Vector128.Create(max);

            for (; i <= span.Length - Vector128<T>.Count; i += Vector128<T>.Count)
            {
                Vector128<T> value = Vector128.LoadUnsafe(ref start, (nuint)i);
                ClampToFinite(value, lower, upper).StoreUnsafe(ref start, (nuint)i);
            }
        }

        for (; i < span.Length; i++)
        {
            ref T value = ref Unsafe.Add(ref start, (uint)i);
            value = ClampToFinite(value, min, max);
        }
    }
}
