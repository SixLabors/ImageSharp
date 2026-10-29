// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles.Companding;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>Converts linear or encoded sRGB planes to the encoder's XYB samples.</summary>
internal static class JxlXybEncoder
{
    /// <summary>The IEEE single-precision mask that removes the sign bit before root normalization.</summary>
    private const uint MagnitudeMask = 0x7FFFFFFFU;

    /// <summary>The IEEE single-precision fraction bits retained when rebuilding the normalized mantissa.</summary>
    private const uint MantissaMask = 0x007FFFFFU;

    /// <summary>The IEEE bit pattern of the smallest positive normal float; smaller magnitudes need exponent normalization.</summary>
    private const uint MinimumNormalBits = 0x00800000U;

    /// <summary>The number of fraction bits below the IEEE single-precision exponent field.</summary>
    private const int MantissaBits = 23;

    /// <summary>The bias added to an IEEE single-precision normal exponent.</summary>
    private const int ExponentBias = 127;

    /// <summary>The exact power-of-two shift that makes every nonzero subnormal float normal.</summary>
    private const int SubnormalExponentShift = 24;

    /// <summary>The exact multiplier used before extracting a subnormal input's exponent.</summary>
    private const float SubnormalScale = 1 << SubnormalExponentShift;

    /// <summary>
    /// The exponent offset divisible by three that makes all finite input exponents positive.
    /// The smallest subnormal exponent is -149, so the adjusted exponent range is [1,277].
    /// </summary>
    private const int ExponentOffset = 150;

    /// <summary>The cube-root exponent offset removed after dividing the adjusted exponent by three.</summary>
    private const int ExponentQuotientOffset = ExponentOffset / 3;

    /// <summary>The fixed-point reciprocal shift used for exact division by three over the adjusted exponent range.</summary>
    private const int ThirdShift = 16;

    /// <summary>Ceil(2^16/3), which gives floor(v/3) after shifting for every integer v in [1,277].</summary>
    private const int ThirdMultiplier = 21846;

    /// <summary>The native bit-pattern bias used to seed the reciprocal cube root of a normalized mantissa.</summary>
    private const int ReciprocalRootSeedBias = 0x54800000;

    /// <summary>The native exponent multiplier used by the reciprocal-root seed.</summary>
    private const int ReciprocalRootExponentMultiplier = 0x002AAAAA;

    /// <summary>The rounded single-precision one-third coefficient used by native root refinement.</summary>
    private const float OneThird = 1F / 3F;

    /// <summary>The rounded single-precision four-thirds coefficient used by native root refinement.</summary>
    private const float FourThirds = 4F / 3F;

    /// <summary>The number of native reciprocal-root iterations before its final correction.</summary>
    private const int ReciprocalRootIterations = 3;

    /// <summary>Prepares nine intensity-scaled matrix entries and three negative cube-root biases.</summary>
    /// <param name="intensityTarget">The positive image intensity target, in nits.</param>
    /// <param name="coefficients">The twelve scalar coefficients reused by every row and SIMD width.</param>
    private static void ComputeOpsinAbsorbanceCoefficients(float intensityTarget, Span<float> coefficients)
    {
        ReadOnlySpan<float> matrix = JxlOpsinConstants.OpsinAbsorbanceMatrix;
        ReadOnlySpan<float> biases = JxlOpsinConstants.OpsinAbsorbanceBias;
        float scale = intensityTarget / 255F;
        for (int i = 0; i < matrix.Length; i++)
        {
            coefficients[i] = matrix[i] * scale;
        }

        for (int c = 0; c < 3; c++)
        {
            coefficients[9 + c] = -MathF.Cbrt(biases[c]);
        }
    }

