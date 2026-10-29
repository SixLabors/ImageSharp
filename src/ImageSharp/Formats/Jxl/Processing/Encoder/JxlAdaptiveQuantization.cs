// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Adaptive quantization encoder
/// </summary>
internal static class JxlAdaptiveQuantization
{
    // Scaling differences between JPEG XL and Butteraugli
    private const float SGMul = 226.77216153508914f;
    private const float SGMul2 = 1f / 73.377132366608819f;

    // Includes correlation factor for std::log -> log2
    private const float SGRetMul = SGMul2 * 18.6580932135f * JxlMath.InverseLog2E;
    private const float SGVOffset = 7.7825991679894591f;

    private const float DcQuantPow = 0.83f;
    private const float DcQuant = 1.095924047623553f;
    private const float AcQuant = 0.765f;

    private const int DefaultButteraugliIterations = 2;
    private const int MaxButteraugliIterations = 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeMaskForAcStrategyUse(float outputValue)
    {
        const float multiplier = 1f;
        const float offset = 0.001f;

        return multiplier / (outputValue + offset);
    }

    public static Vector<float> ComputeMask(Vector<float> outputValue)
    {
        Vector<float> @base = Vector.Create(-0.7647f);
        Vector<float> mul4 = Vector.Create(9.4708735624378946f);
        Vector<float> mul2 = Vector.Create(17.35036561631863f);
        Vector<float> offset2 = Vector.Create(302.59587815579727f);
        Vector<float> mul3 = Vector.Create(6.7943250517376494f);
        Vector<float> offset3 = Vector.Create(3.7179635626140772f);
        Vector<float> offset4 = Vector.Create(0.25f) * offset3;
        Vector<float> mul0 = Vector.Create(0.80061762862741759f);

        Vector<float> v1 = Vector.Max(outputValue * mul0, Vector.Create(1e-3f));
        Vector<float> v2 = Vector<float>.One / (v1 + offset2);
        Vector<float> v3 = Vector<float>.One / ((v1 * v1) + offset3);
        Vector<float> v4 = Vector<float>.One / ((v1 * v1) + offset4);

        return @base + ((mul4 * v4) + ((mul2 * v2) + (mul3 * v3)));
    }

    public static float RatioOfDerivativesOfCubicRootToSimpleGamma(float v, bool invert = false)
    {
        float epsilon = 1e-2f;
        v = Math.Max(0, v); // cannot be < 0
        const float numMul = SGRetMul * 3 * SGMul;
        float voffset = (SGVOffset * JxlMath.InverseLog2E) + epsilon;
        const float denMul = JxlMath.InverseLog2E * SGMul;

        float v2 = v * v;
        float num = (numMul * v2) + epsilon;
        float den = ((denMul * v) * v2) + voffset;

        return invert ? num / den : den / num;
    }

    public static Vector<float> RatioOfDerivativesOfCubicRootToSimpleGamma(Vector<float> v, bool invert)
    {
        float epsilon = 1e-2f;
        v = Vector.Max(Vector<float>.Zero, v);

        Vector<float> numMul = Vector.Create(SGRetMul * 3 * SGMul);
        Vector<float> vOffset = Vector.Create((SGVOffset * JxlMath.InverseLog2E) + epsilon);
        Vector<float> denMul = Vector.Create(JxlMath.InverseLog2E * SGMul);

        Vector<float> v2 = v * v;
        Vector<float> num = (numMul * v2) + Vector.Create(epsilon);
        Vector<float> den = ((denMul * v) * v2) + vOffset;

        return invert ? num / den : den / num;
    }

    public static float GammaModulation(int x, int y, JxlPlane<float> xybX, JxlPlane<float> xybY, Rectangle rect, float outVal)
    {
        const float biasConst = 0.16f;
        const float gamma = 0.1005613337192697f;

        Vector<float> overallRatio = Vector<float>.Zero;
        Vector<float> bias = new(biasConst);

        for (int dy = 0; dy < 8; dy++)
        {
            ReadOnlySpan<float> rowInX = xybX.GetRow(rect, y + dy);
            ReadOnlySpan<float> rowInY = xybY.GetRow(rect, y + dy);

            for (int dx = 0; dx < 8; dx += Vector<float>.Count)
            {
                Vector<float> iny = new(rowInY[(x + dx)..]);
                iny += bias;

                Vector<float> inx = new(rowInX[(x + dx)..]);

                Vector<float> r = iny - inx;
                Vector<float> ratioR = RatioOfDerivativesOfCubicRootToSimpleGamma(r, invert: true);
                overallRatio += ratioR;

                Vector<float> g = iny + inx;
                Vector<float> ratioG = RatioOfDerivativesOfCubicRootToSimpleGamma(g, invert: true);
                overallRatio += ratioG;
            }
        }

        float overallRatioScalar = Vector.Sum(overallRatio) * (0.5f / 64.0f);

        return (gamma * MathF.Log2(overallRatioScalar)) + outVal;
    }

    // Change precision in 8x8 blocks that have significant amounts of blue
    // content (but are not close to solid blue).
    // This is based on the idea that M and L cone activations saturate the
    // S (blue) receptors, and the S reception becomes more important when
    // both M and L levels are low. In that case M and L receptors may be
    // observing S-spectra instead and viewing them with higher spatial
    // accuracy, justifying spending more bits here.
    public static Vector<float> BlueModulation(int x, int y, JxlPlane<float> planex, JxlPlane<float> planey, JxlPlane<float> planeb, Rectangle rect, Vector<float> outVal)
    {
        const float limitConst = 0.010474084867598155f;
        const float offsetConst = 0.0031994768654636393f;
        const float multiplier = 0.90590804735610064f;
        const float maxLimit = 15.463398341612438f;

        Vector<float> sum = Vector<float>.Zero;
        Vector<float> limit = new(limitConst);
        Vector<float> offset = new(offsetConst);

        for (int dy = 0; dy < 8; dy++)
        {
            ReadOnlySpan<float> rowInX = planex.GetRow(rect, y + dy);
            ReadOnlySpan<float> rowInY = planey.GetRow(rect, y + dy);
            ReadOnlySpan<float> rowInB = planeb.GetRow(rect, y + dy);

            for (int dx = 0; dx < 8; dx += Vector<float>.Count)
            {
                Vector<float> pX = Vector.Create(rowInX[(x + dx)..]);
                Vector<float> pB = Vector.Create(rowInB[(x + dx)..]);
                Vector<float> pYRaw = Vector.Create(rowInY[(x + dx)..]) + offset;

                Vector<float> pYEffective = pYRaw + Vector.Abs(pX);
                Vector<float> excess = pB - pYEffective;

                Vector<float> contribution = Vector.Min(excess, limit);
                contribution = Vector.ConditionalSelect(Vector.GreaterThan(pB, pYEffective), contribution, Vector<float>.Zero);

                sum += contribution;
            }
        }

        float scalarSum = Vector.Sum(sum);

        if (scalarSum >= 32 * limitConst)
        {
            scalarSum = (64 * limitConst) - scalarSum;
        }

        if (scalarSum >= maxLimit * limitConst)
        {
            scalarSum = maxLimit * limitConst;
        }

        scalarSum *= multiplier;

        return new Vector<float>(scalarSum) + outVal;
    }

    private static float HfModulationScalar(int x, int y, JxlPlane<float> xybY, Rectangle rect, float outVal)
    {
        const float valueMinY = 0.0206f;
        const float mulY = -0.38f;
        const float offset = 0.42f;

        float sumY = 0;

        for (int dy = 0; dy < 8; dy++)
        {
            ReadOnlySpan<float> rowInY = xybY.GetRow(rect, y + dy)[x..];
            ReadOnlySpan<float> rowInYNext = dy == 7
                ? rowInY
                : xybY.GetRow(rect, y + dy + 1)[x..];

            for (int dx = 0; dx < 7; dx++)
            {
                sumY += MathF.Min(valueMinY, MathF.Abs(rowInY[dx] - rowInY[dx + 1]));
            }

            for (int dx = 0; dx < 8; dx++)
            {
                sumY += MathF.Min(valueMinY, MathF.Abs(rowInY[dx] - rowInYNext[dx]));
            }
        }

        float scalarSumY = sumY * mulY;
        scalarSumY += offset;

        return scalarSumY + outVal;
    }

    private static Vector<float> HfModulationVectorized(int x, int y, JxlPlane<float> xybY, Rectangle rect, Vector<float> outVal)
    {
        const float valueMinY = 0.0206f;
        const float mulY = -0.38f;
        const float offset = 0.42f;

        Vector<float> sumY = Vector<float>.Zero;
        Vector<float> valueMinVY = new(valueMinY);

        ReadOnlySpan<uint> maskRight = [uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue, 0];

        for (int dy = 0; dy < 8; dy++)
        {
            ReadOnlySpan<float> rowInY = xybY.GetRow(rect, y + dy)[x..];
            ReadOnlySpan<float> rowInYNext = dy == 7
                ? rowInY
                : xybY.GetRow(rect, y + dy + 1)[x..];

            for (int dx = 0; dx < 8; dx += Vector<float>.Count)
            {
                Vector<uint> mask = Vector.Create(maskRight[dx..]);
                Vector<float> maskFloat = Vector.AsVectorSingle(mask);

                Vector<float> pY = Vector.Create(rowInY[dx..]);
                Vector<float> prY = Vector.Create(rowInY[(dx + 1)..]);
                Vector<float> pdY = Vector.Create(rowInYNext[dx..]);

                Vector<float> horizontal = Vector.Min(valueMinVY, Vector.Abs(pY - prY));
                Vector<float> vertical = Vector.Min(valueMinVY, Vector.Abs(pY - pdY));

                sumY += Vector.BitwiseAnd(horizontal, maskFloat);
                sumY += vertical;
            }
        }

        float scalarSumY = Vector.Sum(sumY);
        scalarSumY *= mulY;
        scalarSumY += offset;

        return new Vector<float>(scalarSumY) + outVal;
    }

