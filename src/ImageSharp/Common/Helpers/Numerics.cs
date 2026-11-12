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
    /// Gets the greatest common divisor of two nonnegative 64-bit values.
    /// </summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns>The greatest common divisor, or zero when both values are zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GreatestCommonDivisor(ulong a, ulong b)
    {
        // Euclid's remainder step preserves common divisors without a multiplication that can overflow.
        while (b != 0)
        {
            ulong remainder = a % b;
            a = b;
            b = remainder;
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
    /// Computes the natural logarithm of one plus a finite nonnegative value.
    /// </summary>
    /// <param name="value">The value to add to one.</param>
    /// <returns>The natural logarithm of one plus <paramref name="value"/>.</returns>
    public static double Log1P(double value)
    {
        // Adding one can discard low bits of value. Add their first-order contribution to the logarithm
        // so a small positive value does not become zero when the sum rounds to one.
        double sum = 1.0 + value;
        return Math.Log(sum) + ((value - (sum - 1.0)) / sum);
    }

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
    /// Clamps the associated RGB lanes of one RGBA vector to [0, alpha] while preserving the alpha lane.
    /// </summary>
    /// <param name="source">The associated vector to clamp.</param>
    /// <returns>The clamped vector. NaN RGB maps to zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> ClampRgbToAlpha(Vector128<float> source)
    {
        Vector128<float> alpha = Vector128_.ShuffleNative(source, ShuffleAlphaControl);

        // The clamp also bounds W by itself, so select the original W lane to preserve alpha bit-for-bit.
        Vector128<float> clamped = ColorNumerics.ClampToFinite(source, Vector128<float>.Zero, alpha);
        return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), source, clamped);
    }

    /// <summary>
    /// Premultiplies the X, Y, and Z components of one RGBA vector by its W component while preserving W.
    /// </summary>
    /// <param name="source">One RGBA vector.</param>
    /// <returns>The associated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Premultiply(Vector128<float> source)
    {
        Vector128<float> alpha = Vector128_.ShuffleNative(source, ShuffleAlphaControl);

        // Multiplication also squares W, so select the original W lane to preserve alpha bit-for-bit.
        return Vector128.ConditionalSelect(Vector128.Create(0, 0, 0, -1).AsSingle(), source, source * alpha);
    }

    /// <summary>
    /// Premultiplies the X, Y, and Z components of two RGBA vectors by their W components while preserving W.
    /// </summary>
    /// <param name="source">Two RGBA vectors.</param>
    /// <returns>The associated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Premultiply(Vector256<float> source)
    {
        // The shuffle replicates W inside each 128-bit lane, so each pixel uses its own alpha.
        Vector256<float> alpha = Vector256_.ShuffleNative(source, ShuffleAlphaControl);
        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

        // Multiplication also squares W, so select the original W lanes to preserve alpha bit-for-bit.
        return Vector256.ConditionalSelect(alphaMask, source, source * alpha);
    }

    /// <summary>
    /// Premultiplies the X, Y, and Z components of four RGBA vectors by their W components while preserving W.
    /// </summary>
    /// <param name="source">Four RGBA vectors.</param>
    /// <returns>The associated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Premultiply(Vector512<float> source)
    {
        // The shuffle replicates W inside each 128-bit lane, so each pixel uses its own alpha.
        Vector512<float> alpha = Vector512_.ShuffleNative(source, ShuffleAlphaControl);
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();

        // Multiplication also squares W, so select the original W lanes to preserve alpha bit-for-bit.
        return Vector512.ConditionalSelect(alphaMask, source, source * alpha);
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

            while (Unsafe.IsAddressLessThan(ref vectorsBase, ref vectorsEnd))
            {
                vectorsBase = Premultiply(vectorsBase);
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
                vectorsBase = Premultiply(vectorsBase);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            vectors = vectors[(vectors.Length - (vectors.Length % vectorsPerVector))..];
        }

        // The 128-bit form holds one vector. It serves the final vectors and the path without hardware acceleration,
        // so each vector uses the same arithmetic at every width.
        ref Vector128<float> remainderBase = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(vectors));
        ref Vector128<float> remainderEnd = ref Unsafe.Add(ref remainderBase, (uint)vectors.Length);

        while (Unsafe.IsAddressLessThan(ref remainderBase, ref remainderEnd))
        {
            remainderBase = Premultiply(remainderBase);
            remainderBase = ref Unsafe.Add(ref remainderBase, 1);
        }
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
                vectorsBase = UnPremultiply(vectorsBase);
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
                vectorsBase = UnPremultiply(vectorsBase);
                vectorsBase = ref Unsafe.Add(ref vectorsBase, 1);
            }

            vectors = vectors[(vectors.Length - (vectors.Length % vectorsPerVector))..];
        }

        // The 128-bit form holds one vector. It serves the final vectors and the path without hardware acceleration,
        // so each vector uses the same arithmetic at every width.
        ref Vector128<float> remainderBase = ref Unsafe.As<Vector4, Vector128<float>>(ref MemoryMarshal.GetReference(vectors));
        ref Vector128<float> remainderEnd = ref Unsafe.Add(ref remainderBase, (uint)vectors.Length);

        while (Unsafe.IsAddressLessThan(ref remainderBase, ref remainderEnd))
        {
            remainderBase = UnPremultiply(remainderBase);
            remainderBase = ref Unsafe.Add(ref remainderBase, 1);
        }
    }

    /// <summary>
    /// Unpremultiplies the RGB lanes of one RGBA vector by its alpha lane while preserving the alpha lane.
    /// When alpha is zero, the RGB lanes remain unchanged because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The associated vector.</param>
    /// <returns>The unassociated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> UnPremultiply(Vector128<float> source)
        => UnPremultiply(source, Vector128_.ShuffleNative(source, ShuffleAlphaControl), 1F);

    /// <summary>
    /// Unpremultiplies the RGB lanes of two RGBA vectors by their alpha lanes while preserving the alpha lanes.
    /// When alpha is zero, the RGB lanes remain unchanged because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The associated vectors.</param>
    /// <returns>The unassociated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> UnPremultiply(Vector256<float> source)
        => UnPremultiply(source, Vector256_.ShuffleNative(source, ShuffleAlphaControl), 1F);

    /// <summary>
    /// Unpremultiplies the RGB lanes of four RGBA vectors by their alpha lanes while preserving the alpha lanes.
    /// When alpha is zero, the RGB lanes remain unchanged because no unassociated value can be recovered.
    /// </summary>
    /// <param name="source">The associated vectors.</param>
    /// <returns>The unassociated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> UnPremultiply(Vector512<float> source)
        => UnPremultiply(source, Vector512_.ShuffleNative(source, ShuffleAlphaControl), 1F);

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
        // The result keeps the supplied alpha. Write it to the alpha lane first, as the shared rule requires.
        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
        return UnPremultiply(Vector128.ConditionalSelect(alphaMask, alpha, source), alpha, 1F);
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
        // The result keeps the supplied alpha. Write it to the alpha lanes first, as the shared rule requires.
        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return UnPremultiply(Vector256.ConditionalSelect(alphaMask, alpha, source), alpha, 1F);
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
        // The result keeps the supplied alpha. Write it to the alpha lanes first, as the shared rule requires.
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return UnPremultiply(Vector512.ConditionalSelect(alphaMask, alpha, source), alpha, 1F);
    }

    /// <summary>
    /// Unpremultiplies the RGB lanes of one RGBA vector by its alpha lane, and divides the result to normalized units.
    /// </summary>
    /// <remarks>
    /// The rule is the rule of <see cref="UnPremultiply(Vector128{float})"/> applied to <paramref name="source"/> divided by
    /// <paramref name="maximum"/>. Both divisions are one division, so each lane rounds once:
    /// RGB divided by alpha is already normalized, and alpha, and RGB at zero alpha, are divided by <paramref name="maximum"/>.
    /// This keeps exact integer results away from rounding midpoints.
    /// </remarks>
    /// <param name="source">The associated vector, with alpha in [0, <paramref name="maximum"/>].</param>
    /// <param name="maximum">The alpha of full opacity.</param>
    /// <returns>The unassociated vector in normalized units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> UnPremultiply(Vector128<float> source, float maximum)
        => UnPremultiply(source, Vector128_.ShuffleNative(source, ShuffleAlphaControl), maximum);

    /// <summary>
    /// Unpremultiplies the RGB lanes of two RGBA vectors by their alpha lanes, and divides the results to normalized units.
    /// </summary>
    /// <remarks>
    /// The rule is the rule of <see cref="UnPremultiply(Vector128{float}, float)"/>.
    /// </remarks>
    /// <param name="source">The associated vectors, with alpha in [0, <paramref name="maximum"/>].</param>
    /// <param name="maximum">The alpha of full opacity.</param>
    /// <returns>The unassociated vectors in normalized units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> UnPremultiply(Vector256<float> source, float maximum)
        => UnPremultiply(source, Vector256_.ShuffleNative(source, ShuffleAlphaControl), maximum);

    /// <summary>
    /// Unpremultiplies the RGB lanes of four RGBA vectors by their alpha lanes, and divides the results to normalized units.
    /// </summary>
    /// <remarks>
    /// The rule is the rule of <see cref="UnPremultiply(Vector128{float}, float)"/>.
    /// </remarks>
    /// <param name="source">The associated vectors, with alpha in [0, <paramref name="maximum"/>].</param>
    /// <param name="maximum">The alpha of full opacity.</param>
    /// <returns>The unassociated vectors in normalized units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> UnPremultiply(Vector512<float> source, float maximum)
        => UnPremultiply(source, Vector512_.ShuffleNative(source, ShuffleAlphaControl), maximum);

    /// <summary>
    /// Divides RGB by alpha and divides alpha by the maximum. Every unpremultiply form of one RGBA vector uses this rule.
    /// </summary>
    /// <remarks>
    /// The alpha lane of <paramref name="source"/> must equal <paramref name="alpha"/>. The public forms set this lane.
    /// A maximum of one keeps alpha, and RGB at zero alpha, unchanged, because a division by one is exact.
    /// </remarks>
    /// <param name="source">The associated vector, with the alpha lane equal to <paramref name="alpha"/>.</param>
    /// <param name="alpha">The source alpha replicated to every lane.</param>
    /// <param name="maximum">The alpha of full opacity.</param>
    /// <returns>The unassociated vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> UnPremultiply(Vector128<float> source, Vector128<float> alpha, float maximum)
    {
        // Zero alpha has no mathematical inverse. Its RGB lanes use the maximum as divisor, so the stored RGB is kept.
        // The all-ones lane 3 also selects the maximum, so alpha itself is only normalized.
        Vector128<float> alphaMask = Vector128.Create(0, 0, 0, -1).AsSingle();
        Vector128<float> maximumMask = Vector128.Equals(alpha, Vector128<float>.Zero) | alphaMask;
        return source / Vector128.ConditionalSelect(maximumMask, Vector128.Create(maximum), alpha);
    }

    /// <summary>
    /// Divides RGB by alpha and divides alpha by the maximum for two RGBA vectors.
    /// </summary>
    /// <remarks>
    /// The contract is the contract of <see cref="UnPremultiply(Vector128{float}, Vector128{float}, float)"/>.
    /// </remarks>
    /// <param name="source">The associated vectors, with the alpha lanes equal to <paramref name="alpha"/>.</param>
    /// <param name="alpha">Each source alpha replicated across its four lanes.</param>
    /// <param name="maximum">The alpha of full opacity.</param>
    /// <returns>The unassociated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> UnPremultiply(Vector256<float> source, Vector256<float> alpha, float maximum)
    {
        // Each four-lane RGBA group uses its own alpha. The all-ones lanes 3 and 7 select the maximum for alpha itself.
        Vector256<float> alphaMask = Vector256.Create(0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        Vector256<float> maximumMask = Vector256.Equals(alpha, Vector256<float>.Zero) | alphaMask;
        return source / Vector256.ConditionalSelect(maximumMask, Vector256.Create(maximum), alpha);
    }

    /// <summary>
    /// Divides RGB by alpha and divides alpha by the maximum for four RGBA vectors.
    /// </summary>
    /// <remarks>
    /// The contract is the contract of <see cref="UnPremultiply(Vector128{float}, Vector128{float}, float)"/>.
    /// </remarks>
    /// <param name="source">The associated vectors, with the alpha lanes equal to <paramref name="alpha"/>.</param>
    /// <param name="alpha">Each source alpha replicated across its four lanes.</param>
    /// <param name="maximum">The alpha of full opacity.</param>
    /// <returns>The unassociated vectors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> UnPremultiply(Vector512<float> source, Vector512<float> alpha, float maximum)
    {
        // Each four-lane RGBA group uses its own alpha. The all-ones lanes 3, 7, 11 and 15 select the maximum for alpha itself.
        Vector512<float> alphaMask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        Vector512<float> maximumMask = Vector512.Equals(alpha, Vector512<float>.Zero) | alphaMask;
        return source / Vector512.ConditionalSelect(maximumMask, Vector512.Create(maximum), alpha);
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
    /// Replaces the fourth component in each four-component group without changing the other component bits.
    /// </summary>
    /// <param name="value">The vectors to update.</param>
    /// <param name="w">The vectors that supply each fourth component.</param>
    /// <returns>The vectors with their fourth components replaced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> WithW(Vector128<float> value, Vector128<float> w)
    {
        // Select only W in each four-lane group. This retains signed zeros and NaN payload bits in all other lanes.
        Vector128<float> mask = Vector128.Create(0, 0, 0, -1).AsSingle();
        return Vector128.ConditionalSelect(mask, w, value);
    }

    /// <summary>
    /// Replaces the fourth component in each four-component group without changing the other component bits.
    /// </summary>
    /// <param name="value">The vectors to update.</param>
    /// <param name="w">The vectors that supply each fourth component.</param>
    /// <returns>The vectors with their fourth components replaced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> WithW(Vector256<float> value, Vector256<float> w)
    {
        // Lanes 3 and 7 are W. The immediate blend preserves every other lane without arithmetic.
        return Avx.Blend(value, w, BlendAlphaControl);
    }

    /// <summary>
    /// Replaces the fourth component in each four-component group without changing the other component bits.
    /// </summary>
    /// <param name="value">The vectors to update.</param>
    /// <param name="w">The vectors that supply each fourth component.</param>
    /// <returns>The vectors with their fourth components replaced.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> WithW(Vector512<float> value, Vector512<float> w)
    {
        // Select only W in each four-lane group. This retains signed zeros and NaN payload bits in all other lanes.
        Vector512<float> mask = Vector512.Create(0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1, 0, 0, 0, -1).AsSingle();
        return Vector512.ConditionalSelect(mask, w, value);
    }

    /// <summary>
    /// Replaces the fourth component of each destination vector with the corresponding source value.
    /// All other component bits remain unchanged.
    /// </summary>
    /// <param name="destination">The vectors to update. Their storage must not overlap <paramref name="w"/>.</param>
    /// <param name="w">The fourth components. The span must contain at least one value per destination vector.</param>
    public static void WithW(Span<Vector4> destination, ReadOnlySpan<float> w)
    {
        Guard.MustBeGreaterThanOrEqualTo(w.Length, destination.Length, nameof(w));
        ref float weights = ref MemoryMarshal.GetReference(w);
        ref float output = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int vectorsPerRegister = Vector512<float>.Count / Vector128<float>.Count;
            int count = destination.Length / vectorsPerRegister;
            Vector512<int> repeatWeights = Vector512.Create(0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3);
            for (int block = 0; block < count; block++, i += vectorsPerRegister)
            {
                // Four source values describe four consecutive [X,Y,Z,W] groups. Repeat each value
                // across its group, then let WithW select only lanes 3, 7, 11 and 15. No arithmetic touches RGB.
                Vector128<float> input = Vector128.LoadUnsafe(ref weights, (nuint)i);
                Vector256<float> pair = Vector256.Create(input, input);
                Vector512<float> expanded = Vector512.ShuffleNative(Vector512.Create(pair, pair), repeatWeights);
                nuint offset = (nuint)(uint)i * (nuint)Vector128<float>.Count;
                Vector512<float> value = Vector512.LoadUnsafe(ref output, offset);
                WithW(value, expanded).StoreUnsafe(ref output, offset);
            }
        }

        // WithW's 256-bit overload uses the existing AVX blend. Keep this gate at its actual instruction requirement.
        if (Avx.IsSupported)
        {
            int vectorsPerRegister = Vector256<float>.Count / Vector128<float>.Count;
            int count = (destination.Length - i) / vectorsPerRegister;
            const byte repeatFirst = 0b_00_00_00_00;
            const byte repeatSecond = 0b_01_01_01_01;
            for (int block = 0; block < count; block++, i += vectorsPerRegister)
            {
                // Read exactly two W values. Place W0 in the low half and W1 in the high half,
                // then repeat each half's first lane. This needs AVX, without an AVX2 permutation dependency.
                Vector128<float> input = Vector64.LoadUnsafe(ref weights, (nuint)i).ToVector128();
                Vector256<float> pair = Vector256.Create(input, Vector128_.ShuffleNative(input, repeatSecond));
                Vector256<float> expanded = Vector256_.ShuffleNative(pair, repeatFirst);
                nuint offset = (nuint)(uint)i * (nuint)Vector128<float>.Count;
                Vector256<float> value = Vector256.LoadUnsafe(ref output, offset);
                WithW(value, expanded).StoreUnsafe(ref output, offset);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i < destination.Length; i++)
            {
                // One source value supplies lane 3 of one complete vector. Unaligned access needs no row padding.
                nuint offset = (nuint)(uint)i * (nuint)Vector128<float>.Count;
                Vector128<float> value = Vector128.LoadUnsafe(ref output, offset);
                WithW(value, Vector128.Create(Unsafe.Add(ref weights, i))).StoreUnsafe(ref output, offset);
            }
        }

        // Scalar assignment has the same bit-preserving contract, including NaN payloads and signed zero.
        for (; i < destination.Length; i++)
        {
            destination[i].W = w[i];
        }
    }

    /// <summary>
    /// Replaces the fourth component of every destination vector with one value.
    /// All other component bits remain unchanged.
    /// </summary>
    /// <param name="destination">The vectors to update.</param>
    /// <param name="w">The fourth component to store in every vector.</param>
    public static void WithW(Span<Vector4> destination, float w)
    {
        ref float output = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int vectorsPerRegister = Vector512<float>.Count / Vector128<float>.Count;
            int count = destination.Length / vectorsPerRegister;
            Vector512<float> expanded = Vector512.Create(w);
            for (int block = 0; block < count; block++, i += vectorsPerRegister)
            {
                // Four complete vectors share the same W. The shared selector retains all twelve other component bit patterns.
                nuint offset = (nuint)(uint)i * (nuint)Vector128<float>.Count;
                Vector512<float> value = Vector512.LoadUnsafe(ref output, offset);
                WithW(value, expanded).StoreUnsafe(ref output, offset);
            }
        }

        if (Avx.IsSupported)
        {
            int vectorsPerRegister = Vector256<float>.Count / Vector128<float>.Count;
            int count = (destination.Length - i) / vectorsPerRegister;
            Vector256<float> expanded = Vector256.Create(w);
            for (int block = 0; block < count; block++, i += vectorsPerRegister)
            {
                // Two complete vectors share W in lanes 3 and 7. Only the selected components are written.
                nuint offset = (nuint)(uint)i * (nuint)Vector128<float>.Count;
                Vector256<float> value = Vector256.LoadUnsafe(ref output, offset);
                WithW(value, expanded).StoreUnsafe(ref output, offset);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> expanded = Vector128.Create(w);
            for (; i < destination.Length; i++)
            {
                nuint offset = (nuint)(uint)i * (nuint)Vector128<float>.Count;
                Vector128<float> value = Vector128.LoadUnsafe(ref output, offset);
                WithW(value, expanded).StoreUnsafe(ref output, offset);
            }
        }

        // Fewer complete SIMD groups, or all vectors without SIMD, retain the same scalar assignment rule.
        for (; i < destination.Length; i++)
        {
            destination[i].W = w;
        }
    }

    /// <summary>
    /// Replaces the first three components of each destination vector with the corresponding source vector.
    /// All fourth-component bits remain unchanged.
    /// </summary>
    /// <param name="xyz">The first three components to copy, without arithmetic or range conversion.</param>
    /// <param name="destination">The vectors to update. Its length must cover the source, and its storage must not overlap the source.</param>
    public static void WithXYZ(ReadOnlySpan<Vector3> xyz, Span<Vector4> destination)
    {
        Guard.DestinationShouldNotBeTooShort(xyz, destination, nameof(destination));
        ref float input = ref Unsafe.As<Vector3, float>(ref MemoryMarshal.GetReference(xyz));
        ref float output = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
        int count = xyz.Length;
        int i = 0;

        if (Avx512F.IsSupported)
        {
            // Four source vectors occupy twelve floats. A sixteen-float load includes the next source vectors,
            // so retain the last complete group for smaller widths. The permutation inserts unused W positions;
            // WithW replaces those positions with the destination's original W bits after loading both inputs.
            Vector512<int> insertW = Vector512.Create(0, 1, 2, 0, 3, 4, 5, 0, 6, 7, 8, 0, 9, 10, 11, 0);
            int groups = (count >> 2) - 1;
            for (int block = 0; block < groups; block++)
            {
                Vector512<float> current = Vector512.LoadUnsafe(ref output);
                Vector512<float> source = Vector512.LoadUnsafe(ref input);
                Vector512<float> expanded = Avx512F.PermuteVar16x32(source, insertW);
                WithW(expanded, current).StoreUnsafe(ref output);
                input = ref Unsafe.Add(ref input, 12U);
                output = ref Unsafe.Add(ref output, 16U);
                i += 4;
            }
        }
        else if (Avx2.IsSupported)
        {
            // Two source vectors occupy six floats. Keep the last pair for smaller widths because the
            // eight-float load also reads the next vector. The immediate W selector preserves lanes 3 and 7.
            Vector256<int> insertW = Vector256.Create(0, 1, 2, 0, 3, 4, 5, 0);
            int groups = (count >> 1) - 1;
            for (int block = 0; block < groups; block++)
            {
                Vector256<float> current = Vector256.LoadUnsafe(ref output);
                Vector256<float> source = Vector256.LoadUnsafe(ref input);
                Vector256<float> expanded = Avx2.PermuteVar8x32(source, insertW);
                WithW(expanded, current).StoreUnsafe(ref output);
                input = ref Unsafe.Add(ref input, 6U);
                output = ref Unsafe.Add(ref output, 8U);
                i += 2;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            // A four-float load reads [X,Y,Z,next X]. At least two source vectors remain at each load,
            // so the extra float is within the source span. Replace it with the existing W and leave one scalar vector.
            for (; i < count - 1; i++)
            {
                Vector128<float> current = Vector128.LoadUnsafe(ref output);
                Vector128<float> source = Vector128.LoadUnsafe(ref input);
                WithW(source, current).StoreUnsafe(ref output);
                input = ref Unsafe.Add(ref input, 3U);
                output = ref Unsafe.Add(ref output, 4U);
            }
        }

        // A three-float assignment cannot overwrite W or read a fourth source float. It retains NaN payloads
        // and signed zeros exactly, including the complete fallback when hardware intrinsics are disabled.
        ref Vector4 destinationStart = ref MemoryMarshal.GetReference(destination);
        for (; i < count; i++)
        {
            Unsafe.As<Vector4, Vector3>(ref Unsafe.Add(ref destinationStart, (uint)i)) = xyz[i];
        }
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
    public static nuint Vector256Count<TVector>(this int length)
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
    /// Gets the count of vectors that safely fit into a span whose element type matches the vector lane type.
    /// </summary>
    /// <typeparam name="TVector">The type of the span elements and vector lanes.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector128Count<TVector>(this ReadOnlySpan<TVector> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector128<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into a span whose element type matches the vector lane type.
    /// </summary>
    /// <typeparam name="TVector">The type of the span elements and vector lanes.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector256Count<TVector>(this ReadOnlySpan<TVector> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector256<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into a span whose element type matches the vector lane type.
    /// </summary>
    /// <typeparam name="TVector">The type of the span elements and vector lanes.</typeparam>
    /// <param name="span">The given span.</param>
    /// <returns>Count of vectors that safely fit into the span.</returns>
    public static nuint Vector512Count<TVector>(this ReadOnlySpan<TVector> span)
        where TVector : struct
        => (uint)span.Length / (uint)Vector512<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into the given length.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="length">The given length.</param>
    /// <returns>Count of vectors that safely fit into the length.</returns>
    public static nuint Vector128Count<TVector>(this int length)
        where TVector : struct
        => (uint)length / (uint)Vector128<TVector>.Count;

    /// <summary>
    /// Gets the count of vectors that safely fit into length.
    /// </summary>
    /// <typeparam name="TVector">The type of the vector.</typeparam>
    /// <param name="length">The given length.</param>
    /// <returns>Count of vectors that safely fit into the length.</returns>
    public static nuint Vector512Count<TVector>(this int length)
        where TVector : struct
        => (uint)length / (uint)Vector512<TVector>.Count;
}
