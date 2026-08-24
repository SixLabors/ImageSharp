// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides optimized static methods for trigonometric, logarithmic,
/// and other common mathematical functions.
/// </summary>
internal static class Numerics
{
    public const int BlendAlphaControl = 0b_10_00_10_00;
    private const int ShuffleAlphaControl = 0b_11_11_11_11;

    /// <summary>
    /// Determine the Greatest CommonDivisor (GCD) of two numbers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }

        return a;
    }

    /// <summary>
    /// Determine the Least Common Multiple (LCM) of two numbers.
    /// See https://en.wikipedia.org/wiki/Least_common_multiple#Reduction_by_the_greatest_common_divisor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeastCommonMultiple(int a, int b)
        => a / GreatestCommonDivisor(a, b) * b;

    /// <summary>
    /// Calculates <paramref name="x"/> % 2
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Modulo2(int x) => x & 1;

    /// <summary>
    /// Calculates <paramref name="x"/> % 4
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Modulo4(int x) => x & 3;

    /// <summary>
    /// Calculates <paramref name="x"/> % 4
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint Modulo4(nint x) => x & 3;

    /// <summary>
    /// Calculates <paramref name="x"/> % 4
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint Modulo4(nuint x) => x & 3;

    /// <summary>
    /// Calculates <paramref name="x"/> % 8
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Modulo8(int x) => x & 7;

    /// <summary>
    /// Calculates <paramref name="x"/> % 8
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint Modulo8(nint x) => x & 7;

    /// <summary>
    /// Calculates <paramref name="x"/> % 64
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Modulo64(int x) => x & 63;

    /// <summary>
    /// Calculates <paramref name="x"/> % 64
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint Modulo64(nint x) => x & 63;

    /// <summary>
    /// Calculates <paramref name="x"/> % 256
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Modulo256(int x) => x & 255;

    /// <summary>
    /// Calculates <paramref name="x"/> % 256
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nint Modulo256(nint x) => x & 255;

    /// <summary>
    /// Fast (x mod m) calculator, with the restriction that
    /// <paramref name="m"/> should be power of 2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ModuloP2(int x, int m) => x & (m - 1);

    /// <summary>
    /// Returns the absolute value of a 32-bit signed integer.
    /// Uses bit shifting to speed up the operation compared to <see cref="Math"/>.
    /// </summary>
    /// <param name="x">
    /// A number that is greater than <see cref="int.MinValue"/>, but less than
    /// or equal to <see cref="int.MaxValue"/>
    /// </param>
    /// <returns>The <see cref="int"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Abs(int x)
    {
        int y = x >> 31;
        return (x ^ y) - y;
    }

    /// <summary>
    /// Returns a specified number raised to the power of 2
    /// </summary>
    /// <param name="x">A single-precision floating-point number</param>
    /// <returns>The number <paramref name="x" /> raised to the power of 2.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pow2(float x) => x * x;

    /// <summary>
    /// Returns a specified number raised to the power of 3
    /// </summary>
    /// <param name="x">A single-precision floating-point number</param>
    /// <returns>The number <paramref name="x" /> raised to the power of 3.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pow3(float x) => x * x * x;

    /// <summary>
    /// Returns a specified number raised to the power of 3
    /// </summary>
    /// <param name="x">A double-precision floating-point number</param>
    /// <returns>The number <paramref name="x" /> raised to the power of 3.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Pow3(double x) => x * x * x;

    /// <summary>
    /// Implementation of 1D Gaussian G(x) function
    /// </summary>
    /// <param name="x">The x provided to G(x).</param>
    /// <param name="sigma">The spread of the blur.</param>
    /// <returns>The Gaussian G(x)</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Gaussian(float x, float sigma)
    {
        const float numerator = 1.0f;
        float denominator = MathF.Sqrt(2 * MathF.PI) * sigma;

        float exponentNumerator = -x * x;
        float exponentDenominator = 2 * Pow2(sigma);

        float left = numerator / denominator;
        float right = MathF.Exp(exponentNumerator / exponentDenominator);

        return left * right;
    }

    /// <summary>
    /// Returns the result of a normalized sine cardinal function for the given value.
    /// SinC(x) = sin(pi*x)/(pi*x).
    /// </summary>
    /// <param name="f">A single-precision floating-point number to calculate the result for.</param>
    /// <returns>
    /// The sine cardinal of <paramref name="f" />.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinC(float f)
    {
        if (MathF.Abs(f) > Constants.Epsilon)
        {
            f *= MathF.PI;
            float result = MathF.Sin(f) / f;
            return MathF.Abs(result) < Constants.Epsilon ? 0F : result;
        }

        return 1F;
    }

    /// <summary>
    /// Returns the value clamped to the inclusive range of min and max.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    /// <returns>The clamped <see cref="byte"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Clamp(byte value, byte min, byte max)
    {
        // Order is important here as someone might set min to higher than max.
        if (value > max)
        {
            return max;
        }

        if (value < min)
        {
            return min;
        }

        return value;
    }

    /// <summary>
    /// Returns the value clamped to the inclusive range of min and max.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    /// <returns>The clamped <see cref="uint"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Clamp(uint value, uint min, uint max)
    {
        if (value > max)
        {
            return max;
        }

        if (value < min)
        {
            return min;
        }

        return value;
    }

    /// <summary>
    /// Returns the value clamped to the inclusive range of min and max.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    /// <returns>The clamped <see cref="uint"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int value, int min, int max)
    {
        if (value > max)
        {
            return max;
        }

        if (value < min)
        {
            return min;
        }

        return value;
    }

    /// <summary>
    /// Returns the value clamped to the inclusive range of min and max.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    /// <returns>The clamped <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max)
    {
        if (value > max)
        {
            return max;
        }

        if (value < min)
        {
            return min;
        }

        return value;
    }

    /// <summary>
    /// Returns the value clamped to the inclusive range of min and max.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    /// <returns>The clamped <see cref="double"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double value, double min, double max)
    {
        if (value > max)
        {
            return max;
        }

        if (value < min)
        {
            return min;
        }

        return value;
    }

    /// <summary>
    /// Returns the value clamped to the inclusive range of min and max.
    /// 5x Faster than <see cref="Vector4.Clamp(Vector4, Vector4, Vector4)"/>
    /// on platforms &lt; NET 5.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    /// <returns>The clamped <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Clamp(Vector4 value, Vector4 min, Vector4 max)
        => Vector4.Min(Vector4.Max(value, min), max);

    /// <summary>
    /// Clamps the span values to the inclusive range of min and max.
    /// </summary>
    /// <param name="span">The span containing the values to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp(Span<byte> span, byte min, byte max)
        => TensorPrimitives.Clamp(span, min, max, span);

    /// <summary>
    /// Clamps the span values to the inclusive range of min and max.
    /// </summary>
    /// <param name="span">The span containing the values to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp(Span<uint> span, uint min, uint max)
        => TensorPrimitives.Clamp(span, min, max, span);

    /// <summary>
    /// Clamps the span values to the inclusive range of min and max.
    /// </summary>
    /// <param name="span">The span containing the values to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp(Span<int> span, int min, int max)
        => TensorPrimitives.Clamp(span, min, max, span);

    /// <summary>
    /// Clamps the span values to the inclusive range of min and max.
    /// </summary>
    /// <param name="span">The span containing the values to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp(Span<float> span, float min, float max)
        => TensorPrimitives.Clamp(span, min, max, span);

    /// <summary>
    /// Clamps the span values to the inclusive range of min and max.
    /// </summary>
    /// <param name="span">The span containing the values to clamp.</param>
    /// <param name="min">The minimum inclusive value.</param>
    /// <param name="max">The maximum inclusive value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp(Span<double> span, double min, double max)
        => TensorPrimitives.Clamp(span, min, max, span);

    /// <summary>
    /// Pre-multiplies the "x", "y", "z" components of a vector by its "w" component leaving the "w" component intact.
    /// </summary>
    /// <param name="source">The <see cref="Vector4"/> to premultiply.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Premultiply(ref Vector4 source)
    {
        // Load into a local variable to prevent accessing the source from memory multiple times.
        Vector4 src = source;
        Vector4 alpha = PermuteW(src);
        source = WithW(src * alpha, alpha);
    }

    /// <summary>
    /// Clamps associated color components to the alpha component while preserving alpha.
    /// </summary>
    /// <param name="source">The associated vector to clamp.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ClampRgbToAlpha(ref Vector4 source)
    {
        Vector4 alpha = PermuteW(source);
        source = WithW(Vector4.Min(Vector4.Max(source, Vector4.Zero), alpha), alpha);
    }

    /// <summary>
    /// Premultiplies the X, Y, and Z components of each vector by its W component while preserving W.
    /// </summary>
    /// <param name="vectors">The vectors to premultiply.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Premultiply(Span<Vector4> vectors)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector512<float>.Count / Vector128<float>.Count;
            ref Vector512<float> vectorsBase = ref Unsafe.As<Vector4, Vector512<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector512<float> vectorsEnd = ref Unsafe.Add(ref vectorsBase, (uint)(vectors.Length / vectorsPerVector));
            Vector128<float> alphaMask128 = Vector128.Create(0, 0, 0, -1).AsSingle();
            Vector256<float> alphaMask256 = Vector256.Create(alphaMask128, alphaMask128);
            Vector512<float> alphaMask = Vector512.Create(alphaMask256, alphaMask256);

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsEnd))
            {
                Vector512<float> source = vectorsBase;
                Vector512<float> alpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);

                // Multiplication also squares W, so select the original W lanes to preserve alpha bit-for-bit.
                vectorsBase = Vector512.ConditionalSelect(alphaMask, source, source * alpha);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            vectors = vectors[(vectors.Length - (vectors.Length % vectorsPerVector))..];
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector256<float>.Count / Vector128<float>.Count;
            ref Vector256<float> vectorsBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector256<float> vectorsEnd = ref Unsafe.Add(ref vectorsBase, (uint)(vectors.Length / vectorsPerVector));
            Vector128<float> alphaMask128 = Vector128.Create(0, 0, 0, -1).AsSingle();
            Vector256<float> alphaMask = Vector256.Create(alphaMask128, alphaMask128);

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsEnd))
            {
                Vector256<float> source = vectorsBase;
                Vector256<float> alpha = Vector256_.ShuffleNative(source, ShuffleAlphaControl);

                // Multiplication also squares W, so select the original W lanes to preserve alpha bit-for-bit.
                vectorsBase = Vector256.ConditionalSelect(alphaMask, source, source * alpha);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            vectors = vectors[(vectors.Length - (vectors.Length % vectorsPerVector))..];
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref Vector128<float> vectorsBase = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector128<float> vectorsEnd = ref Unsafe.Add(ref vectorsBase, (uint)vectors.Length);
            Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsEnd))
            {
                Vector128<float> source = vectorsBase;
                Vector128<float> alpha = Vector128_.ShuffleNative(source, ShuffleAlphaControl);

                // Multiplication also squares W, so select the original W lane to preserve alpha bit-for-bit.
                vectorsBase = Vector128.ConditionalSelect(alphaMask, source, source * alpha);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            return;
        }

        ref Vector4 vectorsStart = ref MemoryMarshal.GetReference(vectors);

        for (nuint i = 0; i < (uint)vectors.Length; i++)
        {
            Premultiply(ref Unsafe.Add(ref vectorsStart, i));
        }
    }

    /// <summary>
    /// Reverses the result of premultiplying a vector via <see cref="Premultiply(ref Vector4)"/>.
    /// When alpha is zero, the RGB components remain unchanged because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The <see cref="Vector4"/> to unpremultiply.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnPremultiply(ref Vector4 source)
    {
        Vector4 alpha = PermuteW(source);
        UnPremultiply(ref source, alpha);
    }

    /// <summary>
    /// Unpremultiplies the X, Y, and Z components of a vector by the supplied alpha while preserving W.
    /// When alpha is zero, the RGB components remain unchanged because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The vector to unpremultiply.</param>
    /// <param name="alpha">The source alpha replicated to every component.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnPremultiply(ref Vector4 source, Vector4 alpha)
    {
        // Zero alpha has no mathematical inverse, so preserve stored additive or hidden RGB data unchanged.
        if (alpha == Vector4.Zero)
        {
            return;
        }

        // Division would replace W with one, so restore the original alpha component exactly.
        source = WithW(source / alpha, alpha);
    }

    /// <summary>
    /// Unpremultiplies the X, Y, and Z components of each vector by its W component while preserving W.
    /// Vectors with zero W retain their RGB components because no unassociated value can be recovered.
    /// </summary>
    /// <param name="vectors">The vectors to unpremultiply.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UnPremultiply(Span<Vector4> vectors)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector512<float>.Count / Vector128<float>.Count;
            ref Vector512<float> vectorsBase = ref Unsafe.As<Vector4, Vector512<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector512<float> vectorsEnd = ref Unsafe.Add(ref vectorsBase, (uint)(vectors.Length / vectorsPerVector));

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsEnd))
            {
                Vector512<float> source = vectorsBase;
                Vector512<float> alpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
                vectorsBase = UnPremultiply(source, alpha);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            vectors = vectors[(vectors.Length - (vectors.Length % vectorsPerVector))..];
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int vectorsPerVector = Vector256<float>.Count / Vector128<float>.Count;
            ref Vector256<float> vectorsBase = ref Unsafe.As<Vector4, Vector256<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector256<float> vectorsEnd = ref Unsafe.Add(ref vectorsBase, (uint)(vectors.Length / vectorsPerVector));

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsEnd))
            {
                Vector256<float> source = vectorsBase;
                Vector256<float> alpha = Vector256_.ShuffleNative(source, ShuffleAlphaControl);
                vectorsBase = UnPremultiply(source, alpha);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            vectors = vectors[(vectors.Length - (vectors.Length % vectorsPerVector))..];
        }

        if (Vector128.IsHardwareAccelerated)
        {
            ref Vector128<float> vectorsBase = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector128<float> vectorsEnd = ref Unsafe.Add(ref vectorsBase, (uint)vectors.Length);

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsEnd))
            {
                Vector128<float> source = vectorsBase;
                Vector128<float> alpha = Vector128_.ShuffleNative(source, ShuffleAlphaControl);
                vectorsBase = UnPremultiply(source, alpha);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            return;
        }

        ref Vector4 vectorsStart = ref MemoryMarshal.GetReference(vectors);

        for (nuint i = 0; i < (uint)vectors.Length; i++)
        {
            UnPremultiply(ref Unsafe.Add(ref vectorsStart, i));
        }
    }

    /// <summary>
    /// Unpremultiplies the RGB lanes of a vector while preserving its alpha lane.
    /// When alpha is zero, the RGB lanes remain unchanged because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The associated vector.</param>
    /// <param name="alpha">The source alpha replicated to every lane.</param>
    /// <returns>The unassociated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> UnPremultiply(Vector128<float> source, Vector128<float> alpha)
    {
        // Zero alpha has no mathematical inverse, so select the source lanes to preserve stored additive or hidden RGB data.
        Vector128<float> zeroMask = Vector128.Equals(alpha, Vector128<float>.Zero);
        Vector128<float> result = Vector128.ConditionalSelect(zeroMask, source, source / alpha);

        // Division would replace W with one, so restore the original alpha lane exactly.
        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
        return Vector128.ConditionalSelect(alphaMask, alpha, result);
    }

    /// <summary>
    /// Unpremultiplies the RGB lanes of two vectors while preserving their alpha lanes.
    /// Vectors with zero alpha retain their RGB lanes because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The associated vectors.</param>
    /// <param name="alpha">Each source alpha replicated across its four lanes.</param>
    /// <returns>The unassociated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> UnPremultiply(Vector256<float> source, Vector256<float> alpha)
    {
        // Zero alpha has no mathematical inverse, so select the source lanes to preserve stored additive or hidden RGB data.
        Vector256<float> zeroMask = Avx.CompareEqual(alpha, Vector256<float>.Zero);
        Vector256<float> result = Avx.BlendVariable(Avx.Divide(source, alpha), source, zeroMask);

        // Division would replace W with one, so restore both original alpha lanes exactly.
        return Avx.Blend(result, alpha, BlendAlphaControl);
    }

    /// <summary>
    /// Unpremultiplies the RGB lanes of four vectors while preserving their alpha lanes.
    /// Vectors with zero alpha retain their RGB lanes because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The associated vectors.</param>
    /// <param name="alpha">Each source alpha replicated across its four lanes.</param>
    /// <returns>The unassociated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> UnPremultiply(Vector512<float> source, Vector512<float> alpha)
    {
        // Zero alpha has no mathematical inverse, so select the source lanes to preserve stored additive or hidden RGB data.
        Vector512<float> zeroMask = Vector512.Equals(alpha, Vector512<float>.Zero);
        Vector512<float> result = Vector512.ConditionalSelect(zeroMask, source, source / alpha);

        // Division would replace W with one, so restore all four original alpha lanes exactly.
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector512.ConditionalSelect(alphaMask, alpha, result);
    }

    /// <summary>
    /// Permutes the given vector return a new instance with all the values set to <see cref="Vector4.W"/>.
    /// </summary>
    /// <param name="value">The vector.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 PermuteW(Vector4 value)
    {
        if (Sse.IsSupported)
        {
            return Sse.Shuffle(value.AsVector128(), value.AsVector128(), ShuffleAlphaControl).AsVector4();
        }

        return new Vector4(value.W);
    }

    /// <summary>
    /// Sets the W component of the given vector <paramref name="value"/> to the given value from <paramref name="w"/>.
    /// </summary>
    /// <param name="value">The vector to set.</param>
    /// <param name="w">The vector containing the W value.</param>
    /// <returns>The <see cref="Vector4"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 WithW(Vector4 value, Vector4 w)
    {
        if (Sse41.IsSupported)
        {
            return Sse41.Insert(value.AsVector128(), w.AsVector128(), 0b11_11_0000).AsVector4();
        }

        if (Sse.IsSupported)
        {
            // Create tmp as <w[3], w[0], value[2], value[0]>
            // Then return <value[0], value[1], tmp[2], tmp[0]> (which is <value[0], value[1], value[2], w[3]>)
            Vector128<float> tmp = Sse.Shuffle(w.AsVector128(), value.AsVector128(), 0b00_10_00_11);
            return Sse.Shuffle(value.AsVector128(), tmp, 0b00_10_01_00).AsVector4();
        }

        value.W = w.W;
        return value;
    }

    /// <summary>
    /// Calculates the cube pow of all the XYZ channels of the input vectors.
    /// </summary>
    /// <param name="vectors">The span of vectors</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CubePowOnXYZ(Span<Vector4> vectors)
    {
        ref Vector4 baseRef = ref MemoryMarshal.GetReference(vectors);
        ref Vector4 endRef = ref Unsafe.Add(ref baseRef, (uint)vectors.Length);

        while (Unsafe.IsAddressLessThan(ref baseRef, ref endRef))
        {
            Vector4 v = baseRef;
            Vector4 a = PermuteW(v);

            // Fast path for the default gamma exposure, which is 3. In this case we can skip
            // calling Math.Pow 3 times (one per component), as the method is an internal call and
            // introduces quite a bit of overhead. Instead, we can just manually multiply the whole
            // pixel in Vector4 format 3 times, and then restore the alpha channel before copying it
            // back to the target index in the temporary span. The whole iteration will get completely
            // inlined and traslated into vectorized instructions, with much better performance.
            v = v * v * v;
            v = WithW(v, a);

            baseRef = v;
            baseRef = ref Unsafe.Add(ref baseRef, 1);
        }
    }

    /// <summary>
    /// Calculates the cube root of all the XYZ channels of the input vectors.
    /// </summary>
    /// <param name="vectors">The span of vectors</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void CubeRootOnXYZ(Span<Vector4> vectors)
    {
        if (Sse41.IsSupported)
        {
            ref Vector128<float> vectors128Ref = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(vectors));
            ref Vector128<float> vectors128End = ref Unsafe.Add(ref vectors128Ref, (uint)vectors.Length);

            Vector128<int> v128_341 = Vector128.Create(341);
            Vector128<int> v128_negativeZero = Vector128.Create(-0.0f).AsInt32();
            Vector128<int> v128_one = Vector128.Create(1.0f).AsInt32();

            Vector128<float> v128_13rd = Vector128.Create(1 / 3f);
            Vector128<float> v128_23rds = Vector128.Create(2 / 3f);

            while (Unsafe.IsAddressLessThan(ref vectors128Ref, ref vectors128End))
            {
                Vector128<float> vecx = vectors128Ref;
                Vector128<int> veax = vecx.AsInt32();

                // If we can use SSE41 instructions, we can vectorize the entire cube root calculation, and also execute it
                // directly on 32 bit floating point values. What follows is a vectorized implementation of this method:
                // https://www.musicdsp.org/en/latest/Other/206-fast-cube-root-square-root-and-reciprocal-for-x86-sse-cpus.html.
                // Furthermore, after the initial setup in vectorized form, we're doing two Newton approximations here
                // using a different succession (the same used below), which should be less unstable due to not having cube pow.
                veax = Sse2.AndNot(v128_negativeZero, veax);
                veax = Sse2.Subtract(veax, v128_one);
                veax = Sse2.ShiftRightArithmetic(veax, 10);
                veax = Sse41.MultiplyLow(veax, v128_341);
                veax = Sse2.Add(veax, v128_one);
                veax = Sse2.AndNot(v128_negativeZero, veax);
                veax = Sse2.Or(veax, Sse2.And(vecx.AsInt32(), v128_negativeZero));

                Vector128<float> y4 = veax.AsSingle();

                if (Fma.IsSupported)
                {
                    y4 = Fma.MultiplyAdd(v128_23rds, y4, Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                    y4 = Fma.MultiplyAdd(v128_23rds, y4, Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                }
                else
                {
                    y4 = Sse.Add(Sse.Multiply(v128_23rds, y4), Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                    y4 = Sse.Add(Sse.Multiply(v128_23rds, y4), Sse.Multiply(v128_13rd, Sse.Divide(vecx, Sse.Multiply(y4, y4))));
                }

                y4 = Sse41.Insert(y4, vecx, 0xF0);

                vectors128Ref = y4;
                vectors128Ref = ref Unsafe.Add(ref vectors128Ref, 1);
            }
        }
        else
        {
            ref Vector4 vectorsRef = ref MemoryMarshal.GetReference(vectors);
            ref Vector4 vectorsEnd = ref Unsafe.Add(ref vectorsRef, (uint)vectors.Length);

            // Fallback with scalar preprocessing and vectorized approximation steps
            while (Unsafe.IsAddressLessThan(ref vectorsRef, ref vectorsEnd))
            {
                Vector4 v = vectorsRef;

                double
                    x64 = v.X,
                    y64 = v.Y,
                    z64 = v.Z;
                float a = v.W;

                ulong
                    xl = *(ulong*)&x64,
                    yl = *(ulong*)&y64,
                    zl = *(ulong*)&z64;

                // Here we use a trick to compute the starting value x0 for the cube root. This is because doing
                // pow(x, 1 / gamma) is the same as the gamma-th root of x, and since gamme is 3 in this case,
                // this means what we actually want is to find the cube root of our clamped values.
                // For more info on the  constant below, see:
                // https://community.intel.com/t5/Intel-C-Compiler/Fast-approximate-of-transcendental-operations/td-p/1044543.
                // Here we perform the same trick on all RGB channels separately to help the CPU execute them in paralle, and
                // store the alpha channel to preserve it. Then we set these values to the fields of a temporary 128-bit
                // register, and use it to accelerate two steps of the Newton approximation using SIMD.
                xl = 0x2a9f8a7be393b600 + (xl / 3);
                yl = 0x2a9f8a7be393b600 + (yl / 3);
                zl = 0x2a9f8a7be393b600 + (zl / 3);

                Vector4 y4;
                y4.X = (float)*(double*)&xl;
                y4.Y = (float)*(double*)&yl;
                y4.Z = (float)*(double*)&zl;
                y4.W = 0;

                y4 = (2 / 3f * y4) + (1 / 3f * (v / (y4 * y4)));
                y4 = (2 / 3f * y4) + (1 / 3f * (v / (y4 * y4)));
                y4.W = a;

                vectorsRef = y4;
                vectorsRef = ref Unsafe.Add(ref vectorsRef, 1);
            }
        }
    }

    /// <summary>
    /// Performs a linear interpolation between two values based on the given weighting.
    /// </summary>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="amount">Values between 0 and 1 that indicates the weight of <paramref name="value2"/>.</param>
    /// <returns>The <see cref="Vector256{Single}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Lerp(
        in Vector256<float> value1,
        in Vector256<float> value2,
        in Vector256<float> amount)
    {
        Vector256<float> diff = Avx.Subtract(value2, value1);
        if (Fma.IsSupported)
        {
            return Fma.MultiplyAdd(diff, amount, value1);
        }
        else
        {
            return Avx.Add(Avx.Multiply(diff, amount), value1);
        }
    }

    /// <summary>
    /// Performs a linear interpolation between two values based on the given weighting.
    /// </summary>
    /// <param name="value1">The first value.</param>
    /// <param name="value2">The second value.</param>
    /// <param name="amount">A value between 0 and 1 that indicates the weight of <paramref name="value2"/>.</param>
    /// <returns>The <see cref="float"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float value1, float value2, float amount)
        => ((value2 - value1) * amount) + value1;

    /// <summary>
    /// Accumulates 8-bit integers into <paramref name="accumulator"/> by
    /// widening them to 32-bit integers and performing four additions.
    /// </summary>
    /// <remarks>
    /// <c>byte(1, 2, 3, 4,  5, 6, 7, 8,  9, 10, 11, 12,  13, 14, 15, 16)</c>
    /// is widened and added onto <paramref name="accumulator"/> as such:
    /// <code>
    ///  accumulator += i32(1, 2, 3, 4);
    ///  accumulator += i32(5, 6, 7, 8);
    ///  accumulator += i32(9, 10, 11, 12);
    ///  accumulator += i32(13, 14, 15, 16);
    /// </code>
    /// </remarks>
    /// <param name="accumulator">The accumulator destination.</param>
    /// <param name="values">The values to accumulate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Accumulate(ref Vector<uint> accumulator, Vector<byte> values)
    {
        Vector.Widen(values, out Vector<ushort> shortLow, out Vector<ushort> shortHigh);

        Vector.Widen(shortLow, out Vector<uint> intLow, out Vector<uint> intHigh);
        accumulator += intLow;
        accumulator += intHigh;

        Vector.Widen(shortHigh, out intLow, out intHigh);
        accumulator += intLow;
        accumulator += intHigh;
    }

    /// <summary>
    /// Reduces elements of the vector into one sum.
    /// </summary>
    /// <param name="accumulator">The accumulator to reduce.</param>
    /// <returns>The sum of all elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReduceSum(Vector256<int> accumulator)
    {
        // Add upper lane to lower lane.
        Vector128<int> vsum = Sse2.Add(accumulator.GetLower(), accumulator.GetUpper());

        // Add odd to even.
        vsum = Sse2.Add(vsum, Sse2.Shuffle(vsum, 0b_11_11_01_01));

        // Add high to low.
        vsum = Sse2.Add(vsum, Sse2.Shuffle(vsum, 0b_11_10_11_10));

        return Sse2.ConvertToInt32(vsum);
    }

    /// <summary>
    /// Reduces even elements of the vector into one sum.
    /// </summary>
    /// <param name="accumulator">The accumulator to reduce.</param>
    /// <returns>The sum of even elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EvenReduceSum(Vector128<int> accumulator)
    {
        // Add high to low.
        Vector128<int> vsum = Sse2.Add(accumulator, Sse2.Shuffle(accumulator, 0b_11_10_11_10));

        return Sse2.ConvertToInt32(vsum);
    }

    /// <summary>
    /// Reduces even elements of the vector into one sum.
    /// </summary>
    /// <param name="accumulator">The accumulator to reduce.</param>
    /// <returns>The sum of even elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EvenReduceSum(Vector256<int> accumulator)
    {
        Vector128<int> vsum = Sse2.Add(accumulator.GetLower(), accumulator.GetUpper()); // add upper lane to lower lane
        vsum = Sse2.Add(vsum, Sse2.Shuffle(vsum, 0b_11_10_11_10));                      // add high to low

        // Vector128<int>.ToScalar() isn't optimized pre-net5.0 https://github.com/dotnet/runtime/pull/37882
        return Sse2.ConvertToInt32(vsum);
    }

    /// <summary>
    /// Fast division with ceiling for <see cref="uint"/> numbers.
    /// </summary>
    /// <param name="value">Divident value.</param>
    /// <param name="divisor">Divisor value.</param>
    /// <returns>Ceiled division result.</returns>
    public static uint DivideCeil(uint value, uint divisor) => (value + divisor - 1) / divisor;

    /// <summary>
    /// Tells whether input value is outside of the given range.
    /// </summary>
    /// <param name="value">Value.</param>
    /// <param name="min">Minimum value, inclusive.</param>
    /// <param name="max">Maximum value, inclusive.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOutOfRange(int value, int min, int max)
        => (uint)(value - min) > (uint)(max - min);

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint VectorCount<TVector>(this ReadOnlySpan<byte> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector128Count<TVector>(this ReadOnlySpan<byte> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector128<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector256Count<TVector>(this ReadOnlySpan<byte> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector256<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector512Count<TVector>(this ReadOnlySpan<byte> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector512<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint VectorCount<TVector>(this ReadOnlySpan<float> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector128Count<TVector>(this ReadOnlySpan<float> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector128<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector256Count<TVector>(this ReadOnlySpan<float> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector256<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into length.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="length">The given length.</param>
    /// <returns>Count of vectors that safely fit into the length.</returns>
    public static nuint Vector256Count<TVector>(int length)
        where TVector : struct
        => (uint)length / (uint)Vector256<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given span.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector512Count<TVector>(this ReadOnlySpan<float> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector512<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into length.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="length">The given length.</param>
    /// <returns>Count of vectors that safely fit into the length.</returns>
    public static nuint Vector512Count<TVector>(int length)
        where TVector : struct
        => (uint)length / (uint)Vector512<TVector>.Count;
}
