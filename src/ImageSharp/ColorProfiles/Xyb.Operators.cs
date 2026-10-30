// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <content>
/// Provides the shared forward and inverse XYB arithmetic. Each SIMD lane is one independent color.
/// Matrices and cube-root biases are prepared before traversal. Codec rows and profile conversions use these same equations.
/// </content>
public readonly partial struct Xyb
{
    /// <summary>
    /// The IEEE single-precision mask that removes the sign bit before root normalization.
    /// </summary>
    private const uint MagnitudeMask = 0x7FFFFFFFU;

    /// <summary>
    /// The IEEE single-precision fraction bits retained when rebuilding the normalized mantissa.
    /// </summary>
    private const uint MantissaMask = 0x007FFFFFU;

    /// <summary>
    /// The IEEE bit pattern of the smallest positive normal float; smaller magnitudes need exponent normalization.
    /// </summary>
    private const uint MinimumNormalBits = 0x00800000U;

    /// <summary>
    /// The number of fraction bits below the IEEE single-precision exponent field.
    /// </summary>
    private const int MantissaBits = 23;

    /// <summary>
    /// The bias added to an IEEE single-precision normal exponent.
    /// </summary>
    private const int ExponentBias = 127;

    /// <summary>
    /// The exact power-of-two shift that makes every nonzero subnormal float normal.
    /// </summary>
    private const int SubnormalExponentShift = 24;

    /// <summary>
    /// The exact multiplier used before extracting a subnormal input's exponent.
    /// </summary>
    private const float SubnormalScale = 1 << SubnormalExponentShift;

    /// <summary>
    /// The exponent offset divisible by three that makes all finite input exponents positive.
    /// The smallest subnormal exponent is -149, so the adjusted exponent range is [1,277].
    /// </summary>
    private const int ExponentOffset = 150;

    /// <summary>
    /// The cube-root exponent offset removed after dividing the adjusted exponent by three.
    /// </summary>
    private const int ExponentQuotientOffset = ExponentOffset / 3;

    /// <summary>
    /// The fixed-point reciprocal shift used for exact division by three over the adjusted exponent range.
    /// </summary>
    private const int ThirdShift = 16;

    /// <summary>
    /// Ceil(2^16/3), which gives floor(v/3) after shifting for every integer v in [1,277].
    /// </summary>
    private const int ThirdMultiplier = 21846;

    /// <summary>
    /// The native bit-pattern bias used to seed the reciprocal cube root of a normalized mantissa.
    /// </summary>
    private const int ReciprocalRootSeedBias = 0x54800000;

    /// <summary>
    /// The native exponent multiplier used by the reciprocal-root seed.
    /// </summary>
    private const int ReciprocalRootExponentMultiplier = 0x002AAAAA;

    /// <summary>
    /// The rounded single-precision one-third coefficient used by native root refinement.
    /// </summary>
    private const float OneThird = 1F / 3F;

    /// <summary>
    /// The rounded single-precision four-thirds coefficient used by native root refinement.
    /// </summary>
    private const float FourThirds = 4F / 3F;

    /// <summary>
    /// The number of native reciprocal-root iterations before its final correction.
    /// </summary>
    private const int ReciprocalRootIterations = 3;

    /// <summary>
    /// Applies the biased absorbance transform and opponent mix to one linear RGB sample.
    /// </summary>
    /// <param name="r">The finite red component in the pixel range.</param>
    /// <param name="g">The corresponding green component.</param>
    /// <param name="b">The corresponding blue component.</param>
    /// <param name="matrix">The intensity-scaled forward matrix in Vector3.Transform order.</param>
    /// <param name="biases">The signed inverse biases, subtracted before root evaluation.</param>
    /// <param name="cubeRootBiases">The signed cube roots added after root evaluation.</param>
    /// <param name="x">The X result.</param>
    /// <param name="y">The Y result.</param>
    /// <param name="blue">The B result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertFromRgb(
        float r,
        float g,
        float b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out float x,
        out float y,
        out float blue)
    {
        // Preserve native B+bias, then G, then R multiply-add order. Moving bias outside that chain changes rounding near black.
        float mixed0 = float.MultiplyAddEstimate(matrix.M31, b, -biases.X);
        mixed0 = float.MultiplyAddEstimate(matrix.M21, g, mixed0);
        mixed0 = float.MultiplyAddEstimate(matrix.M11, r, mixed0);
        float mixed1 = float.MultiplyAddEstimate(matrix.M32, b, -biases.Y);
        mixed1 = float.MultiplyAddEstimate(matrix.M22, g, mixed1);
        mixed1 = float.MultiplyAddEstimate(matrix.M12, r, mixed1);
        float mixed2 = float.MultiplyAddEstimate(matrix.M33, b, -biases.Z);
        mixed2 = float.MultiplyAddEstimate(matrix.M23, g, mixed2);
        mixed2 = float.MultiplyAddEstimate(matrix.M13, r, mixed2);
        mixed0 = CubeRootAndAdd(MathF.Max(0F, mixed0), cubeRootBiases.X);
        mixed1 = CubeRootAndAdd(MathF.Max(0F, mixed1), cubeRootBiases.Y);
        mixed2 = CubeRootAndAdd(MathF.Max(0F, mixed2), cubeRootBiases.Z);
        x = 0.5F * (mixed0 - mixed1);
        y = 0.5F * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>
    /// Transforms 4 independent RGB samples with the same scalar coefficients and native evaluation order.
    /// </summary>
    /// <param name="r">The linear red lanes.</param>
    /// <param name="g">The linear green lanes.</param>
    /// <param name="b">The linear blue lanes.</param>
    /// <param name="matrix">The intensity-scaled forward matrix in Vector3.Transform order.</param>
    /// <param name="biases">The signed inverse biases, subtracted before root evaluation.</param>
    /// <param name="cubeRootBiases">The signed cube roots added after root evaluation.</param>
    /// <param name="x">The X lanes.</param>
    /// <param name="y">The Y lanes.</param>
    /// <param name="blue">The B lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertFromRgb(
        Vector128<float> r,
        Vector128<float> g,
        Vector128<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector128<float> x,
        out Vector128<float> y,
        out Vector128<float> blue)
    {
        Vector128<float> mixed0 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M31), b, Vector128.Create(-biases.X));
        mixed0 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M21), g, mixed0);
        mixed0 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M11), r, mixed0);

        Vector128<float> mixed1 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M32), b, Vector128.Create(-biases.Y));
        mixed1 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M22), g, mixed1);
        mixed1 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M12), r, mixed1);

        Vector128<float> mixed2 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M33), b, Vector128.Create(-biases.Z));
        mixed2 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M23), g, mixed2);
        mixed2 = Vector128.MultiplyAddEstimate(Vector128.Create(matrix.M13), r, mixed2);

        mixed0 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed0), Vector128.Create(cubeRootBiases.X));
        mixed1 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed1), Vector128.Create(cubeRootBiases.Y));
        mixed2 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed2), Vector128.Create(cubeRootBiases.Z));
        Vector128<float> half = Vector128.Create(0.5F);
        x = half * (mixed0 - mixed1);
        y = half * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>
    /// Transforms 8 independent RGB samples with the same scalar coefficients and native evaluation order.
    /// </summary>
    /// <param name="r">The linear red lanes.</param>
    /// <param name="g">The linear green lanes.</param>
    /// <param name="b">The linear blue lanes.</param>
    /// <param name="matrix">The intensity-scaled forward matrix in Vector3.Transform order.</param>
    /// <param name="biases">The signed inverse biases, subtracted before root evaluation.</param>
    /// <param name="cubeRootBiases">The signed cube roots added after root evaluation.</param>
    /// <param name="x">The X lanes.</param>
    /// <param name="y">The Y lanes.</param>
    /// <param name="blue">The B lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertFromRgb(
        Vector256<float> r,
        Vector256<float> g,
        Vector256<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector256<float> x,
        out Vector256<float> y,
        out Vector256<float> blue)
    {
        Vector256<float> mixed0 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M31), b, Vector256.Create(-biases.X));
        mixed0 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M21), g, mixed0);
        mixed0 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M11), r, mixed0);

        Vector256<float> mixed1 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M32), b, Vector256.Create(-biases.Y));
        mixed1 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M22), g, mixed1);
        mixed1 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M12), r, mixed1);

        Vector256<float> mixed2 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M33), b, Vector256.Create(-biases.Z));
        mixed2 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M23), g, mixed2);
        mixed2 = Vector256.MultiplyAddEstimate(Vector256.Create(matrix.M13), r, mixed2);

        mixed0 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed0), Vector256.Create(cubeRootBiases.X));
        mixed1 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed1), Vector256.Create(cubeRootBiases.Y));
        mixed2 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed2), Vector256.Create(cubeRootBiases.Z));
        Vector256<float> half = Vector256.Create(0.5F);
        x = half * (mixed0 - mixed1);
        y = half * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>
    /// Transforms 16 independent RGB samples with the same scalar coefficients and native evaluation order.
    /// </summary>
    /// <param name="r">The linear red lanes.</param>
    /// <param name="g">The linear green lanes.</param>
    /// <param name="b">The linear blue lanes.</param>
    /// <param name="matrix">The intensity-scaled forward matrix in Vector3.Transform order.</param>
    /// <param name="biases">The signed inverse biases, subtracted before root evaluation.</param>
    /// <param name="cubeRootBiases">The signed cube roots added after root evaluation.</param>
    /// <param name="x">The X lanes.</param>
    /// <param name="y">The Y lanes.</param>
    /// <param name="blue">The B lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertFromRgb(
        Vector512<float> r,
        Vector512<float> g,
        Vector512<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector512<float> x,
        out Vector512<float> y,
        out Vector512<float> blue)
    {
        Vector512<float> mixed0 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M31), b, Vector512.Create(-biases.X));
        mixed0 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M21), g, mixed0);
        mixed0 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M11), r, mixed0);

        Vector512<float> mixed1 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M32), b, Vector512.Create(-biases.Y));
        mixed1 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M22), g, mixed1);
        mixed1 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M12), r, mixed1);

        Vector512<float> mixed2 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M33), b, Vector512.Create(-biases.Z));
        mixed2 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M23), g, mixed2);
        mixed2 = Vector512.MultiplyAddEstimate(Vector512.Create(matrix.M13), r, mixed2);

        mixed0 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed0), Vector512.Create(cubeRootBiases.X));
        mixed1 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed1), Vector512.Create(cubeRootBiases.Y));
        mixed2 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed2), Vector512.Create(cubeRootBiases.Z));
        Vector512<float> half = Vector512.Create(0.5F);
        x = half * (mixed0 - mixed1);
        y = half * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>
    /// Applies the inverse opsin equations to one pixel in the same order as each SIMD lane.
    /// </summary>
    /// <param name="x">The X sample.</param>
    /// <param name="y">The Y sample.</param>
    /// <param name="b">The B sample.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The signed absorbance biases.</param>
    /// <param name="cubeRootBiases">The signed cube roots of the negative biases.</param>
    /// <param name="r">The reconstructed red sample.</param>
    /// <param name="g">The reconstructed green sample.</param>
    /// <param name="blue">The reconstructed blue sample.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertToRgb(
        float x,
        float y,
        float b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out float r,
        out float g,
        out float blue)
    {
        // Undo the opponent mix and the forward transform's cube-root offset before cubing.
        float gammaR = (y + x) - cubeRootBiases.X;
        float gammaG = (y - x) - cubeRootBiases.Y;
        float gammaB = b - cubeRootBiases.Z;
        float mixedR = float.MultiplyAddEstimate(gammaR * gammaR, gammaR, biases.X);
        float mixedG = float.MultiplyAddEstimate(gammaG * gammaG, gammaG, biases.Y);
        float mixedB = float.MultiplyAddEstimate(gammaB * gammaB, gammaB, biases.Z);

        // The runtime uses the same X, then Y, then Z multiply-add order as shared Transform3.
        Vector3 linear = Vector3.Transform(new Vector3(mixedR, mixedG, mixedB), matrix);
        r = linear.X;
        g = linear.Y;
        blue = linear.Z;
    }

    /// <summary>
    /// Reconstructs 4 independent pixels; each register holds one component in pixel order.
    /// </summary>
    /// <param name="x">The X samples.</param>
    /// <param name="y">The Y samples.</param>
    /// <param name="b">The B samples.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The signed absorbance biases, broadcast uniformly to all lanes.</param>
    /// <param name="cubeRootBiases">The signed cube roots subtracted before cubing.</param>
    /// <param name="r">The reconstructed red samples.</param>
    /// <param name="g">The reconstructed green samples.</param>
    /// <param name="blue">The reconstructed blue samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertToRgb(
        Vector128<float> x,
        Vector128<float> y,
        Vector128<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector128<float> r,
        out Vector128<float> g,
        out Vector128<float> blue)
    {
        Vector128<float> gammaR = (y + x) - Vector128.Create(cubeRootBiases.X);
        Vector128<float> gammaG = (y - x) - Vector128.Create(cubeRootBiases.Y);
        Vector128<float> gammaB = b - Vector128.Create(cubeRootBiases.Z);
        Vector128<float> mixedR = Vector128.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector128.Create(biases.X));
        Vector128<float> mixedG = Vector128.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector128.Create(biases.Y));
        Vector128<float> mixedB = Vector128.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector128.Create(biases.Z));
        SimdUtils.Transform3(in matrix, mixedR, mixedG, mixedB, out r, out g, out blue);
    }

    /// <summary>
    /// Reconstructs 8 independent pixels; each register holds one component in pixel order.
    /// </summary>
    /// <param name="x">The X samples.</param>
    /// <param name="y">The Y samples.</param>
    /// <param name="b">The B samples.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The signed absorbance biases, broadcast uniformly to all lanes.</param>
    /// <param name="cubeRootBiases">The signed cube roots subtracted before cubing.</param>
    /// <param name="r">The reconstructed red samples.</param>
    /// <param name="g">The reconstructed green samples.</param>
    /// <param name="blue">The reconstructed blue samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertToRgb(
        Vector256<float> x,
        Vector256<float> y,
        Vector256<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector256<float> r,
        out Vector256<float> g,
        out Vector256<float> blue)
    {
        Vector256<float> gammaR = (y + x) - Vector256.Create(cubeRootBiases.X);
        Vector256<float> gammaG = (y - x) - Vector256.Create(cubeRootBiases.Y);
        Vector256<float> gammaB = b - Vector256.Create(cubeRootBiases.Z);
        Vector256<float> mixedR = Vector256.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector256.Create(biases.X));
        Vector256<float> mixedG = Vector256.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector256.Create(biases.Y));
        Vector256<float> mixedB = Vector256.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector256.Create(biases.Z));
        SimdUtils.Transform3(in matrix, mixedR, mixedG, mixedB, out r, out g, out blue);
    }

    /// <summary>
    /// Reconstructs 16 independent pixels; each register holds one component in pixel order.
    /// </summary>
    /// <param name="x">The X samples.</param>
    /// <param name="y">The Y samples.</param>
    /// <param name="b">The B samples.</param>
    /// <param name="matrix">The transposed, intensity-scaled inverse matrix.</param>
    /// <param name="biases">The signed absorbance biases, broadcast uniformly to all lanes.</param>
    /// <param name="cubeRootBiases">The signed cube roots subtracted before cubing.</param>
    /// <param name="r">The reconstructed red samples.</param>
    /// <param name="g">The reconstructed green samples.</param>
    /// <param name="blue">The reconstructed blue samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ConvertToRgb(
        Vector512<float> x,
        Vector512<float> y,
        Vector512<float> b,
        in Matrix4x4 matrix,
        Vector3 biases,
        Vector3 cubeRootBiases,
        out Vector512<float> r,
        out Vector512<float> g,
        out Vector512<float> blue)
    {
        Vector512<float> gammaR = (y + x) - Vector512.Create(cubeRootBiases.X);
        Vector512<float> gammaG = (y - x) - Vector512.Create(cubeRootBiases.Y);
        Vector512<float> gammaB = b - Vector512.Create(cubeRootBiases.Z);
        Vector512<float> mixedR = Vector512.MultiplyAddEstimate(gammaR * gammaR, gammaR, Vector512.Create(biases.X));
        Vector512<float> mixedG = Vector512.MultiplyAddEstimate(gammaG * gammaG, gammaG, Vector512.Create(biases.Y));
        Vector512<float> mixedB = Vector512.MultiplyAddEstimate(gammaB * gammaB, gammaB, Vector512.Create(biases.Z));
        SimdUtils.Transform3(in matrix, mixedR, mixedG, mixedB, out r, out g, out blue);
    }

    /// <summary>
    /// Computes a nonnegative finite cube root plus its bias without overflowing reciprocal-root intermediates.
    /// </summary>
    /// <param name="value">The nonnegative finite absorbance, including zero and subnormal values.</param>
    /// <param name="add">The negative cube-root bias.</param>
    /// <returns>The biased root with the final addition performed as a multiply-add estimate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CubeRootAndAdd(float value, float add)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value) & MagnitudeMask;
        bool subnormal = bits < MinimumNormalBits;
        float normalized = BitConverter.UInt32BitsToSingle(bits) * (subnormal ? SubnormalScale : 1F);
        uint normalizedBits = BitConverter.SingleToUInt32Bits(normalized);

        // Standard subnormal preparation multiplies by 2^24, then corrects the extracted exponent.
        // Zero has no exponent: substitute exponent zero before division and use final scale zero.
        int exponent = bits == 0 ? 0 : (int)(normalizedBits >> MantissaBits) - ExponentBias - (subnormal ? SubnormalExponentShift : 0);
        int positiveExponent = exponent + ExponentOffset;

        // Adding 150 keeps the exponent in [1,277] and preserves its remainder modulo three.
        // Multiplication by ceil(2^16/3), then shifting 16 bits, gives the exact integer quotient throughout that interval.
        int quotient = (positiveExponent * ThirdMultiplier) >> ThirdShift;
        int remainder = positiveExponent - (3 * quotient);
        float z = BitConverter.Int32BitsToSingle((int)(normalizedBits & MantissaMask) | ((ExponentBias + remainder) << MantissaBits));
        float scale = bits == 0 ? 0F : BitConverter.Int32BitsToSingle((quotient + ExponentBias - ExponentQuotientOffset) << MantissaBits);
        float reciprocalRoot = BitConverter.Int32BitsToSingle(ReciprocalRootSeedBias - ((ExponentBias + remainder) * ReciprocalRootExponentMultiplier));
        float thirdZ = OneThird * z;

        // x=z*2^(3k), where 1<=z<8 and k=quotient-ExponentQuotientOffset. Reciprocal roots and their fourth powers stay in a normal, bounded range.
        // Native reciprocal-root refinements now see only that range. They never see tiny normals or subnormal absorbance directly.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            float squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = float.MultiplyAddEstimate(-thirdZ, squared * squared, FourThirds * reciprocalRoot);
        }

        float squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = float.MultiplyAddEstimate(OneThird, float.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot), reciprocalRoot);
        squaredRoot = reciprocalRoot * reciprocalRoot;

        // Scaling a normal coefficient by 2^k is exact. Keep it inside the last multiply-add so bias cancellation gets no extra rounded root.
        return float.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }

    /// <summary>
    /// Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.
    /// </summary>
    /// <param name="value">The absorbance lanes, including zero and subnormal values.</param>
    /// <param name="add">The corresponding negative cube-root biases.</param>
    /// <returns>The biased roots, with no scalar lane calls or temporary sample storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> CubeRootAndAdd(Vector128<float> value, Vector128<float> add)
    {
        Vector128<uint> bits = value.AsUInt32() & Vector128.Create(MagnitudeMask);
        Vector128<uint> zero = Vector128.Equals(bits, Vector128<uint>.Zero);
        Vector128<uint> subnormal = Vector128.LessThan(bits, Vector128.Create(MinimumNormalBits));
        Vector128<float> normalization = Vector128.ConditionalSelect(subnormal.AsSingle(), Vector128.Create(SubnormalScale), Vector128<float>.One);
        Vector128<uint> normalizedBits = (bits.AsSingle() * normalization).AsUInt32();
        Vector128<int> exponent = Vector128.ShiftRightLogical(normalizedBits, MantissaBits).AsInt32() - Vector128.Create(ExponentBias);
        exponent -= Vector128.ConditionalSelect(subnormal.AsInt32(), Vector128.Create(SubnormalExponentShift), Vector128<int>.Zero);
        exponent = Vector128.ConditionalSelect(zero.AsInt32(), Vector128<int>.Zero, exponent);
        Vector128<int> positiveExponent = exponent + Vector128.Create(ExponentOffset);

        // For positiveExponent in [1,277], floor(v/3)=(v*21846)>>16. 21846=ceil(2^16/3).
        // The reciprocal error is below 1/3, so it cannot cross the next quotient. All products fit Int32 and remain SIMD.
        Vector128<int> quotient = Vector128.ShiftRightArithmetic(positiveExponent * Vector128.Create(ThirdMultiplier), ThirdShift);
        Vector128<int> remainder = positiveExponent - (quotient * Vector128.Create(3));
        Vector128<int> normalExponent = remainder + Vector128.Create(ExponentBias);
        Vector128<uint> mantissa = normalizedBits & Vector128.Create(MantissaMask);
        Vector128<float> z = (mantissa | Vector128.ShiftLeft(normalExponent, MantissaBits).AsUInt32()).AsSingle();
        Vector128<float> scale = Vector128.ShiftLeft(quotient + Vector128.Create(ExponentBias - ExponentQuotientOffset), MantissaBits).AsSingle();
        scale = Vector128.ConditionalSelect(zero.AsSingle(), Vector128<float>.Zero, scale);
        Vector128<int> seed = Vector128.Create(ReciprocalRootSeedBias) - (normalExponent * Vector128.Create(ReciprocalRootExponentMultiplier));
        Vector128<float> reciprocalRoot = seed.AsSingle();
        Vector128<float> thirdZ = Vector128.Create(OneThird) * z;

        // Every z is in [1,8), including zero lanes whose final scale is zero. The iteration matches the scalar arithmetic order.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            Vector128<float> squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = Vector128.MultiplyAddEstimate(-thirdZ, squared * squared, Vector128.Create(FourThirds) * reciprocalRoot);
        }

        Vector128<float> squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = Vector128.MultiplyAddEstimate(
            Vector128.Create(OneThird),
            Vector128.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot),
            reciprocalRoot);

        squaredRoot = reciprocalRoot * reciprocalRoot;
        return Vector128.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }

    /// <summary>
    /// Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.
    /// </summary>
    /// <param name="value">The absorbance lanes, including zero and subnormal values.</param>
    /// <param name="add">The corresponding negative cube-root biases.</param>
    /// <returns>The biased roots, with no scalar lane calls or temporary sample storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> CubeRootAndAdd(Vector256<float> value, Vector256<float> add)
    {
        Vector256<uint> bits = value.AsUInt32() & Vector256.Create(MagnitudeMask);
        Vector256<uint> zero = Vector256.Equals(bits, Vector256<uint>.Zero);
        Vector256<uint> subnormal = Vector256.LessThan(bits, Vector256.Create(MinimumNormalBits));
        Vector256<float> normalization = Vector256.ConditionalSelect(subnormal.AsSingle(), Vector256.Create(SubnormalScale), Vector256<float>.One);
        Vector256<uint> normalizedBits = (bits.AsSingle() * normalization).AsUInt32();
        Vector256<int> exponent = Vector256.ShiftRightLogical(normalizedBits, MantissaBits).AsInt32() - Vector256.Create(ExponentBias);
        exponent -= Vector256.ConditionalSelect(subnormal.AsInt32(), Vector256.Create(SubnormalExponentShift), Vector256<int>.Zero);
        exponent = Vector256.ConditionalSelect(zero.AsInt32(), Vector256<int>.Zero, exponent);
        Vector256<int> positiveExponent = exponent + Vector256.Create(ExponentOffset);

        // For positiveExponent in [1,277], floor(v/3)=(v*21846)>>16. 21846=ceil(2^16/3).
        // The reciprocal error is below 1/3, so it cannot cross the next quotient. All products fit Int32 and remain SIMD.
        Vector256<int> quotient = Vector256.ShiftRightArithmetic(positiveExponent * Vector256.Create(ThirdMultiplier), ThirdShift);
        Vector256<int> remainder = positiveExponent - (quotient * Vector256.Create(3));
        Vector256<int> normalExponent = remainder + Vector256.Create(ExponentBias);
        Vector256<uint> mantissa = normalizedBits & Vector256.Create(MantissaMask);
        Vector256<float> z = (mantissa | Vector256.ShiftLeft(normalExponent, MantissaBits).AsUInt32()).AsSingle();
        Vector256<float> scale = Vector256.ShiftLeft(quotient + Vector256.Create(ExponentBias - ExponentQuotientOffset), MantissaBits).AsSingle();
        scale = Vector256.ConditionalSelect(zero.AsSingle(), Vector256<float>.Zero, scale);
        Vector256<int> seed = Vector256.Create(ReciprocalRootSeedBias) - (normalExponent * Vector256.Create(ReciprocalRootExponentMultiplier));
        Vector256<float> reciprocalRoot = seed.AsSingle();
        Vector256<float> thirdZ = Vector256.Create(OneThird) * z;

        // Every z is in [1,8), including zero lanes whose final scale is zero. The iteration matches the scalar arithmetic order.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            Vector256<float> squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = Vector256.MultiplyAddEstimate(-thirdZ, squared * squared, Vector256.Create(FourThirds) * reciprocalRoot);
        }

        Vector256<float> squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = Vector256.MultiplyAddEstimate(
            Vector256.Create(OneThird),
            Vector256.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot),
            reciprocalRoot);

        squaredRoot = reciprocalRoot * reciprocalRoot;
        return Vector256.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }

    /// <summary>
    /// Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.
    /// </summary>
    /// <param name="value">The absorbance lanes, including zero and subnormal values.</param>
    /// <param name="add">The corresponding negative cube-root biases.</param>
    /// <returns>The biased roots, with no scalar lane calls or temporary sample storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> CubeRootAndAdd(Vector512<float> value, Vector512<float> add)
    {
        Vector512<uint> bits = value.AsUInt32() & Vector512.Create(MagnitudeMask);
        Vector512<uint> zero = Vector512.Equals(bits, Vector512<uint>.Zero);
        Vector512<uint> subnormal = Vector512.LessThan(bits, Vector512.Create(MinimumNormalBits));
        Vector512<float> normalization = Vector512.ConditionalSelect(subnormal.AsSingle(), Vector512.Create(SubnormalScale), Vector512<float>.One);
        Vector512<uint> normalizedBits = (bits.AsSingle() * normalization).AsUInt32();
        Vector512<int> exponent = Vector512.ShiftRightLogical(normalizedBits, MantissaBits).AsInt32() - Vector512.Create(ExponentBias);
        exponent -= Vector512.ConditionalSelect(subnormal.AsInt32(), Vector512.Create(SubnormalExponentShift), Vector512<int>.Zero);
        exponent = Vector512.ConditionalSelect(zero.AsInt32(), Vector512<int>.Zero, exponent);
        Vector512<int> positiveExponent = exponent + Vector512.Create(ExponentOffset);

        // For positiveExponent in [1,277], floor(v/3)=(v*21846)>>16. 21846=ceil(2^16/3).
        // The reciprocal error is below 1/3, so it cannot cross the next quotient. All products fit Int32 and remain SIMD.
        Vector512<int> quotient = Vector512.ShiftRightArithmetic(positiveExponent * Vector512.Create(ThirdMultiplier), ThirdShift);
        Vector512<int> remainder = positiveExponent - (quotient * Vector512.Create(3));
        Vector512<int> normalExponent = remainder + Vector512.Create(ExponentBias);
        Vector512<uint> mantissa = normalizedBits & Vector512.Create(MantissaMask);
        Vector512<float> z = (mantissa | Vector512.ShiftLeft(normalExponent, MantissaBits).AsUInt32()).AsSingle();
        Vector512<float> scale = Vector512.ShiftLeft(quotient + Vector512.Create(ExponentBias - ExponentQuotientOffset), MantissaBits).AsSingle();
        scale = Vector512.ConditionalSelect(zero.AsSingle(), Vector512<float>.Zero, scale);
        Vector512<int> seed = Vector512.Create(ReciprocalRootSeedBias) - (normalExponent * Vector512.Create(ReciprocalRootExponentMultiplier));
        Vector512<float> reciprocalRoot = seed.AsSingle();
        Vector512<float> thirdZ = Vector512.Create(OneThird) * z;

        // Every z is in [1,8), including zero lanes whose final scale is zero. The iteration matches the scalar arithmetic order.
        for (int i = 0; i < ReciprocalRootIterations; i++)
        {
            Vector512<float> squared = reciprocalRoot * reciprocalRoot;
            reciprocalRoot = Vector512.MultiplyAddEstimate(-thirdZ, squared * squared, Vector512.Create(FourThirds) * reciprocalRoot);
        }

        Vector512<float> squaredRoot = reciprocalRoot * reciprocalRoot;
        reciprocalRoot = Vector512.MultiplyAddEstimate(
            Vector512.Create(OneThird),
            Vector512.MultiplyAddEstimate(-z, squaredRoot * squaredRoot, reciprocalRoot),
            reciprocalRoot);

        squaredRoot = reciprocalRoot * reciprocalRoot;
        return Vector512.MultiplyAddEstimate(squaredRoot * scale, z, add);
    }
}