    public static Vector<float> HfModulation(int x, int y, JxlPlane<float> xybY, Rectangle rect, Vector<float> outVal)
    {
        if (Vector<float>.Count == 1)
        {
            // We don't have SIMD support, everything is scalar.
            // Loop iterations are different in scalar HF modulation,
            // so it's more efficient to go with a separate duplicated
            // routine.
            float result = HfModulationScalar(x, y, xybY, rect, outVal.ToScalar());
            return Vector.Create(result);
        }
        else
        {
            // We have SIMD support
            return HfModulationVectorized(x, y, xybY, rect, outVal);
        }
    }

    private static void PerBlockModulations(
        float butteraugliTarget,
        JxlPlane<float> xybX,
        JxlPlane<float> xybY,
        JxlPlane<float> xybB,
        Rectangle rectIn,
        float scale,
        Rectangle rectOut,
        JxlPlane<float> output)
    {
        float baseLevel = 0.48f * scale;

        const float dampenRampStart = 2.0f;
        const float dampenRampEnd = 14.0f;

        float dampen = 1.0f;

        if (butteraugliTarget >= dampenRampStart)
        {
            dampen = 1.0f - ((butteraugliTarget - dampenRampStart) / (dampenRampEnd - dampenRampStart));

            if (dampen < 0)
            {
                dampen = 0;
            }
        }

        float mul = scale * dampen;
        float add = (1.0f - dampen) * baseLevel;

        for (int iy = rectOut.Y0(); iy < rectOut.Y1(); iy++)
        {
            int y = iy * 8;
            Span<float> rowOut = output.GetRow(iy);

            for (int ix = rectOut.X0(); ix < rectOut.X1(); ix++)
            {
                int x = ix * 8;

                Vector<float> maskVal = ComputeMask(Vector.Create(rowOut[ix]));

                maskVal = GammaModulation(x, y, xybX, xybY, rectIn, maskVal);
                float outVal = HfModulationScalar(x, y, xybY, rectIn, maskVal);
                outVal = MathF.Min(outVal, BlueModulation(x, y, xybX, xybY, xybB, rectIn, maskVal));

                rowOut[ix] = (MathF.Pow(2.0f, outVal * 1.442695041f) * mul) + add;
            }
        }
    }

    private static float MaskingSqrt(float v)
    {
        const float kLogOffset = 27.505837037000106f;
        const float kMul = 211.66567973503678f;

        return 0.25f * MathF.Sqrt((v * MathF.Sqrt(kMul * 1e8f)) + kLogOffset);
    }

    private static Vector<float> MaskingSqrt(Vector<float> v)
    {
        Vector<float> kLogOffset = Vector.Create(27.505837037000106f);
        Vector<float> kMul = Vector.Create(211.66567973503678f);

        return Vector.Create(0.25f) * Vector.SquareRoot((v * Vector.SquareRoot(kMul * 1e8f)) + kLogOffset);
    }

    /// <summary>Inserts one finite sample into four ordered minima.</summary>
    /// <param name="v">The new sample.</param>
    /// <param name="min0">The lowest retained sample.</param>
    /// <param name="min1">The second-lowest retained sample.</param>
    /// <param name="min2">The third-lowest retained sample.</param>
    /// <param name="min3">The fourth-lowest retained sample.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMin4(float v, ref float min0, ref float min1, ref float min2, ref float min3)
    {
        if (v < min3)
        {
            if (v < min0)
            {
                min3 = min2;
                min2 = min1;
                min1 = min0;
                min0 = v;
            }
            else if (v < min1)
            {
                min3 = min2;
                min2 = min1;
                min1 = v;
            }
            else if (v < min2)
            {
                min3 = min2;
                min2 = v;
            }
            else
            {
                min3 = v;
            }
        }
    }

    /// <summary>
    /// Filters the four lowest samples of each clamped three-by-three neighborhood and downsamples the result by two in each axis.
    /// </summary>
    /// <param name="butteraugliTarget">The perceptual target used to select the four minimum weights.</param>
    /// <param name="fromRect">The source region, twice the destination width and height.</param>
    /// <param name="from">The borrowed source plane. Neighbor positions clamp at its global image edges.</param>
    /// <param name="toRect">The destination region, independent of the source origin and physical stride.</param>
    /// <param name="to">The distinct destination plane. Samples outside toRect remain unchanged.</param>
    /// <returns>Whether the source and destination regions have the required two-to-one dimensions.</returns>
    public static bool FuzzyErosion(float butteraugliTarget, Rectangle fromRect, JxlPlane<float> from, Rectangle toRect, JxlPlane<float> to)
    {
        // Each output combines four source neighborhoods. This checks the actual downsampling contract, not equal-sized image regions.
        if ((long)toRect.Width * 2 != fromRect.Width || (long)toRect.Height * 2 != fromRect.Height)
        {
            return false;
        }

        ReadOnlySpan<float> mulBase = [0.125F, 0.1F, 0.09F, 0.06F];
        ReadOnlySpan<float> mulAdd = [0F, -0.1F, -0.09F, -0.06F];
        float mul = butteraugliTarget < 2F ? (2F - butteraugliTarget) * 0.5F : 0F;
        InlineArray4<float> weights = default;
        float normSum = 0F;

        for (int i = 0; i < 4; i++)
        {
            weights[i] = mulBase[i] + (mul * mulAdd[i]);
            normSum += weights[i];
        }

        const float total = 0.29959705784054957F;
        for (int i = 0; i < 4; i++)
        {
            weights[i] *= total / normSum;
        }

        FuzzyErosionOperator operation = new(weights[0], weights[1], weights[2], weights[3]);
        ReadOnlySpan<float> source = from.GetRow(0);
        Span<float> destination = to.GetRow(0);
        // Reserve a right neighbor after every vector's final source pair. This bound uses the full source width, not the tile edge.
        int vectorEnd = Math.Min(toRect.Width, (from.XSize - fromRect.Left - 1) / 2);

        for (int y = 0; y < toRect.Height; y++)
        {
            int sourceY = fromRect.Top + (2 * y);

            // A source row pair needs four rows of neighborhoods. Clamp at the plane edges, including repeated rows at height two.
            ReadOnlySpan<float> top = source.Slice(Math.Max(0, sourceY - 1) * from.PixelsPerRow, from.XSize);
            ReadOnlySpan<float> upper = source.Slice(sourceY * from.PixelsPerRow, from.XSize);
            ReadOnlySpan<float> lower = source.Slice((sourceY + 1) * from.PixelsPerRow, from.XSize);
            ReadOnlySpan<float> bottom = source.Slice(Math.Min(from.YSize - 1, sourceY + 2) * from.PixelsPerRow, from.XSize);
            Span<float> output = destination.Slice(((toRect.Top + y) * to.PixelsPerRow) + toRect.Left, toRect.Width);
            int x = 0;

            // The first output pair can touch the global left edge. Complete it with clamped scalar reads before vector interiors.
            if (fromRect.Left == 0 && x < output.Length)
            {
                output[x++] = Erode2x2(top, upper, lower, bottom, fromRect.Left, operation);
            }

            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= vectorEnd - Vector512<float>.Count; x += Vector512<float>.Count)
                {
                    int sourceX = fromRect.Left + (2 * x);
                    ErodePair(top, upper, lower, bottom, sourceX, operation, out Vector512<float> upper0, out Vector512<float> lower0);
                    ErodePair(top, upper, lower, bottom, sourceX + Vector512<float>.Count, operation, out Vector512<float> upper1, out Vector512<float> lower1);
                    SimdUtils.Deinterleave2(upper0.AsInt32(), upper1.AsInt32(), out Vector512<int> upperEven, out Vector512<int> upperOdd);
                    SimdUtils.Deinterleave2(lower0.AsInt32(), lower1.AsInt32(), out Vector512<int> lowerEven, out Vector512<int> lowerOdd);

                    // Deinterleaving only moves bits. Add the four neighborhoods in native row-major order, without regrouping the lower pair.
                    Vector512<float> result = upperEven.AsSingle() + upperOdd.AsSingle();
                    result += lowerEven.AsSingle();
                    result += lowerOdd.AsSingle();
                    result.StoreUnsafe(ref MemoryMarshal.GetReference(output), (uint)x);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= vectorEnd - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    int sourceX = fromRect.Left + (2 * x);
                    ErodePair(top, upper, lower, bottom, sourceX, operation, out Vector256<float> upper0, out Vector256<float> lower0);
                    ErodePair(top, upper, lower, bottom, sourceX + Vector256<float>.Count, operation, out Vector256<float> upper1, out Vector256<float> lower1);
                    SimdUtils.Deinterleave2(upper0.AsInt32(), upper1.AsInt32(), out Vector256<int> upperEven, out Vector256<int> upperOdd);
                    SimdUtils.Deinterleave2(lower0.AsInt32(), lower1.AsInt32(), out Vector256<int> lowerEven, out Vector256<int> lowerOdd);

                    // Deinterleaving only moves bits. Add the four neighborhoods in native row-major order, without regrouping the lower pair.
                    Vector256<float> result = upperEven.AsSingle() + upperOdd.AsSingle();
                    result += lowerEven.AsSingle();
                    result += lowerOdd.AsSingle();
                    result.StoreUnsafe(ref MemoryMarshal.GetReference(output), (uint)x);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= vectorEnd - Vector128<float>.Count; x += Vector128<float>.Count)
                {
                    int sourceX = fromRect.Left + (2 * x);
                    ErodePair(top, upper, lower, bottom, sourceX, operation, out Vector128<float> upper0, out Vector128<float> lower0);
                    ErodePair(top, upper, lower, bottom, sourceX + Vector128<float>.Count, operation, out Vector128<float> upper1, out Vector128<float> lower1);
                    SimdUtils.Deinterleave2(upper0.AsInt32(), upper1.AsInt32(), out Vector128<int> upperEven, out Vector128<int> upperOdd);
                    SimdUtils.Deinterleave2(lower0.AsInt32(), lower1.AsInt32(), out Vector128<int> lowerEven, out Vector128<int> lowerOdd);

                    // Deinterleaving only moves bits. Add the four neighborhoods in native row-major order, without regrouping the lower pair.
                    Vector128<float> result = upperEven.AsSingle() + upperOdd.AsSingle();
                    result += lowerEven.AsSingle();
                    result += lowerOdd.AsSingle();
                    result.StoreUnsafe(ref MemoryMarshal.GetReference(output), (uint)x);
                }
            }