    /// <summary>Converts source planes to XYB and optionally retains the expanded linear samples.</summary>
    /// <typeparam name="TOperator">The shared identity or sRGB expansion operator selected before traversal.</typeparam>
    /// <param name="coefficients">The twelve prepared scalar coefficients.</param>
    /// <param name="source">The source planes, which can also be the XYB destination.</param>
    /// <param name="destination">The equally sized XYB destination.</param>
    /// <param name="linear">The optional equally sized destination for expanded linear RGB.</param>
    /// <param name="operation">The input component operation.</param>
    private static void RgbToXyb<TOperator>(
        ReadOnlySpan<float> coefficients,
        JxlImage3<float> source,
        JxlImage3<float> destination,
        JxlImage3<float>? linear,
        TOperator operation)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        // Borrow every allocation before row processing. Independent source, XYB and retained-linear owners can have different strides.
        ReadOnlySpan<float> input0 = source.PlaneRow(0, 0);
        ReadOnlySpan<float> input1 = source.PlaneRow(1, 0);
        ReadOnlySpan<float> input2 = source.PlaneRow(2, 0);
        Span<float> output0 = destination.PlaneRow(0, 0);
        Span<float> output1 = destination.PlaneRow(1, 0);
        Span<float> output2 = destination.PlaneRow(2, 0);
        Span<float> linear0 = linear is null ? [] : linear.PlaneRow(0, 0);
        Span<float> linear1 = linear is null ? [] : linear.PlaneRow(1, 0);
        Span<float> linear2 = linear is null ? [] : linear.PlaneRow(2, 0);
        int sourceStride = source.PixelsPerRow;
        int destinationStride = destination.PixelsPerRow;
        int linearStride = linear is null ? 0 : linear.PixelsPerRow;
        int width = source.XSize;