            for (; x < output.Length; x++)
            {
                output[x] = Erode2x2(top, upper, lower, bottom, fromRect.Left + (2 * x), operation);
            }
        }

        return true;
    }

    /// <summary>
    /// Combines one clamped source pair from each of two rows with the same order as the vector interior.
    /// </summary>
    /// <param name="top">The row above the upper source row.</param>
    /// <param name="upper">The upper source row.</param>
    /// <param name="lower">The lower source row.</param>
    /// <param name="bottom">The row below the lower source row.</param>
    /// <param name="x">The global source column of the left sample.</param>
    /// <param name="operation">The prepared four-minimum weights.</param>
    /// <returns>The sum of the four filtered neighborhoods in upper-left, upper-right, lower-left, lower-right order.</returns>
    private static float Erode2x2(
        ReadOnlySpan<float> top,
        ReadOnlySpan<float> upper,
        ReadOnlySpan<float> lower,
        ReadOnlySpan<float> bottom,
        int x,
        in FuzzyErosionOperator operation)
    {
        int left = Math.Max(0, x - 1);
        int right = Math.Min(upper.Length - 1, x + 2);
        float result = operation.Invoke(upper[x], upper[left], upper[x + 1], top[left], top[x], top[x + 1], lower[left], lower[x], lower[x + 1]);
        result += operation.Invoke(upper[x + 1], upper[x], upper[right], top[x], top[x + 1], top[right], lower[x], lower[x + 1], lower[right]);
        result += operation.Invoke(lower[x], lower[left], lower[x + 1], upper[left], upper[x], upper[x + 1], bottom[left], bottom[x], bottom[x + 1]);
        result += operation.Invoke(lower[x + 1], lower[x], lower[right], upper[x], upper[x + 1], upper[right], bottom[x], bottom[x + 1], bottom[right]);
        return result;
    }

    /// <summary>
    /// Filters corresponding source columns in two adjacent rows while reusing their common neighborhood loads.
    /// </summary>
    /// <param name="top">The row above the upper source row.</param>
    /// <param name="upper">The upper source row.</param>
    /// <param name="lower">The lower source row.</param>
    /// <param name="bottom">The row below the lower source row.</param>
    /// <param name="x">The first global source column. Its vector and one-sample halo stay inside every row.</param>
    /// <param name="operation">The prepared four-minimum weights.</param>
    /// <param name="upperResult">Filtered upper-row samples in original column order.</param>
    /// <param name="lowerResult">Filtered lower-row samples in original column order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ErodePair(
        ReadOnlySpan<float> top,
        ReadOnlySpan<float> upper,
        ReadOnlySpan<float> lower,
        ReadOnlySpan<float> bottom,
        int x,
        in FuzzyErosionOperator operation,
        out Vector128<float> upperResult,
        out Vector128<float> lowerResult)
    {
        Vector128<float> topLeft = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x - 1));
        Vector128<float> topCenter = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x));
        Vector128<float> topRight = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x + 1));
        Vector128<float> upperLeft = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x - 1));
        Vector128<float> upperCenter = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x));
        Vector128<float> upperRight = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x + 1));
        Vector128<float> lowerLeft = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x - 1));
        Vector128<float> lowerCenter = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x));
        Vector128<float> lowerRight = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x + 1));
        Vector128<float> bottomLeft = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x - 1));
        Vector128<float> bottomCenter = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x));
        Vector128<float> bottomRight = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x + 1));

        upperResult = operation.Invoke(upperCenter, upperLeft, upperRight, topLeft, topCenter, topRight, lowerLeft, lowerCenter, lowerRight);

        lowerResult = operation.Invoke(lowerCenter, lowerLeft, lowerRight, upperLeft, upperCenter, upperRight, bottomLeft, bottomCenter, bottomRight);
    }

    /// <summary>
    /// Filters corresponding source columns in two adjacent rows while reusing their common neighborhood loads.
    /// </summary>
    /// <param name="top">The row above the upper source row.</param>
    /// <param name="upper">The upper source row.</param>
    /// <param name="lower">The lower source row.</param>
    /// <param name="bottom">The row below the lower source row.</param>
    /// <param name="x">The first global source column. Its vector and one-sample halo stay inside every row.</param>
    /// <param name="operation">The prepared four-minimum weights.</param>
    /// <param name="upperResult">Filtered upper-row samples in original column order.</param>
    /// <param name="lowerResult">Filtered lower-row samples in original column order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ErodePair(
        ReadOnlySpan<float> top,
        ReadOnlySpan<float> upper,
        ReadOnlySpan<float> lower,
        ReadOnlySpan<float> bottom,
        int x,
        in FuzzyErosionOperator operation,
        out Vector256<float> upperResult,
        out Vector256<float> lowerResult)
    {
        Vector256<float> topLeft = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x - 1));
        Vector256<float> topCenter = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x));
        Vector256<float> topRight = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x + 1));
        Vector256<float> upperLeft = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x - 1));
        Vector256<float> upperCenter = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x));
        Vector256<float> upperRight = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x + 1));
        Vector256<float> lowerLeft = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x - 1));
        Vector256<float> lowerCenter = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x));
        Vector256<float> lowerRight = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x + 1));
        Vector256<float> bottomLeft = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x - 1));
        Vector256<float> bottomCenter = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x));
        Vector256<float> bottomRight = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x + 1));

        upperResult = operation.Invoke(upperCenter, upperLeft, upperRight, topLeft, topCenter, topRight, lowerLeft, lowerCenter, lowerRight);

        lowerResult = operation.Invoke(lowerCenter, lowerLeft, lowerRight, upperLeft, upperCenter, upperRight, bottomLeft, bottomCenter, bottomRight);
    }

    /// <summary>
    /// Filters corresponding source columns in two adjacent rows while reusing their common neighborhood loads.
    /// </summary>
    /// <param name="top">The row above the upper source row.</param>
    /// <param name="upper">The upper source row.</param>
    /// <param name="lower">The lower source row.</param>
    /// <param name="bottom">The row below the lower source row.</param>
    /// <param name="x">The first global source column. Its vector and one-sample halo stay inside every row.</param>
    /// <param name="operation">The prepared four-minimum weights.</param>
    /// <param name="upperResult">Filtered upper-row samples in original column order.</param>
    /// <param name="lowerResult">Filtered lower-row samples in original column order.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ErodePair(
        ReadOnlySpan<float> top,
        ReadOnlySpan<float> upper,
        ReadOnlySpan<float> lower,
        ReadOnlySpan<float> bottom,
        int x,
        in FuzzyErosionOperator operation,
        out Vector512<float> upperResult,
        out Vector512<float> lowerResult)
    {
        Vector512<float> topLeft = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x - 1));
        Vector512<float> topCenter = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x));
        Vector512<float> topRight = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(top), (uint)(x + 1));
        Vector512<float> upperLeft = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x - 1));
        Vector512<float> upperCenter = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x));
        Vector512<float> upperRight = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(upper), (uint)(x + 1));
        Vector512<float> lowerLeft = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x - 1));
        Vector512<float> lowerCenter = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x));
        Vector512<float> lowerRight = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(lower), (uint)(x + 1));
        Vector512<float> bottomLeft = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x - 1));
        Vector512<float> bottomCenter = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x));
        Vector512<float> bottomRight = Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(bottom), (uint)(x + 1));

        upperResult = operation.Invoke(upperCenter, upperLeft, upperRight, topLeft, topCenter, topRight, lowerLeft, lowerCenter, lowerRight);

        lowerResult = operation.Invoke(lowerCenter, lowerLeft, lowerRight, upperLeft, upperCenter, upperRight, bottomLeft, bottomCenter, bottomRight);
    }

    /// <summary>
    /// Evaluates the adaptive quantizer's four-lowest-neighbor filter, distinct from Butteraugli's three-minimum masking filter.
    /// </summary>
    /// <param name="weight0">The lowest sample weight.</param>
    /// <param name="weight1">The second-lowest sample weight.</param>
    /// <param name="weight2">The third-lowest sample weight.</param>
    /// <param name="weight3">The fourth-lowest sample weight.</param>
    private readonly struct FuzzyErosionOperator(float weight0, float weight1, float weight2, float weight3)
    {
        /// <summary>
        /// Returns the weighted four lowest finite values of one three-by-three neighborhood.
        /// </summary>
        /// <param name="first">The center sample.</param>
        /// <param name="second">The left sample.</param>
        /// <param name="third">The right sample.</param>
        /// <param name="fourth">The upper-left sample.</param>
        /// <param name="fifth">The upper sample.</param>
        /// <param name="sixth">The upper-right sample.</param>
        /// <param name="seventh">The lower-left sample.</param>
        /// <param name="eighth">The lower sample.</param>
        /// <param name="ninth">The lower-right sample.</param>
        /// <returns>The weighted four-minimum sum in native arithmetic order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Invoke(
            float first,
            float second,
            float third,
            float fourth,
            float fifth,
            float sixth,
            float seventh,
            float eighth,
            float ninth)
        {
            // Sort the initial four values, then retain only the four lowest while inserting the remaining neighbors.
            // Every SIMD lane performs this same finite-value ordering independently; equal values retain the same four magnitudes.
            float minimum = MathF.Min(first, second);
            second = MathF.Max(first, second);
            first = minimum;
            minimum = MathF.Min(third, fourth);
            fourth = MathF.Max(third, fourth);
            third = minimum;
            minimum = MathF.Min(first, third);
            third = MathF.Max(first, third);
            first = minimum;
            minimum = MathF.Min(second, fourth);
            fourth = MathF.Max(second, fourth);
            second = minimum;
            minimum = MathF.Min(second, third);
            third = MathF.Max(second, third);
            second = minimum;

            StoreMin4(fifth, ref first, ref second, ref third, ref fourth);
            StoreMin4(sixth, ref first, ref second, ref third, ref fourth);
            StoreMin4(seventh, ref first, ref second, ref third, ref fourth);
            StoreMin4(eighth, ref first, ref second, ref third, ref fourth);
            StoreMin4(ninth, ref first, ref second, ref third, ref fourth);
            return (((weight0 * first) + (weight1 * second)) + (weight2 * third)) + (weight3 * fourth);
        }

        /// <inheritdoc cref="Invoke(float, float, float, float, float, float, float, float, float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> Invoke(
            Vector128<float> first,
            Vector128<float> second,
            Vector128<float> third,
            Vector128<float> fourth,
            Vector128<float> fifth,
            Vector128<float> sixth,
            Vector128<float> seventh,
            Vector128<float> eighth,
            Vector128<float> ninth)
        {
            // Sort the initial four values, then retain only the four lowest while inserting the remaining neighbors.
            // Every SIMD lane performs this same finite-value ordering independently; equal values retain the same four magnitudes.
            Vector128<float> minimum = Vector128.Min(first, second);
            second = Vector128.Max(first, second);
            first = minimum;
            minimum = Vector128.Min(third, fourth);
            fourth = Vector128.Max(third, fourth);
            third = minimum;
            minimum = Vector128.Min(first, third);
            third = Vector128.Max(first, third);
            first = minimum;
            minimum = Vector128.Min(second, fourth);
            fourth = Vector128.Max(second, fourth);
            second = minimum;
            minimum = Vector128.Min(second, third);
            third = Vector128.Max(second, third);
            second = minimum;

            StoreMin4(fifth, ref first, ref second, ref third, ref fourth);
            StoreMin4(sixth, ref first, ref second, ref third, ref fourth);
            StoreMin4(seventh, ref first, ref second, ref third, ref fourth);
            StoreMin4(eighth, ref first, ref second, ref third, ref fourth);
            StoreMin4(ninth, ref first, ref second, ref third, ref fourth);
            Vector128<float> result = (Vector128.Create(weight0) * first) + (Vector128.Create(weight1) * second);
            result += Vector128.Create(weight2) * third;
            return result + (Vector128.Create(weight3) * fourth);
        }

        /// <inheritdoc cref="Invoke(float, float, float, float, float, float, float, float, float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector256<float> Invoke(
            Vector256<float> first,
            Vector256<float> second,
            Vector256<float> third,
            Vector256<float> fourth,
            Vector256<float> fifth,
            Vector256<float> sixth,
            Vector256<float> seventh,
            Vector256<float> eighth,
            Vector256<float> ninth)
        {
            // Sort the initial four values, then retain only the four lowest while inserting the remaining neighbors.
            // Every SIMD lane performs this same finite-value ordering independently; equal values retain the same four magnitudes.
            Vector256<float> minimum = Vector256.Min(first, second);
            second = Vector256.Max(first, second);
            first = minimum;
            minimum = Vector256.Min(third, fourth);
            fourth = Vector256.Max(third, fourth);
            third = minimum;
            minimum = Vector256.Min(first, third);
            third = Vector256.Max(first, third);
            first = minimum;
            minimum = Vector256.Min(second, fourth);
            fourth = Vector256.Max(second, fourth);
            second = minimum;
            minimum = Vector256.Min(second, third);
            third = Vector256.Max(second, third);
            second = minimum;

            StoreMin4(fifth, ref first, ref second, ref third, ref fourth);
            StoreMin4(sixth, ref first, ref second, ref third, ref fourth);
            StoreMin4(seventh, ref first, ref second, ref third, ref fourth);
            StoreMin4(eighth, ref first, ref second, ref third, ref fourth);
            StoreMin4(ninth, ref first, ref second, ref third, ref fourth);
            Vector256<float> result = (Vector256.Create(weight0) * first) + (Vector256.Create(weight1) * second);
            result += Vector256.Create(weight2) * third;
            return result + (Vector256.Create(weight3) * fourth);
        }

        /// <inheritdoc cref="Invoke(float, float, float, float, float, float, float, float, float)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector512<float> Invoke(
            Vector512<float> first,
            Vector512<float> second,
            Vector512<float> third,
            Vector512<float> fourth,
            Vector512<float> fifth,
            Vector512<float> sixth,
            Vector512<float> seventh,
            Vector512<float> eighth,
            Vector512<float> ninth)
        {
            // Sort the initial four values, then retain only the four lowest while inserting the remaining neighbors.
            // Every SIMD lane performs this same finite-value ordering independently; equal values retain the same four magnitudes.
            Vector512<float> minimum = Vector512.Min(first, second);
            second = Vector512.Max(first, second);
            first = minimum;
            minimum = Vector512.Min(third, fourth);
            fourth = Vector512.Max(third, fourth);
            third = minimum;
            minimum = Vector512.Min(first, third);
            third = Vector512.Max(first, third);
            first = minimum;
            minimum = Vector512.Min(second, fourth);
            fourth = Vector512.Max(second, fourth);
            second = minimum;
            minimum = Vector512.Min(second, third);
            third = Vector512.Max(second, third);
            second = minimum;

            StoreMin4(fifth, ref first, ref second, ref third, ref fourth);
            StoreMin4(sixth, ref first, ref second, ref third, ref fourth);
            StoreMin4(seventh, ref first, ref second, ref third, ref fourth);
            StoreMin4(eighth, ref first, ref second, ref third, ref fourth);
            StoreMin4(ninth, ref first, ref second, ref third, ref fourth);
            Vector512<float> result = (Vector512.Create(weight0) * first) + (Vector512.Create(weight1) * second);
            result += Vector512.Create(weight2) * third;
            return result + (Vector512.Create(weight3) * fourth);
        }
    }

    /// <summary>
    /// Inserts independent finite samples into four ordered minima without changing lane order.
    /// </summary>
    /// <param name="value">The new sample in each lane.</param>
    /// <param name="min0">The lowest retained sample in each lane.</param>
    /// <param name="min1">The second-lowest retained sample in each lane.</param>
    /// <param name="min2">The third-lowest retained sample in each lane.</param>
    /// <param name="min3">The fourth-lowest retained sample in each lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMin4(
        Vector128<float> value,
        ref Vector128<float> min0,
        ref Vector128<float> min1,
        ref Vector128<float> min2,
        ref Vector128<float> min3)
    {
        Vector128<float> next = Vector128.Max(value, min0);
        min0 = Vector128.Min(value, min0);
        value = Vector128.Max(next, min1);
        min1 = Vector128.Min(next, min1);
        next = Vector128.Max(value, min2);
        min2 = Vector128.Min(value, min2);
        min3 = Vector128.Min(next, min3);
    }

    /// <summary>
    /// Inserts independent finite samples into four ordered minima without changing lane order.
    /// </summary>
    /// <param name="value">The new sample in each lane.</param>
    /// <param name="min0">The lowest retained sample in each lane.</param>
    /// <param name="min1">The second-lowest retained sample in each lane.</param>
    /// <param name="min2">The third-lowest retained sample in each lane.</param>
    /// <param name="min3">The fourth-lowest retained sample in each lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMin4(
        Vector256<float> value,
        ref Vector256<float> min0,
        ref Vector256<float> min1,
        ref Vector256<float> min2,
        ref Vector256<float> min3)
    {
        Vector256<float> next = Vector256.Max(value, min0);
        min0 = Vector256.Min(value, min0);
        value = Vector256.Max(next, min1);
        min1 = Vector256.Min(next, min1);
        next = Vector256.Max(value, min2);
        min2 = Vector256.Min(value, min2);
        min3 = Vector256.Min(next, min3);
    }

    /// <summary>
    /// Inserts independent finite samples into four ordered minima without changing lane order.
    /// </summary>
    /// <param name="value">The new sample in each lane.</param>
    /// <param name="min0">The lowest retained sample in each lane.</param>
    /// <param name="min1">The second-lowest retained sample in each lane.</param>
    /// <param name="min2">The third-lowest retained sample in each lane.</param>
    /// <param name="min3">The fourth-lowest retained sample in each lane.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreMin4(
        Vector512<float> value,
        ref Vector512<float> min0,
        ref Vector512<float> min1,
        ref Vector512<float> min2,
        ref Vector512<float> min3)
    {
        Vector512<float> next = Vector512.Max(value, min0);
        min0 = Vector512.Min(value, min0);
        value = Vector512.Max(next, min1);
        min1 = Vector512.Min(next, min1);
        next = Vector512.Max(value, min2);
        min2 = Vector512.Min(value, min2);
        min3 = Vector512.Min(next, min3);
    }

    private static void Blur1x1Masking(Configuration configuration, JxlPlane<float> mask1x1, Rectangle rect)
    {
        const float filterMask1x1_0 = 0.364911248f;
        const float filterMask1x1_1 = 0.05f;
        const float filterMask1x1_2 = 0.1688888021f;
        const float filterMask1x1_3 = 0.221069183f;
        const float filterMask1x1_4 = 0.306563504f;

        double sum = 1.0 +
                     (4.0 * ((filterMask1x1_0 + filterMask1x1_1) +
                             (filterMask1x1_2 + filterMask1x1_4) +
                             (2.0 * filterMask1x1_3)));

        if (sum < 1e-5)
        {
            sum = 1e-5;
        }

        float normalize = (float)(1.0 / sum);

        JxlWeightsSymmetric5 weights = default;
        weights.C = JxlWeightsSymmetric5.CreateVector4(normalize);
        weights.R = JxlWeightsSymmetric5.CreateVector4(normalize * filterMask1x1_0);
        weights.R2 = JxlWeightsSymmetric5.CreateVector4(normalize * filterMask1x1_2);
        weights.D = JxlWeightsSymmetric5.CreateVector4(normalize * filterMask1x1_1);
        weights.D2 = JxlWeightsSymmetric5.CreateVector4(normalize * filterMask1x1_4);
        weights.L = JxlWeightsSymmetric5.CreateVector4(normalize * filterMask1x1_3);

        using JxlPlane<float> temp = new(configuration, rect.Width, rect.Height);

        if (!JxlConvolve.Symmetric5(mask1x1, in rect, ref weights, temp, temp.GetRectangle()))
        {
            throw new InvalidOperationException("Symmetric5 convolution failed");
        }

        mask1x1.Swap(temp);
    }

    /// <summary>
    /// Computes the block quantization map and masking planes with reusable tile scratch storage.
    /// </summary>
    /// <param name="configuration">The configuration that owns the allocator.</param>
    /// <param name="butteraugliTarget">The requested perceptual error target.</param>
    /// <param name="xyb">The source XYB planes.</param>
    /// <param name="rect">The source region with dimensions divisible by eight.</param>
    /// <param name="scale">The quantization scale.</param>
    /// <param name="mask">The returned block masking plane.</param>
    /// <param name="mask1x1">The returned full-resolution masking plane.</param>
    /// <returns>The owned block quantization map. The caller owns all three output planes.</returns>
    public static JxlPlane<float> AdaptiveQuantizationMap(
        Configuration configuration,
        float butteraugliTarget,
        JxlImage3<float> xyb,
        Rectangle rect,
        float scale,
        out JxlPlane<float> mask,
        out JxlPlane<float> mask1x1)
    {
        if ((rect.Width & 7) != 0)
        {
            throw new ArgumentException("Rectangle width must be divisible by 8", nameof(rect));
        }

        if ((rect.Height & 7) != 0)
        {
            throw new ArgumentException("Rectangle height must be divisible by 8", nameof(rect));
        }

        int xSizeBlocks = rect.Width / 8;
        int ySizeBlocks = rect.Height / 8;
        JxlPlane<float> quantizationMap = new(configuration, xSizeBlocks, ySizeBlocks);
        JxlPlane<float>? allocatedMask = null;
        JxlPlane<float>? allocatedMask1x1 = null;

        try
        {
            allocatedMask = new(configuration, xSizeBlocks, ySizeBlocks);
            allocatedMask1x1 = new(configuration, xyb.XSize, xyb.YSize);
            mask = allocatedMask;
            mask1x1 = allocatedMask1x1;

            using AdaptiveQuantizationInternal impl = new(configuration, quantizationMap);
            int numTilesX = JxlMath.DivCeil(xSizeBlocks, JxlEncoderParameters.TileDimensionsInBlocks);
            int numTilesY = JxlMath.DivCeil(ySizeBlocks, JxlEncoderParameters.TileDimensionsInBlocks);

            // Tiles run sequentially and reuse one difference row and one erosion plane.
            // Tile coordinates select the output region; they never select a scratch allocation.
            for (int ty = 0; ty < numTilesY; ty++)
            {
                int by0 = ty * JxlEncoderParameters.TileDimensionsInBlocks;
                int by1 = Math.Min(by0 + JxlEncoderParameters.TileDimensionsInBlocks, ySizeBlocks);

                for (int tx = 0; tx < numTilesX; tx++)
                {
                    int bx0 = tx * JxlEncoderParameters.TileDimensionsInBlocks;
                    int bx1 = Math.Min(bx0 + JxlEncoderParameters.TileDimensionsInBlocks, xSizeBlocks);
                    Rectangle rectOut = new(bx0, by0, bx1 - bx0, by1 - by0);

                    if (!impl.TryComputeTile(butteraugliTarget, scale, xyb, rect, rectOut, mask, mask1x1))
                    {
                        throw new InvalidOperationException("Could not process the quantization tile.");
                    }
                }
            }

            Blur1x1Masking(configuration, mask1x1, rect);
            return quantizationMap;
        }
        catch
        {
            // Output ownership transfers only on success. Nullable locals record which allocations
            // completed, so an allocation or transform failure releases every acquired output.
            allocatedMask1x1?.Dispose();
            allocatedMask?.Dispose();
            quantizationMap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Writes one perceptual error per transform block into reusable search storage.
    /// </summary>
    /// <param name="distMap">The full-resolution perceptual differences.</param>
    /// <param name="tileSize">The pixel width and height of one quantization block.</param>
    /// <param name="margin">The additional sampled pixels around each transform.</param>
    /// <param name="acStrategy">The selected transforms and their covered blocks.</param>
    /// <param name="tileDistMap">The preallocated per-block result.</param>
    private static void TileDistMap(JxlPlane<float> distMap, int tileSize, int margin, JxlAcStrategyImage acStrategy, JxlPlane<float> tileDistMap)
    {
        int tileXSize = tileDistMap.XSize;
        int tileYSize = tileDistMap.YSize;
        ReadOnlySpan<float> differences = distMap.GetRow(0);
        Span<float> tileDifferences = tileDistMap.GetRow(0);
        ReadOnlySpan<byte> strategies = acStrategy.GetRows();

        const float borderMul = 0.98f;
        const float cornerMul = 0.7f;
        const float tileNorm = 1.2f;

        for (int tileY = 0; tileY < tileYSize; tileY++)
        {
            JxlAcStrategyRow acStrategyRow = new(strategies.Slice(tileY * acStrategy.PixelsPerRow, acStrategy.XSize));

            for (int tileX = 0; tileX < tileXSize; tileX++)
            {
                JxlAcStrategy acs = acStrategyRow[tileX];

                if (!acs.IsFirstBlock)
                {
                    continue;
                }

                int thisTileXSize = acs.CoveredBlocksX * tileSize;
                int thisTileYSize = acs.CoveredBlocksY * tileSize;

                int yBegin = Math.Max(0, (tileSize * tileY) - margin);
                int yEnd = Math.Min(distMap.YSize, (tileSize * tileY) + thisTileYSize + margin);

                int xBegin = Math.Max(0, (tileSize * tileX) - margin);
                int xEnd = Math.Min(distMap.XSize, (tileSize * tileX) + thisTileXSize + margin);

                float distNorm = 0.0f;
                double pixels = 0.0;

                for (int y = yBegin; y < yEnd; y++)
                {
                    float yMul = 1.0f;

                    if (margin != 0 && (y == yBegin || y == yEnd - 1))
                    {
                        yMul = borderMul;
                    }

                    ReadOnlySpan<float> row = differences[(y * distMap.PixelsPerRow)..];

                    for (int x = xBegin; x < xEnd; x++)
                    {
                        float xMul = yMul;

                        if (margin != 0 && (x == xBegin || x == xEnd - 1))
                        {
                            if (xMul == 1.0f)
                            {
                                xMul = borderMul;
                            }
                            else
                            {
                                xMul = cornerMul;
                            }
                        }

                        float v = row[x];

                        v *= v;
                        v *= v;
                        v *= v;
                        v *= v;

                        distNorm += xMul * v;
                        pixels += xMul;
                    }
                }

                if (pixels == 0)
                {
                    pixels = 1;
                }

                float tileDist = tileNorm * MathF.Pow((float)(distNorm / pixels), 1.0f / 16.0f);

                // All blocks of a multi-block transform share its error. Slice the existing result span rather than reacquiring row memory.
                for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                {
                    tileDifferences.Slice(((tileY + iy) * tileDistMap.PixelsPerRow) + tileX, acs.CoveredBlocksX).Fill(tileDist);
                }
            }
        }
    }

    /// <summary>
    /// Renders an encoder's prepared coefficients while borrowing its shared controls.
    /// </summary>
    /// <param name="configuration">The configuration that owns temporary decoded samples.</param>
    /// <param name="frameHeader">The frame settings used to prepare the coefficients.</param>
    /// <param name="opsin">The borrowed encoder input used for the frame extent.</param>
    /// <param name="encState">The encoder that owns shared controls and coefficient images.</param>
    /// <param name="cms">The color conversion provider used by the existing encoder pipeline.</param>
    /// <returns>The decoded bundle, which the caller owns and must dispose.</returns>
    private static JxlImageBundle RoundtripImage(
        Configuration configuration,
        JxlFrameHeader frameHeader,
        JxlImage3<float> opsin,
        JxlPassesEncoderState encState,
        JxlCmsInterface cms)
    {
        if (opsin.YSize % JxlFrameDimensions.BlockDimensions != 0)
        {
            throw new InvalidOperationException("The opsin height must be divisible by the block dimension.");
        }

        int xSizeGroups = JxlMath.DivCeil(opsin.XSize, JxlFrameDimensions.GroupDimensions);
        int ySizeGroups = JxlMath.DivCeil(opsin.YSize, JxlFrameDimensions.GroupDimensions);
        int numGroups = xSizeGroups * ySizeGroups;
        int numSpecialFrames = encState.SpecialFrames.Count;
        JxlModularFrameEncoder modularFrameEncoder = JxlModularFrameEncoder.Create(configuration, frameHeader, encState.CParams, false);

        InitializePassesEncoder(configuration, frameHeader, opsin, opsin.GetRectangle(), cms, encState, modularFrameEncoder, null);

        // The encoder has prepared its controls. Borrow that exact state instead of allocating another frame's planes and replacing a pointer.
        // Memory block loading supplies AC coefficients directly, so this path must not resize or overwrite decoded coefficient orders.
        using JxlPassesDecoderState decState = new(encState.Shared);
        decState.InitializeFrame(frameHeader);

        JxlImageBundle decoded = new(encState.Shared.CodecMetadata.ImageMetadata)
        {
            Origin = frameHeader.FrameOrigin
        };

        try
        {
            using JxlImage3<float> tmp = new(configuration, opsin.XSize, opsin.YSize);

            if (!decoded.SetFromImage(tmp, decState.OutputEncodingInfo.ColorEncoding))
            {
                throw new InvalidOperationException("Could not set image bundle");
            }

            JxlPassesDecoderState.PipelineOptions options = new()
            {
                UseSlowRenderPipeline = false,
                Coalescing = false,
                RenderSpotcolors = false,
                RenderNoise = false
            };

            JxlImageMetadata metadata = decoded.Metadata;
            decState.PreparePipeline(frameHeader, encState.Shared.CodecMetadata.ImageMetadata, decoded, options);

            using JxlGroupDecoderCache groupDecCache = new(configuration);
            decState.RenderPipeline.PrepareForThreads(1, false);

            void ProcessGroup(int groupIndex)
            {
                if (frameHeader.LoopFilter!.EpfIterations > 0)
                {
                    JxlLoopFilter.ComputeSigma(decState.Shared.FrameDimensions.BlockGroupRect(groupIndex), decState);
                }

                RenderPipelineInput input = decState.RenderPipeline.GetInputBuffers(groupIndex, 0);
                JxlGroupDecoder.DecodeGroupForRoundtrip(frameHeader, encState.Coeffs, groupIndex, decState, groupDecCache, 0, input, null, null);

                for (int c = 0; c < metadata.ExtraChannelCount; c++)
                {
                    (JxlPlane<float> plane, Rectangle rect) = input.GetBuffer(3 + c);
                    JxlImageOperations.FillPlane(0.0f, plane, rect);
                }

                input.Done();
            }

            for (int i = 0; i < numGroups; i++)
            {
                ProcessGroup(i);
            }

            encState.SpecialFrames.RemoveRange(numSpecialFrames, encState.SpecialFrames.Count - numSpecialFrames);
            return decoded;
        }
        catch
        {
            // Success transfers this bundle to the caller. Failure releases only the locally decoded output, never the encoder's shared planes.
            decoded.Dispose();
            throw;
        }
    }

    public static float InitialQuantDc(float butteraugliTarget)
    {
        const float dcMul = 0.3f; // Butteraugli target where non-linearity kicks in
        float butteraugliTargetDc = MathF.Max(
            0.5f * butteraugliTarget,
            MathF.Min(butteraugliTarget, dcMul * MathF.Pow((1.0f / dcMul) * butteraugliTarget, DcQuantPow)));

        return MathF.Min(DcQuant / butteraugliTargetDc, 50f);
    }

    public static bool AdjustQuantField(JxlAcStrategyImage acStrategy, Rectangle rect, float butteraugliTarget, JxlPlane<float> quantField)
    {
        int stride = quantField.PixelsPerRow;
        ReadOnlySpan<byte> strategies = acStrategy.GetRows();
        Span<float> quantizationSamples = quantField.GetRow(0);
        float meanMaxMixer = 1.0f;
        {
            const float limit = 1.54138f;
            const float mul = 0.56391f;
            const float min = 0f;

            if (butteraugliTarget > limit)
            {
                meanMaxMixer -= (butteraugliTarget - limit) * mul;

                if (meanMaxMixer < min)
                {
                    meanMaxMixer = min;
                }
            }
        }

        for (int y = 0; y < rect.Height; y++)
        {
            JxlAcStrategyRow acStrategyRow = new(strategies.Slice(((rect.Top + y) * acStrategy.PixelsPerRow) + rect.Left, rect.Width));
            Span<float> quantRow = quantizationSamples[(((rect.Top + y) * stride) + rect.Left)..];

            for (int x = 0; x < rect.Width; x++)
            {
                JxlAcStrategy acs = acStrategyRow[x];

                if (!acs.IsFirstBlock)
                {
                    continue;
                }

                if (x + acs.CoveredBlocksX > quantField.XSize || y + acs.CoveredBlocksY > quantField.YSize)
                {
                    return false;
                }

                float max = quantRow[x];
                float mean = 0f;

                // Each transform owns a rectangular set of quantization blocks. Visit that set without advancing the outer block coordinates.
                for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                {
                    for (int ix = 0; ix < acs.CoveredBlocksX; ix++)
                    {
                        mean += quantRow[x + ix + (iy * stride)];
                        max = MathF.Max(quantRow[x + ix + (iy * stride)], max);
                    }
                }

                mean /= acs.CoveredBlocksY * acs.CoveredBlocksX;

                if (acs.CoveredBlocksY * acs.CoveredBlocksX >= 4)
                {
                    max *= meanMaxMixer;
                    max += (1.0f - meanMaxMixer) * mean;
                }

                for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                {
                    quantRow.Slice(x + (iy * stride), acs.CoveredBlocksX).Fill(max);
                }
            }
        }

        return true;
    }

    public static bool FindBestQuantization(
        Configuration configuration,
        JxlFrameHeader frameHeader,
        JxlImage3<float> linear,
        JxlImage3<float> opsin,
        JxlPlane<float> quantField,
        JxlPassesEncoderState encState,
        JxlCmsInterface cms,
        JxlAuxiliaryOutput auxOut)
    {
        JxlCompressionParameters cparams = encState.CompressionParameters;

        if (cparams.Resampling > 1 && cparams.OriginalButteraugliDistance <= 4.0f * cparams.Resampling)
        {
            // For downsampled opsin image, the butteraugli based adaptive quantization
            // loop would only make the size bigger without improving the distance much,
            // so in this case we enable it only for very high butteraugli targets.
            return true;
        }

        JxlQuantizer quantizer = encState.Shared.Quantizer;
        JxlPlane<int> rawQuantField = encState.Shared.RawQuantField;
        float butteraugliTarget = cparams.ButteraugliDistance;
        float originalButteraugli = cparams.OriginalButteraugliDistance;

        ButteraugliParameters parameters = new();
        JxlCustomTransferFunction tf = frameHeader.Metadata!.ImageMetadata.ColorEncoding.TransferFunction;

        parameters.IntensityTarget = tf.IsPq || tf.IsHlg ? frameHeader.Metadata.ImageMetadata.IntensityTarget : 0;

        using JxlComparator comparator = new(parameters, cms);
        comparator.SetReferenceImage(configuration, linear);

        bool lowerIsBetter = comparator.GoodQualityScore < comparator.BadQualityScore;
        float initialQuantDc = InitialQuantDc(butteraugliTarget);

        if (!AdjustQuantField(encState.Shared.AcStrategy, quantField.GetRectangle(), originalButteraugli, quantField))
        {
            return false;
        }

        using JxlPlane<float> initialQuantField = new(configuration, quantField.XSize, quantField.YSize);

        if (!JxlImageOperations.CopyImage(quantField, initialQuantField))
        {
            return false;
        }

        JxlImageOperations.ImageMinMax(initialQuantField, out float initialQfMin, out float initialQfMax);

        float initialQfRatio = initialQfMax / initialQfMin;
        float qfMaxDeviationLow = MathF.Sqrt(250f / initialQfRatio);
        float asymmetry = 2;

        if (qfMaxDeviationLow < asymmetry)
        {
            asymmetry = qfMaxDeviationLow;
        }

        float qfLower = initialQfMin / (asymmetry * qfMaxDeviationLow);
        float qfHigher = initialQfMax * (qfMaxDeviationLow / asymmetry);

        // Native requires a representable quantization range below 253 before the iterative search begins.
        if (qfHigher / qfLower >= 253)
        {
            return false;
        }

        const int originalComparisonRound = 1;
        int iters = DefaultButteraugliIterations;

        if (cparams.SpeedTier <= JxlSpeedTier.Tortoise)
        {
            iters = MaxButteraugliIterations;
        }

        // The search keeps these allocations through every attempt. Each attempt overwrites the result maps and reuses the median scratch.
        int tileSize = JxlFrameDimensions.BlockDimensions * cparams.Resampling;
        using JxlPlane<float> tileDistmap = new(configuration, (linear.XSize + tileSize - 1) / tileSize, (linear.YSize + tileSize - 1) / tileSize);
        using JxlPlane<float> diffmap = new(configuration, linear.XSize, linear.YSize);
        using IMemoryOwner<float> quantizationScratch = configuration.MemoryAllocator.Allocate<float>(quantField.XSize * quantField.YSize);
        Span<float> scratch = quantizationScratch.Memory.Span;
        Span<float> quantizationSamples = quantField.GetRow(0);
        ReadOnlySpan<float> initialSamples = initialQuantField.GetRow(0);
        ReadOnlySpan<float> tileSamples = tileDistmap.GetRow(0);

        for (int i = 0; i < iters + 1; i++)
        {
            if (!quantizer.SetQuantField(initialQuantDc, quantField, rawQuantField, scratch))
            {
                return false;
            }

            using JxlImageBundle decLinear = RoundtripImage(configuration, frameHeader, opsin, encState, cms);

            comparator.CompareWith(configuration, decLinear, diffmap, out float score);

            if (!lowerIsBetter)
            {
                score = -score;
                ScoreImage(-1.0f, diffmap);
            }

            TileDistMap(diffmap, tileSize, 0, encState.Shared.AcStrategy, tileDistmap);

            auxOut.NumberOfButteraugliIterations++;
            if (i == iters)
            {
                break;
            }

            Span<double> pow = [0.2, 0.2, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0];
            Span<double> powMod = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0];

            if (i == originalComparisonRound)
            {
                double initMul = 0.6;
                double oneMinusInitMul = 1.0 - initMul;

                for (int y = 0; y < quantField.YSize; y++)
                {
                    Span<float> rowQ = quantizationSamples[(y * quantField.PixelsPerRow)..];
                    ReadOnlySpan<float> rowInit = initialSamples[(y * initialQuantField.PixelsPerRow)..];

                    for (int x = 0; x < quantField.XSize; x++)
                    {
                        double clamp = (oneMinusInitMul * rowQ[x]) + (initMul * rowInit[x]);

                        if (rowQ[x] < clamp)
                        {
                            rowQ[x] = (float)clamp;

                            if (rowQ[x] > qfHigher)
                            {
                                rowQ[x] = qfHigher;
                            }

                            if (rowQ[x] < qfLower)
                            {
                                rowQ[x] = qfLower;
                            }
                        }
                    }
                }
            }

            double curPow = 0;

            if (i < 7)
            {
                curPow = pow[i] + ((originalButteraugli - 1.0) * powMod[i]);

                if (curPow < 0)
                {
                    curPow = 0;
                }
            }

            if (curPow == 0.0)
            {
                for (int y = 0; y < quantField.YSize; y++)
                {
                    ReadOnlySpan<float> rowDist = tileSamples[(y * tileDistmap.PixelsPerRow)..];
                    Span<float> rowQ = quantizationSamples[(y * quantField.PixelsPerRow)..];

                    for (int x = 0; x < quantField.XSize; x++)
                    {
                        float diff = rowDist[x] / originalButteraugli;

                        if (diff > 1.0f)
                        {
                            float old = rowQ[x];
                            // Increase the requested precision before checking whether integer quantization preserves that change.
                            rowQ[x] *= diff;
                            int qfOld = (int)MathF.Round(old * quantizer.InverseGlobalScale, MidpointRounding.AwayFromZero);
                            int qfNew = (int)MathF.Round(rowQ[x] * quantizer.InverseGlobalScale, MidpointRounding.AwayFromZero);

                            if (qfOld == qfNew)
                            {
                                rowQ[x] = old + quantizer.Scale;
                            }
                        }

                        if (rowQ[x] > qfHigher)
                        {
                            rowQ[x] = qfHigher;
                        }

                        if (rowQ[x] < qfLower)
                        {
                            rowQ[x] = qfLower;
                        }
                    }
                }
            }
            else
            {
                for (int y = 0; y < quantField.YSize; y++)
                {
                    ReadOnlySpan<float> rowDist = tileSamples[(y * tileDistmap.PixelsPerRow)..];
                    Span<float> rowQ = quantizationSamples[(y * quantField.PixelsPerRow)..];

                    for (int x = 0; x < quantField.XSize; x++)
                    {
                        float diff = rowDist[x] / originalButteraugli;

                        if (diff <= 1.0f)
                        {
                            rowQ[x] *= (float)Math.Pow(diff, curPow);
                        }
                        else
                        {
                            float old = rowQ[x];
                            rowQ[x] *= diff;

                            int qfOld = (int)MathF.Round(old * quantizer.InverseGlobalScale, MidpointRounding.AwayFromZero);
                            int qfNew = (int)MathF.Round(rowQ[x] * quantizer.InverseGlobalScale, MidpointRounding.AwayFromZero);

                            if (qfOld == qfNew)
                            {
                                rowQ[x] = old + quantizer.Scale;
                            }
                        }

                        if (rowQ[x] > qfHigher)
                        {
                            rowQ[x] = qfHigher;
                        }

                        if (rowQ[x] < qfLower)
                        {
                            rowQ[x] = qfLower;
                        }
                    }
                }
            }
        }

        if (!quantizer.SetQuantField(initialQuantDc, quantField, rawQuantField, scratch))
        {
            return false;
        }

        return true;
    }

    public static bool FindBestQuantizationMaxError(
        Configuration configuration,
        JxlFrameHeader frameHeader,
        JxlImage3<float> opsin,
        JxlPlane<float> quantField,
        JxlPassesEncoderState encState,
        JxlCmsInterface cms,
        JxlAuxiliaryOutput auxOut)
    {
        JxlCompressionParameters cparams = encState.CompressionParameters;
        JxlQuantizer quantizer = encState.Shared.Quantizer;
        JxlPlane<int> rawQuantField = encState.Shared.RawQuantField;

        float initialQuantDc = 16f * MathF.Sqrt(0.1f / cparams.ButteraugliDistance);

        if (!AdjustQuantField(encState.Shared.AcStrategy, quantField.GetRectangle(), cparams.OriginalButteraugliDistance, quantField))
        {
            return false;
        }

        InlineArray3<float> inverseMaxError = default;
        inverseMaxError[0] = 1.0f / cparams.MaxError[0];
        inverseMaxError[1] = 1.0f / cparams.MaxError[1];
        inverseMaxError[2] = 1.0f / cparams.MaxError[2];

        // Median selection mutates only this search-owned copy. Reuse its allocation for every attempt and the final field update.
        using IMemoryOwner<float> quantizationScratch = configuration.MemoryAllocator.Allocate<float>(quantField.XSize * quantField.YSize);
        Span<float> scratch = quantizationScratch.Memory.Span;
        Span<float> quantizationSamples = quantField.GetRow(0);
        ReadOnlySpan<byte> strategies = encState.Shared.AcStrategy.GetRows();
        ReadOnlySpan<float> source0 = opsin.Plane(0).GetRow(0);
        ReadOnlySpan<float> source1 = opsin.Plane(1).GetRow(0);
        ReadOnlySpan<float> source2 = opsin.Plane(2).GetRow(0);

        for (int i = 0; i < MaxButteraugliIterations + 1; i++)
        {
            if (!quantizer.SetQuantField(initialQuantDc, quantField, rawQuantField, scratch))
            {
                return false;
            }

            using JxlImageBundle decoded = RoundtripImage(configuration, frameHeader, opsin, encState, cms);
            JxlImage3<float> reconstructed = decoded.Color;
            ReadOnlySpan<float> decoded0 = reconstructed.Plane(0).GetRow(0);
            ReadOnlySpan<float> decoded1 = reconstructed.Plane(1).GetRow(0);
            ReadOnlySpan<float> decoded2 = reconstructed.Plane(2).GetRow(0);

            for (int by = 0; by < encState.Shared.FrameDimensions.YSizeBlocks; by++)
            {
                JxlAcStrategyRow acStrategyRow = new(strategies.Slice(by * encState.Shared.AcStrategy.PixelsPerRow, encState.Shared.AcStrategy.XSize));

                for (int bx = 0; bx < encState.Shared.FrameDimensions.XSizeBlocks; bx++)
                {
                    JxlAcStrategy acs = acStrategyRow[bx];

                    if (!acs.IsFirstBlock)
                    {
                        continue;
                    }

                    float maxError = 0;

                    for (int c = 0; c < 3; c++)
                    {
                        // Borrow all planes once per image. Channel selection changes spans, not memory ownership or row acquisition.
                        ReadOnlySpan<float> sourceSamples = c == 0 ? source0 : c == 1 ? source1 : source2;
                        ReadOnlySpan<float> decodedSamples = c == 0 ? decoded0 : c == 1 ? decoded1 : decoded2;

                        for (int y = by * JxlFrameDimensions.BlockDimensions; y < (by + acs.CoveredBlocksY) * JxlFrameDimensions.BlockDimensions; y++)
                        {
                            if (y >= decoded.YSize)
                            {
                                continue;
                            }

                            ReadOnlySpan<float> inRow = sourceSamples[(y * opsin.PixelsPerRow)..];
                            ReadOnlySpan<float> decRow = decodedSamples[(y * reconstructed.PixelsPerRow)..];

                            for (int x = bx * JxlFrameDimensions.BlockDimensions; x < (bx + acs.CoveredBlocksX) * JxlFrameDimensions.BlockDimensions; x++)
                            {
                                if (x >= decoded.XSize)
                                {
                                    continue;
                                }

                                maxError = MathF.Max(MathF.Abs(inRow[x] - decRow[x]) * inverseMaxError[c], maxError);
                            }
                        }
                    }

                    // Target an error between max_error/2 and max_error.
                    // If the error in the varblock is above the target, increase the qf to
                    // compensate. If the error is below the target, decrease the qf.
                    // However, to avoid an excessive increase of the qf, only do so if the
                    // error is less than half the maximum allowed error.
                    float qfMul =
                        maxError < 0.5f
                        ? maxError * 2.0f
                        : maxError > 1.0f
                            ? maxError
                            : 1.0f;

                    for (int qy = by; qy < by + acs.CoveredBlocksY; qy++)
                    {
                        Span<float> qfRow = quantizationSamples[(qy * quantField.PixelsPerRow)..];

                        for (int qx = bx; qx < bx + acs.CoveredBlocksX; qx++)
                        {
                            qfRow[qx] *= qfMul;
                        }
                    }
                }
            }
        }

        if (!quantizer.SetQuantField(initialQuantDc, quantField, rawQuantField, scratch))
        {
            return false;
        }

        return true;
    }

    public static JxlPlane<float> InitialQuantField(
        Configuration configuration,
        float butteraugliTarget,
        JxlImage3<float> opsin,
        Rectangle rect,
        float rescale,
        JxlPlane<float> mask,
        JxlPlane<float> mask1x1)
    {
        float quantAc = AcQuant / butteraugliTarget;

        return AdaptiveQuantizationMap(configuration, butteraugliTarget, opsin, rect, quantAc * rescale, mask, mask1x1);
    }

    public static bool FindBestQuantizer(
        Configuration configuration,
        JxlFrameHeader frameHeader,
        JxlImage3<float> linear,
        JxlImage3<float> opsin,
        JxlPlane<float> quantField,
        JxlPassesEncoderState encState,
        JxlCmsInterface cms,
        JxlAuxiliaryOutput auxOut,
        double rescale)
    {
        _ = rescale; // This parameter is unused but is present in reference
        JxlCompressionParameters cparams = encState.CompressionParameters;

        if (cparams.MaxErrorMode)
        {
            return FindBestQuantizationMaxError(configuration, frameHeader, opsin, quantField, encState, cms, auxOut);
        }
        else if (linear && cparams.SpeedTier <= JxlSpeedTier.Kitten)
        {
            return FindBestQuantization(configuration, frameHeader, linear, opsin, quantField, encState, cms, auxOut);
        }

        return true;
    }

    /// <summary>
    /// Owns the scratch storage reused by sequential quantization tiles.
    /// </summary>
    private sealed class AdaptiveQuantizationInternal : IDisposable
    {
        /// <summary>
        /// Initializes the tile scratch storage and borrows the caller's output map.
        /// </summary>
        /// <param name="configuration">The configuration that owns the allocator.</param>
        /// <param name="quantizationMap">The output map, which remains owned by the caller.</param>
        public AdaptiveQuantizationInternal(Configuration configuration, JxlPlane<float> quantizationMap)
        {
            this.AdaptiveQuantizationMap = quantizationMap;
            this.PreErosion = new(
                configuration,
                (JxlEncoderParameters.TileDimensionsInBlocks * 2) + 2,
                (JxlEncoderParameters.TileDimensionsInBlocks * 2) + 2);

            try
            {
                this.DiffBuffer = new(configuration, JxlEncoderParameters.TileDimensions + 8, 1);
            }
            catch
            {
                this.PreErosion.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Gets the erosion plane reused for each tile.
        /// </summary>
        public JxlPlane<float> PreErosion { get; }

        /// <summary>
        /// Gets the borrowed block quantization output.
        /// </summary>
        public JxlPlane<float> AdaptiveQuantizationMap { get; }

        /// <summary>
        /// Gets the difference row reused while accumulating four input rows.
        /// </summary>
        public JxlPlane<float> DiffBuffer { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            this.DiffBuffer.Dispose();
            this.PreErosion.Dispose();
        }

        /// <summary>
        /// Computes one tile using the shared difference row and erosion plane.
        /// </summary>
        /// <param name="butteraugliTarget">The requested perceptual error target.</param>
        /// <param name="scale">The quantization scale.</param>
        /// <param name="xyb">The source XYB planes.</param>
        /// <param name="rectIn">The source region.</param>
        /// <param name="rectOut">The tile region in the output map.</param>
        /// <param name="mask">The caller-owned block masking plane.</param>
        /// <param name="mask1x1">The caller-owned full-resolution masking plane.</param>
        /// <returns>Whether the tile could be computed from the supplied regions.</returns>
        public bool TryComputeTile(
            float butteraugliTarget,
            float scale,
            JxlImage3<float> xyb,
            Rectangle rectIn,
            Rectangle rectOut,
            JxlPlane<float> mask,
            JxlPlane<float> mask1x1)
        {
            const float matchGammaOffset = 0.019f;

            if (rectIn.X0() % JxlFrameDimensions.BlockDimensions != 0 || rectIn.Y0() % JxlFrameDimensions.BlockDimensions != 0)
            {
                return false;
            }

            (int width, int height) = (xyb.XSize, xyb.YSize);

            int yStart1x1 = rectIn.Y0() + (rectOut.Y0() * 8);
            int yEnd1x1 = yStart1x1 + (rectOut.Height * 8);

            int xStart1x1 = rectIn.X0() + (rectOut.X0() * 8);
            int xEnd1x1 = xStart1x1 + (rectOut.Width * 8);

            if (rectIn.X0() != 0 && rectOut.X0() == 0)
            {
                xStart1x1 -= 2;
            }

            if (rectIn.X1() < width && rectOut.X1() * 8 == rectIn.Width)
            {
                xEnd1x1 += 2;
            }

            if (rectIn.Y0() != 0 && rectOut.Y0() == 0)
            {
                yStart1x1 -= 2;
            }

            if (rectIn.Y1() < height && rectOut.Y1() * 8 == rectIn.Height)
            {
                yEnd1x1 += 2;
            }

            for (int y = yStart1x1; y < yEnd1x1; y++)
            {
                int y2 = y + 1 < height ? y + 1 : y;
                int y1 = y > 0 ? y - 1 : y;

                Span<float> rowIn = xyb.PlaneRow(1, y);
                Span<float> rowIn1 = xyb.PlaneRow(1, y1);
                Span<float> rowIn2 = xyb.PlaneRow(1, y2);
                Span<float> mask1x1Out = mask1x1.GetRow(y);

                for (int x = xStart1x1; x < xEnd1x1; x++)
                {
                    const float multiplier = 1.0f;
                    const float scaler = 1.0f;
                    const float offset = 0.01f;

                    int x2 = x + 1 < width ? x + 1 : x;
                    int x1 = x > 0 ? x - 1 : x;

                    float @base = 0.25f * ((rowIn2[x] + rowIn1[x]) + (rowIn[x1] + rowIn[x2]));
                    float gammac = RatioOfDerivativesOfCubicRootToSimpleGamma(rowIn[x] + matchGammaOffset);
                    float diff = MathF.Abs(gammac * (rowIn[x] - @base));

                    diff *= scaler;
                    diff = MathF.Log(1.0f + diff); // log1p
                    mask1x1Out[x] = multiplier / (diff + offset);
                }
            }

            int yStart = rectIn.Y0() + (rectOut.Y0() * 8);
            int yEnd = yStart + (rectOut.Height * 8);

            int xStart = rectIn.X0() + (rectOut.X0() * 8);
            int xEnd = xStart + (rectOut.Width * 8);

            if (xStart != 0)
            {
                xStart -= 4;
            }

            if (xEnd != width)
            {
                xEnd += 4;
            }

            if (yStart != 0)
            {
                yStart -= 4;
            }

            if (yEnd != height)
            {
                yEnd += 4;
            }

            if (!this.PreErosion.ShrinkTo((xEnd - xStart) / 4, (yEnd - yStart) / 4))
            {
                return false;
            }

            const float limit = 0.2f;

            for (int y = yStart; y < yEnd; y++)
            {
                int y2 = y + 1 < height ? y + 1 : y;
                int y1 = y > 0 ? y - 1 : y;

                Span<float> rowIn = xyb.PlaneRow(1, y);
                Span<float> rowIn1 = xyb.PlaneRow(1, y1);
                Span<float> rowIn2 = xyb.PlaneRow(1, y2);
                Span<float> rowOut = this.DiffBuffer.GetRow(0);

                // Pass Span<T> variables instead of capturing them as a ref struct
                // variable cannot be captured.
                void ScalarPixel(int x, Span<float> rowIn, Span<float> rowIn1, Span<float> rowIn2, Span<float> rowOut)
                {
                    int x2 = x + 1 < width ? x + 1 : x;
                    int x1 = x > 0 ? x - 1 : x;

                    float @base = 0.25f * ((rowIn2[x] + rowIn1[x]) + (rowIn[x1] + rowIn[x2]));
                    float gammac = RatioOfDerivativesOfCubicRootToSimpleGamma(rowIn[x] + matchGammaOffset);
                    float diff = gammac * (rowIn[x] - @base);

                    // diff = Min(diff², limit)
                    diff *= diff;
                    if (diff >= limit)
                    {
                        diff = limit;
                    }

                    diff = MaskingSqrt(diff);
                    if ((y % 4) != 0)
                    {
                        rowOut[x - xStart] += diff;
                    }
                    else
                    {
                        rowOut[x - xStart] = diff;
                    }
                }

                int x = xStart;

                if (xStart == 0)
                {
                    ScalarPixel(xStart, rowIn, rowIn1, rowIn2, rowOut);
                    x++;
                }

                // SIMD
                Vector<float> matchGammaOffsetV = Vector.Create(matchGammaOffset);
                Vector<float> quarter = Vector.Create(0.25f);

                for (; x + 1 + Vector<float>.Count < xEnd; x += Vector<float>.Count)
                {
                    Vector<float> input = Vector.Create<float>(rowIn[x..]);

                    // Neighbors
                    Vector<float> inputRight = Vector.Create<float>(rowIn[(x + 1)..]);
                    Vector<float> inputLeft = Vector.Create<float>(rowIn[(x - 1)..]);
                    Vector<float> inputTop = Vector.Create<float>(rowIn2[x..]);
                    Vector<float> inputBottom = Vector.Create<float>(rowIn1[x..]);

                    Vector<float> @base = quarter * ((inputRight + inputLeft) + (inputTop + inputBottom));
                    Vector<float> gammacv = RatioOfDerivativesOfCubicRootToSimpleGamma(input + matchGammaOffsetV, invert: false);
                    Vector<float> diff = gammacv * (input - @base);
                    diff *= diff;
                    diff = Vector.Min(diff, Vector.Create(limit));
                    diff = MaskingSqrt(diff);

                    if ((y & 3) != 0)
                    {
                        diff += Vector.Create<float>(rowOut[(x - xStart)..]);
                    }

                    diff.CopyTo(rowOut[(x - xStart)..]);
                }

                // Scalar
                for (; x < xEnd; x++)
                {
                    ScalarPixel(x, rowIn, rowIn1, rowIn2, rowOut);
                }

                if (y % 4 == 3)
                {
                    Span<float> rowDOut = this.PreErosion.GetRow((y - yStart) / 4);

                    for (int qx = 0; qx < (xEnd - xStart) / 4; qx++)
                    {
                        rowDOut[qx] = ((rowOut[qx * 4] + rowOut[(qx * 4) + 1]) + (rowOut[(qx * 4) + 2] + rowOut[(qx * 4) + 3])) * 0.25f;
                    }
                }
            }

            if (xStart % (JxlFrameDimensions.BlockDimensions / 2) != 0 || yStart % (JxlFrameDimensions.BlockDimensions / 2) != 0)
            {
                return false;
            }

            Rectangle fromRect = new(
                x: xStart % 8 == 0 ? 0 : 1,
                y: yStart % 8 == 0 ? 0 : 1,
                width: rectOut.Width * 2,
                height: rectOut.Height * 2);

            if (!FuzzyErosion(butteraugliTarget, fromRect, this.PreErosion, rectOut, this.AdaptiveQuantizationMap))
            {
                return false;
            }

            for (int y = 0; y < rectOut.Height; y++)
            {
                Span<float> aqMapRow = this.AdaptiveQuantizationMap.GetRow(rectOut, y);
                Span<float> maskRow = mask.GetRow(rectOut, y);

                for (int x = 0; x < rectOut.Width; x++)
                {
                    maskRow[x] = ComputeMaskForAcStrategyUse(aqMapRow[x]);
                }
            }

            PerBlockModulations(butteraugliTarget, xyb.Plane(0), xyb.Plane(1), xyb.Plane(2), rectIn, scale, rectOut, this.AdaptiveQuantizationMap);
            return true;
        }
    }
}