        for (int y = 0; y < source.YSize; y++)
        {
            int inputOffset = y * sourceStride;
            int outputOffset = y * destinationStride;
            int linearOffset = y * linearStride;
            RgbRowToXyb(
                input0.Slice(inputOffset, width),
                input1.Slice(inputOffset, width),
                input2.Slice(inputOffset, width),
                output0.Slice(outputOffset, width),
                output1.Slice(outputOffset, width),
                output2.Slice(outputOffset, width),
                linear is null ? [] : linear0.Slice(linearOffset, width),
                linear is null ? [] : linear1.Slice(linearOffset, width),
                linear is null ? [] : linear2.Slice(linearOffset, width),
                coefficients,
                operation);
        }
    }

    /// <summary>Fuses shared input expansion, optional linear output and XYB reconstruction over one logical row.</summary>
    /// <typeparam name="TOperator">The shared input operation.</typeparam>
    /// <param name="inputR">The finite normalized red samples within the pixel range.</param>
    /// <param name="inputG">The corresponding green samples.</param>
    /// <param name="inputB">The corresponding blue samples.</param>
    /// <param name="outputX">The equally sized X destination.</param>
    /// <param name="outputY">The equally sized Y destination.</param>
    /// <param name="outputB">The equally sized B destination.</param>
    /// <param name="linearR">The optional red destination, empty when no linear output is requested.</param>
    /// <param name="linearG">The matching optional green destination.</param>
    /// <param name="linearB">The matching optional blue destination.</param>
    /// <param name="coefficients">The twelve scalar coefficients prepared before traversal.</param>
    /// <param name="operation">The shared input component operation.</param>
    /// <remarks>
    /// All logical rows have equal lengths. RGB and XYB rows can start at the same address; offset overlap is not supported.
    /// Input components use the normalized finite pixel range [-65504,65504]. Encoded input must use the shared bounded expansion operator.
    /// Supply three empty linear rows when retention is disabled. Retained linear storage must be separate from XYB output storage.
    /// </remarks>
    public static void RgbRowToXyb<TOperator>(
        ReadOnlySpan<float> inputR,
        ReadOnlySpan<float> inputG,
        ReadOnlySpan<float> inputB,
        Span<float> outputX,
        Span<float> outputY,
        Span<float> outputB,
        Span<float> linearR,
        Span<float> linearG,
        Span<float> linearB,
        ReadOnlySpan<float> coefficients,
        TOperator operation)
        where TOperator : struct, TensorPrimitives_.IUnaryOperator
    {
        ref float sourceR = ref MemoryMarshal.GetReference(inputR);
        ref float sourceG = ref MemoryMarshal.GetReference(inputG);
        ref float sourceB = ref MemoryMarshal.GetReference(inputB);
        ref float destinationX = ref MemoryMarshal.GetReference(outputX);
        ref float destinationY = ref MemoryMarshal.GetReference(outputY);
        ref float destinationB = ref MemoryMarshal.GetReference(outputB);
        ref float retainedR = ref MemoryMarshal.GetReference(linearR);
        ref float retainedG = ref MemoryMarshal.GetReference(linearG);
        ref float retainedB = ref MemoryMarshal.GetReference(linearB);
        bool retainLinear = !linearR.IsEmpty;
        int x = 0;

        // Each register holds one component from consecutive pixels. The selected operation owns input expansion and its result safety.
        // Load RGB before storing either output. Every complete SIMD block and scalar remainder uses the same coefficient layout.
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= inputR.Length - Vector512<float>.Count; x += Vector512<float>.Count)
            {
                Vector512<float> r = operation.Invoke(Vector512.LoadUnsafe(ref sourceR, (uint)x));
                Vector512<float> g = operation.Invoke(Vector512.LoadUnsafe(ref sourceG, (uint)x));
                Vector512<float> b = operation.Invoke(Vector512.LoadUnsafe(ref sourceB, (uint)x));
                if (retainLinear)
                {
                    r.StoreUnsafe(ref retainedR, (uint)x);
                    g.StoreUnsafe(ref retainedG, (uint)x);
                    b.StoreUnsafe(ref retainedB, (uint)x);
                }

                LinearRgbToXyb(r, g, b, coefficients, out Vector512<float> valueX, out Vector512<float> valueY, out Vector512<float> valueB);
                valueX.StoreUnsafe(ref destinationX, (uint)x);
                valueY.StoreUnsafe(ref destinationY, (uint)x);
                valueB.StoreUnsafe(ref destinationB, (uint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= inputR.Length - Vector256<float>.Count; x += Vector256<float>.Count)
            {
                Vector256<float> r = operation.Invoke(Vector256.LoadUnsafe(ref sourceR, (uint)x));
                Vector256<float> g = operation.Invoke(Vector256.LoadUnsafe(ref sourceG, (uint)x));
                Vector256<float> b = operation.Invoke(Vector256.LoadUnsafe(ref sourceB, (uint)x));
                if (retainLinear)
                {
                    r.StoreUnsafe(ref retainedR, (uint)x);
                    g.StoreUnsafe(ref retainedG, (uint)x);
                    b.StoreUnsafe(ref retainedB, (uint)x);
                }

                LinearRgbToXyb(r, g, b, coefficients, out Vector256<float> valueX, out Vector256<float> valueY, out Vector256<float> valueB);
                valueX.StoreUnsafe(ref destinationX, (uint)x);
                valueY.StoreUnsafe(ref destinationY, (uint)x);
                valueB.StoreUnsafe(ref destinationB, (uint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= inputR.Length - Vector128<float>.Count; x += Vector128<float>.Count)
            {
                Vector128<float> r = operation.Invoke(Vector128.LoadUnsafe(ref sourceR, (uint)x));
                Vector128<float> g = operation.Invoke(Vector128.LoadUnsafe(ref sourceG, (uint)x));
                Vector128<float> b = operation.Invoke(Vector128.LoadUnsafe(ref sourceB, (uint)x));
                if (retainLinear)
                {
                    r.StoreUnsafe(ref retainedR, (uint)x);
                    g.StoreUnsafe(ref retainedG, (uint)x);
                    b.StoreUnsafe(ref retainedB, (uint)x);
                }

                LinearRgbToXyb(r, g, b, coefficients, out Vector128<float> valueX, out Vector128<float> valueY, out Vector128<float> valueB);
                valueX.StoreUnsafe(ref destinationX, (uint)x);
                valueY.StoreUnsafe(ref destinationY, (uint)x);
                valueB.StoreUnsafe(ref destinationB, (uint)x);
            }
        }

        for (; x < inputR.Length; x++)
        {
            float r = operation.Invoke(inputR[x]);
            float g = operation.Invoke(inputG[x]);
            float b = operation.Invoke(inputB[x]);
            if (retainLinear)
            {
                linearR[x] = r;
                linearG[x] = g;
                linearB[x] = b;
            }

            LinearRgbToXyb(r, g, b, coefficients, out float valueX, out float valueY, out float valueB);
            outputX[x] = valueX;
            outputY[x] = valueY;
            outputB[x] = valueB;
        }
    }

    /// <summary>Applies the biased absorbance transform and opponent mix to one linear RGB sample.</summary>
    /// <param name="r">The finite red component in the pixel range.</param>
    /// <param name="g">The corresponding green component.</param>
    /// <param name="b">The corresponding blue component.</param>
    /// <param name="coefficients">The twelve prepared scalar coefficients.</param>
    /// <param name="x">The X result.</param>
    /// <param name="y">The Y result.</param>
    /// <param name="blue">The B result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LinearRgbToXyb(float r, float g, float b, ReadOnlySpan<float> coefficients, out float x, out float y, out float blue)
    {
        ReadOnlySpan<float> biases = JxlOpsinConstants.OpsinAbsorbanceBias;

        // Preserve native B+bias, then G, then R multiply-add order. Moving bias outside that chain changes rounding near black.
        float mixed0 = float.MultiplyAddEstimate(coefficients[2], b, biases[0]);
        mixed0 = float.MultiplyAddEstimate(coefficients[1], g, mixed0);
        mixed0 = float.MultiplyAddEstimate(coefficients[0], r, mixed0);
        float mixed1 = float.MultiplyAddEstimate(coefficients[5], b, biases[1]);
        mixed1 = float.MultiplyAddEstimate(coefficients[4], g, mixed1);
        mixed1 = float.MultiplyAddEstimate(coefficients[3], r, mixed1);
        float mixed2 = float.MultiplyAddEstimate(coefficients[8], b, biases[2]);
        mixed2 = float.MultiplyAddEstimate(coefficients[7], g, mixed2);
        mixed2 = float.MultiplyAddEstimate(coefficients[6], r, mixed2);
        mixed0 = CubeRootAndAdd(MathF.Max(0F, mixed0), coefficients[9]);
        mixed1 = CubeRootAndAdd(MathF.Max(0F, mixed1), coefficients[10]);
        mixed2 = CubeRootAndAdd(MathF.Max(0F, mixed2), coefficients[11]);
        x = 0.5F * (mixed0 - mixed1);
        y = 0.5F * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>Transforms 4 independent RGB samples with the same scalar coefficients and native evaluation order.</summary>
    /// <param name="r">The linear red lanes.</param>
    /// <param name="g">The linear green lanes.</param>
    /// <param name="b">The linear blue lanes.</param>
    /// <param name="coefficients">The twelve prepared scalar coefficients.</param>
    /// <param name="x">The X lanes.</param>
    /// <param name="y">The Y lanes.</param>
    /// <param name="blue">The B lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LinearRgbToXyb(
        Vector128<float> r,
        Vector128<float> g,
        Vector128<float> b,
        ReadOnlySpan<float> coefficients,
        out Vector128<float> x,
        out Vector128<float> y,
        out Vector128<float> blue)
    {
        ReadOnlySpan<float> biases = JxlOpsinConstants.OpsinAbsorbanceBias;
        Vector128<float> mixed0 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[2]), b, Vector128.Create(biases[0]));
        mixed0 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[1]), g, mixed0);
        mixed0 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[0]), r, mixed0);

        Vector128<float> mixed1 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[5]), b, Vector128.Create(biases[1]));
        mixed1 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[4]), g, mixed1);
        mixed1 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[3]), r, mixed1);

        Vector128<float> mixed2 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[8]), b, Vector128.Create(biases[2]));
        mixed2 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[7]), g, mixed2);
        mixed2 = Vector128.MultiplyAddEstimate(Vector128.Create(coefficients[6]), r, mixed2);

        mixed0 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed0), Vector128.Create(coefficients[9]));
        mixed1 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed1), Vector128.Create(coefficients[10]));
        mixed2 = CubeRootAndAdd(Vector128.Max(Vector128<float>.Zero, mixed2), Vector128.Create(coefficients[11]));
        Vector128<float> half = Vector128.Create(0.5F);
        x = half * (mixed0 - mixed1);
        y = half * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>Transforms 8 independent RGB samples with the same scalar coefficients and native evaluation order.</summary>
    /// <param name="r">The linear red lanes.</param>
    /// <param name="g">The linear green lanes.</param>
    /// <param name="b">The linear blue lanes.</param>
    /// <param name="coefficients">The twelve prepared scalar coefficients.</param>
    /// <param name="x">The X lanes.</param>
    /// <param name="y">The Y lanes.</param>
    /// <param name="blue">The B lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LinearRgbToXyb(
        Vector256<float> r,
        Vector256<float> g,
        Vector256<float> b,
        ReadOnlySpan<float> coefficients,
        out Vector256<float> x,
        out Vector256<float> y,
        out Vector256<float> blue)
    {
        ReadOnlySpan<float> biases = JxlOpsinConstants.OpsinAbsorbanceBias;
        Vector256<float> mixed0 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[2]), b, Vector256.Create(biases[0]));
        mixed0 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[1]), g, mixed0);
        mixed0 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[0]), r, mixed0);

        Vector256<float> mixed1 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[5]), b, Vector256.Create(biases[1]));
        mixed1 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[4]), g, mixed1);
        mixed1 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[3]), r, mixed1);

        Vector256<float> mixed2 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[8]), b, Vector256.Create(biases[2]));
        mixed2 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[7]), g, mixed2);
        mixed2 = Vector256.MultiplyAddEstimate(Vector256.Create(coefficients[6]), r, mixed2);

        mixed0 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed0), Vector256.Create(coefficients[9]));
        mixed1 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed1), Vector256.Create(coefficients[10]));
        mixed2 = CubeRootAndAdd(Vector256.Max(Vector256<float>.Zero, mixed2), Vector256.Create(coefficients[11]));
        Vector256<float> half = Vector256.Create(0.5F);
        x = half * (mixed0 - mixed1);
        y = half * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>Transforms 16 independent RGB samples with the same scalar coefficients and native evaluation order.</summary>
    /// <param name="r">The linear red lanes.</param>
    /// <param name="g">The linear green lanes.</param>
    /// <param name="b">The linear blue lanes.</param>
    /// <param name="coefficients">The twelve prepared scalar coefficients.</param>
    /// <param name="x">The X lanes.</param>
    /// <param name="y">The Y lanes.</param>
    /// <param name="blue">The B lanes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LinearRgbToXyb(
        Vector512<float> r,
        Vector512<float> g,
        Vector512<float> b,
        ReadOnlySpan<float> coefficients,
        out Vector512<float> x,
        out Vector512<float> y,
        out Vector512<float> blue)
    {
        ReadOnlySpan<float> biases = JxlOpsinConstants.OpsinAbsorbanceBias;
        Vector512<float> mixed0 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[2]), b, Vector512.Create(biases[0]));
        mixed0 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[1]), g, mixed0);
        mixed0 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[0]), r, mixed0);

        Vector512<float> mixed1 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[5]), b, Vector512.Create(biases[1]));
        mixed1 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[4]), g, mixed1);
        mixed1 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[3]), r, mixed1);

        Vector512<float> mixed2 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[8]), b, Vector512.Create(biases[2]));
        mixed2 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[7]), g, mixed2);
        mixed2 = Vector512.MultiplyAddEstimate(Vector512.Create(coefficients[6]), r, mixed2);

        mixed0 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed0), Vector512.Create(coefficients[9]));
        mixed1 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed1), Vector512.Create(coefficients[10]));
        mixed2 = CubeRootAndAdd(Vector512.Max(Vector512<float>.Zero, mixed2), Vector512.Create(coefficients[11]));
        Vector512<float> half = Vector512.Create(0.5F);
        x = half * (mixed0 - mixed1);
        y = half * (mixed0 + mixed1);
        blue = mixed2;
    }

    /// <summary>Computes a nonnegative finite cube root plus its bias without overflowing reciprocal-root intermediates.</summary>
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

    /// <summary>Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.</summary>
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

    /// <summary>Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.</summary>
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

    /// <summary>Evaluates bounded reciprocal-root refinements independently in each nonnegative finite absorbance lane.</summary>
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

    /// <summary>Converts the image's direct sRGB samples to XYB, optionally retaining linear sRGB.</summary>
    /// <param name="configuration">The configuration used by the general color conversion path.</param>
    /// <param name="currentEncoding">The source color encoding.</param>
    /// <param name="intensityTarget">The positive original intensity target, in nits.</param>
    /// <param name="black">The optional source black channel used by the general color conversion path.</param>
    /// <param name="image">The equally sized source planes, replaced with XYB samples.</param>
    /// <param name="cms">The general color conversion provider.</param>
    /// <param name="linear">The optional equally sized output that retains expanded linear sRGB.</param>
    /// <returns>Whether the source and requested output planes have matching dimensions.</returns>
    public static bool ToXyb(
        Configuration configuration,
        JxlColorEncoding currentEncoding,
        float intensityTarget,
        JxlPlane<float>? black,
        JxlImage3<float> image,
        JxlCmsInterface cms,
        JxlImage3<float>? linear)
    {
        if (black is not null)
        {
            if (image.GetRectangle() != black.GetRectangle())
            {
                return false;
            }
        }

        if (linear is not null)
        {
            if (image.GetRectangle() != linear.GetRectangle())
            {
                return false;
            }
        }

        // Twelve scalar coefficients serve every SIMD width. Source expansion, optional linear storage and XYB conversion share one pass.
        Span<float> coefficients = stackalloc float[12];
        ComputeOpsinAbsorbanceCoefficients(intensityTarget, coefficients);
        bool directSrgb = currentEncoding.HaveFields
            && currentEncoding.WhitePoint == JxlWhitePoint.D65
            && (currentEncoding.ColorSpace == JxlColorSpace.Gray
                || (currentEncoding.ColorSpace == JxlColorSpace.Rgb && currentEncoding.Primaries == JxlPrimaries.SRgb));

        if (directSrgb && currentEncoding.TransferFunction.IsLinear)
        {
            RgbToXyb(coefficients, image, image, linear, default(TensorPrimitives_.IdentityOperator));
            return true;
        }

        if (directSrgb && currentEncoding.TransferFunction.IsSrgb)
        {
            CompandingUtilities.BoundedOperator<SRgbCompanding.ExpandOperator> expand = new(new(1.055F, 0.055F, 0.04045F), PixelColorRange.Extended);
            RgbToXyb(coefficients, image, image, linear, expand);
            return true;
        }

        // General profile conversion is still supplied by the codec's color-management integration.
        JxlColorEncoding linearSrgb = new()
        {
            ColorSpace = currentEncoding.IsGray ? JxlColorSpace.Gray : JxlColorSpace.Rgb,
            TransferFunction = new() { TransferFunction = JxlTransferFunction.Linear }
        };

        ApplyColorTransform(
            configuration,
            currentEncoding,
            intensityTarget,
            image,
            black,
            image.GetRectangle(),
            linearSrgb,
            cms,
            linear ?? image);

        // Read the actual transformed owner directly. When linear output is retained, no full-image copy back to image is needed.
        RgbToXyb(coefficients, linear ?? image, image, null, default(TensorPrimitives_.IdentityOperator));
        return true;
    }
}
