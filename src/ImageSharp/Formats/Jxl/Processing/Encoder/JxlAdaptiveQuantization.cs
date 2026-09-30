// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.Cms;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.AuxiliaryOutput;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;
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

    private static Vector<float> HfModulationVectorized(int x, int y, JxlImageF xybY, Rectangle rect, Vector<float> outVal)
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

    public static Vector<float> HfModulation(int x, int y, JxlImageF xybY, Rectangle rect, Vector<float> outVal)
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

    private static void PerBlockModulations(float butteraugliTarget, JxlPlane<float> xybX, JxlPlane<float> xybY, JxlPlane<float> xybB, Rectangle rectIn, float scale, Rectangle rectOut, JxlImageF output)
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

    // Look for smooth areas near the area of degradation.
    // If the areas are generally smooth, don't do masking.
    // Output is downsampled by 2x.
    public static bool FuzzyErosion(float butteraugliTarget, Rectangle fromRect, JxlImageF from, Rectangle toRect, JxlImageF to)
    {
        int width = from.XSize;
        int height = from.YSize;
        const int step = 1;

        if (toRect.Width != fromRect.Width || toRect.Height != fromRect.Height)
        {
            return false;
        }

        ReadOnlySpan<float> mulBase = [0.125f, 0.1f, 0.09f, 0.06f];
        ReadOnlySpan<float> mulAdd = [0.0f, -0.1f, -0.09f, -0.06f];
        float mul = 0f;

        if (butteraugliTarget < 2.0f)
        {
            mul = (2.0f - butteraugliTarget) * (1.0f / 2.0f);
        }

        InlineArray4<float> multipliers = default;
        float normSum = 0;

        for (int ii = 0; ii < 4; ii++)
        {
            multipliers[ii] = mulBase[ii] + (mul * mulAdd[ii]);
            normSum += multipliers[ii];
        }

        const float total = 0.29959705784054957f;

        for (int ii = 0; ii < 4; ii++)
        {
            multipliers[ii] *= total / normSum;
        }

        for (int fy = 0; fy < fromRect.Height; fy++)
        {
            int y = fy + fromRect.Y0();
            int ym1 = y >= step ? y - step : y;
            int yp1 = y + step < height ? y + step : y;

            Span<float> rowt = from.GetRow(ym1);
            Span<float> row = from.GetRow(y);
            Span<float> rowb = from.GetRow(yp1);
            Span<float> rowOut = to.GetRow(toRect, fy / 2);

            for (int fx = 0; fx < fromRect.Height; fx++)
            {
                int x = fx + fromRect.X0();
                int xm1 = x >= step ? x - step : x;
                int xp1 = x + step < width ? x + step : x;

                // float min[4] = { row[x], row[xm1], row[xp1], rowt[xm1] };
                InlineArray4<float> min = default;
                min[0] = row[x];
                min[1] = row[xm1];
                min[2] = row[xp1];
                min[3] = rowt[xm1];

                if (min[0] > min[1])
                {
                    RuntimeUtility.Swap(ref min[0], ref min[1]);
                }

                if (min[0] > min[2])
                {
                    RuntimeUtility.Swap(ref min[0], ref min[2]);
                }

                if (min[0] > min[3])
                {
                    RuntimeUtility.Swap(ref min[0], ref min[3]);
                }

                if (min[1] > min[2])
                {
                    RuntimeUtility.Swap(ref min[1], ref min[2]);
                }

                if (min[1] > min[3])
                {
                    RuntimeUtility.Swap(ref min[1], ref min[3]);
                }

                if (min[2] > min[3])
                {
                    RuntimeUtility.Swap(ref min[2], ref min[3]);
                }

                // remaining 5 values of a 3x3 neighborhood
                ref float rmin0 = ref min[0];
                ref float rmin1 = ref min[1];
                ref float rmin2 = ref min[2];
                ref float rmin3 = ref min[3];
                StoreMin4(rowt[x], ref rmin0, ref rmin1, ref rmin2, ref rmin3);
                StoreMin4(rowt[xp1], ref rmin0, ref rmin1, ref rmin2, ref rmin3);
                StoreMin4(rowb[xm1], ref rmin0, ref rmin1, ref rmin2, ref rmin3);
                StoreMin4(rowb[x], ref rmin0, ref rmin1, ref rmin2, ref rmin3);
                StoreMin4(rowb[xp1], ref rmin0, ref rmin1, ref rmin2, ref rmin3);

                float v = (multipliers[0] * min[0]) + (multipliers[1] * min[1]) +
                          (multipliers[2] * min[2]) + (multipliers[3] * min[3]);

                if (fx % 2 == 0 && fy % 2 == 0)
                {
                    rowOut[fx / 2] = v;
                }
                else
                {
                    rowOut[fx / 2] += v;
                }
            }
        }

        return true;
    }

    private static void Blur1x1Masking(Configuration configuration, ref JxlImageF mask1x1, Rectangle rect)
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

        JxlImageF temp = new(configuration, rect.Width, rect.Height);

        if (!JxlConvolve.Symmetric5(mask1x1, in rect, ref weights, temp, temp.GetRectangle()))
        {
            throw new InvalidOperationException("Symmetric5 convolution failed");
        }

        mask1x1 = temp;
    }

    public static JxlImageF AdaptiveQuantizationMap(Configuration configuration, float butteraugliTarget, JxlImageF xyb, Rectangle rect, float scale, out JxlImageF mask, out JxlImageF mask1x1)
    {
        if ((rect.Width & 7) != 0)
        {
            throw new ArgumentException("Rectangle width must be divisible by 8", nameof(rect));
        }

        if ((rect.Height & 7) != 0)
        {
            throw new ArgumentException("Rectangle height must be divisible by 8", nameof(rect));
        }

        AdaptiveQuantizationInternal impl = new();

        int xSizeBlocks = rect.Width / 8;
        int ySizeBlocks = rect.Height / 8;

        impl.AdaptiveQuantizationMap = new JxlImageF(configuration, xSizeBlocks, ySizeBlocks);
        mask = new JxlImageF(configuration, xSizeBlocks, ySizeBlocks);
        mask1x1 = new JxlImageF(configuration, xyb.XSize, xyb.YSize);

        // out variables cannot be captured in a local function
        JxlImageF maskRef = mask;
        JxlImageF mask1x1Ref = mask1x1;

        impl.InitializeBuffer(configuration);

        void ProcessTile(int idx)
        {
            int numTilesX = JxlMath.DivCeil(xSizeBlocks, JxlEncoderParameters.TileDimensionsInBlocks);

            int tx = (int)(idx % (uint)numTilesX);
            int ty = (int)(idx / (uint)numTilesX);

            int by0 = ty * JxlEncoderParameters.TileDimensionsInBlocks;
            int by1 = Math.Min((ty + 1) * JxlEncoderParameters.TileDimensionsInBlocks, ySizeBlocks);

            int bx0 = tx * JxlEncoderParameters.TileDimensionsInBlocks;
            int bx1 = Math.Min((tx + 1) * JxlEncoderParameters.TileDimensionsInBlocks, xSizeBlocks);

            Rectangle rectOut = new(bx0, by0, bx1 - bx0, by1 - by0);

            if (!impl.TryComputeTile(butteraugliTarget, scale, xyb, rect, rectOut, idx, maskRef, mask1x1Ref))
            {
                throw new InvalidOperationException("Couldn't process tile");
            }
        }

        int numTiles = JxlMath.DivCeil(xSizeBlocks, JxlEncoderParameters.TileDimensionsInBlocks) *
                       JxlMath.DivCeil(ySizeBlocks, JxlEncoderParameters.TileDimensionsInBlocks);

        _ = Parallel.For(0, numTiles, configuration.GetParallelOptions(), ProcessTile);
        Blur1x1Masking(configuration, ref mask1x1, rect);

        return impl.AdaptiveQuantizationMap!;
    }

    private static JxlImageF TileDistMap(Configuration configuration, JxlImageF distMap, int tileSize, int margin, JxlAcStrategyImage acStrategy)
    {
        int tileXSize = (distMap.XSize + tileSize - 1) / tileSize;
        int tileYSize = (distMap.YSize + tileSize - 1) / tileSize;

        JxlImageF tileDistMap = new(configuration, tileXSize, tileYSize);

        const float borderMul = 0.98f;
        const float cornerMul = 0.7f;
        const float tileNorm = 1.2f;

        for (int tileY = 0; tileY < tileYSize; tileY++)
        {
            JxlAcStrategyRow acStrategyRow = acStrategy.GetRow(tileY);
            Span<float> distRow = tileDistMap.GetRow(tileY);

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

                    ReadOnlySpan<float> row = distMap.GetRow(y);

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

                distRow[tileX] = tileDist;

                for (int iy = 0; iy < acs.CoveredBlocksY; iy++)
                {
                    for (int ix = 0; ix < acs.CoveredBlocksX; ix++)
                    {
                        tileDistMap.GetRow(tileY + iy)[tileX + ix] = tileDist;
                    }
                }
            }
        }

        return tileDistMap;
    }

    private static JxlImageBundle RoundtripImage(Configuration configuration, JxlFrameHeader frameHeader, JxlImage3F opsin, JxlPassesEncoderState encState, JxlCmsInterface cms)
    {
        JxlPassesDecoderState decState = new(frameHeader, configuration);

        decState.OutputEncodingInfo.SetFromMetadata(encState.Shared.Metadata);
        decState.Shared = encState.Shared;

        if (opsin.YSize % JxlFrameDimensions.BlockDimensions != 0)
        {
            throw new InvalidOperationException("The opsin height must be divisible by the block dimension.");
        }

        int xSizeGroups = JxlMath.DivCeil(opsin.XSize, JxlFrameDimensions.GroupDimensions);
        int ySizeGroups = JxlMath.DivCeil(opsin.YSize, JxlFrameDimensions.GroupDimensions);
        int numGroups = xSizeGroups * ySizeGroups;

        int numSpecialFrames = encState.SpecialFrames.Count;
        int numPasses = encState.ProgressiveSplitter.GetNumPasses();

        JxlModularFrameEncoder modularFrameEncoder = JxlModularFrameEncoder.Create(configuration, frameHeader, encState.CParams, false);

        InitializePassesEncoder(configuration, frameHeader, opsin, opsin.GetRectangle(), cms, encState, modularFrameEncoder, null);

        decState.Initialize(frameHeader);
        decState.InitializeForAc(configuration, numPasses);

        JxlImageBundle decoded = new(configuration, encState.Shared.Metadata.M)
        {
            Origin = frameHeader.FrameOrigin
        };

        JxlImage3F tmp = new(configuration, opsin.XSize, opsin.YSize);

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

        JxlImageMetadata metadata = decoded.Metadata!;

        decState.PreparePipeline(frameHeader, encState.Shared.Metadata.ImageMetadata!, decoded, options);

        JxlGroupDecoderCache[] groupDecCaches = new JxlGroupDecoderCache[numThreads];

        decState.RenderPipeline.PrepareForThreads(numThreads, useGroupIds: false);

        void ProcessGroup(int groupIndex)
        {
            if (frameHeader.LoopFilter!.EpfIterations > 0)
            {
                JxlLoopFilter.ComputeSigma(
                    decState.Shared.FrameDimensions.BlockGroupRect(groupIndex),
                    decState);
            }

            RenderPipelineInput input = decState.RenderPipeline.GetInputBuffers(groupIndex, groupIndex);

            JxlGroupDecoder.DecodeGroupForRoundtrip(
                frameHeader,
                encState.Coeffs,
                groupIndex,
                decState,
                groupDecCaches[groupIndex],
                groupIndex,
                input,
                null,
                null);

            for (int c = 0; c < metadata.ExtraChannelCount; c++)
            {
                (JxlImageF plane, Rectangle rect) = input.GetBuffer(3 + c);
                JxlImageOperations.FillPlane(0.0f, plane, rect);
            }

            input.Done();
        }

        _ = Parallel.For(0, numGroups, configuration.GetParallelOptions(), ProcsesGr);
        encState.SpecialFrames.RemoveRange(numSpecialFrames, encState.SpecialFrames.Count - numSpecialFrames);

        return decoded;
    }

    public static float InitialQuantDc(float butteraugliTarget)
    {
        const float dcMul = 0.3f; // Butteraugli target where non-linearity kicks in
        float butteraugliTargetDc = MathF.Max(
            0.5f * butteraugliTarget,
            MathF.Min(butteraugliTarget, dcMul * MathF.Pow((1.0f / dcMul) * butteraugliTarget, DcQuantPow)));

        return MathF.Min(DcQuant / butteraugliTargetDc, 50f);
    }

    public static bool AdjustQuantField(JxlAcStrategyImage acStrategy, Rectangle rect, float butteraugliTarget, JxlImageF quantField)
    {
        int stride = quantField.PixelsPerRow;
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
            JxlAcStrategyRow acStrategyRow = acStrategy.GetRow(in rect, y);
            Span<float> quantRow = quantField.GetRow(rect, y);

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

                for (int iy = 0; iy < acs.CoveredBlocksY; y++)
                {
                    for (int ix = 0; ix < acs.CoveredBlocksX; x++)
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
                    for (int ix = 0; ix < acs.CoveredBlocksX; ix++)
                    {
                        quantRow[x + ix + (iy * stride)] = max;
                    }
                }
            }
        }

        return true;
    }

    public static bool FindBestQuantization(Configuration configuration, JxlFrameHeader frameHeader, JxlImage3F linear, JxlImage3F opsin, JxlImageF quantField, JxlPassesEncoderState encState, JxlCmsInterface cms, JxlAuxiliaryOutput auxOut)
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
        JxlImageI rawQuantField = encState.Shared.RawQuantField;
        float butteraugliTarget = cparams.ButteraugliDistance;
        float originalButteraugli = cparams.OriginalButteraugliDistance;

        ButteraugliParameters parameters = new();
        JxlCustomTransferFunction tf = frameHeader.Metadata!.ImageMetadata!.ColorEncoding!.TransferFunction;

        parameters.IntensityTarget = tf.IsPq || tf.IsHlg ? frameHeader.Metadata.ImageMetadata.IntensityTarget : 0;

        ButteraugliJxlComparator comparator = new(parameters, cms);
        comparator.SetReferenceImage(configuration, linear);

        bool lowerIsBetter = comparator.GoodQualityScore < comparator.BadQualityScore;
        float initialQuantDc = InitialQuantDc(butteraugliTarget);

        if (!AdjustQuantField(encState.Shared.AcStrategy, quantField.GetRectangle(), originalButteraugli, quantField))
        {
            return false;
        }

        using JxlImageF initialQuantField = new(configuration, quantField.XSize, quantField.YSize);

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

        if (qfHigher / qfLower < 253)
        {
            return false;
        }

        const int originalComparisonRound = 1;
        int iters = DefaultButteraugliIterations;

        if (cparams.SpeedTier <= JxlSpeedTier.Tortoise)
        {
            iters = MaxButteraugliIterations;
        }

        JxlImageF tileDistmap = new();

        for (int i = 0; i < iters + 1; i++)
        {
            if (!quantizer.SetQuantField(configuration, initialQuantDc, quantField, rawQuantField))
            {
                return false;
            }

            JxlImageBundle decLinear = RoundtripImage(configuration, frameHeader, opsin, encState, cms);

            JxlImageF diffmap = new();
            comparator.CompareWith(configuration, decLinear, diffmap, out float score);

            if (!lowerIsBetter)
            {
                score = -score;
                ScoreImage(-1.0f, diffmap);
            }

            tileDistmap = TileDistMap(configuration, diffmap, 8 * cparams.Resampling, 0, encState.Shared.AcStrategy);

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
                    Span<float> rowQ = quantField.GetRow(y);
                    Span<float> rowInit = initialQuantField.GetRow(y);

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
                    Span<float> rowDist = tileDistmap.GetRow(y);
                    Span<float> rowQ = quantField.GetRow(y);

                    for (int x = 0; x < quantField.XSize; x++)
                    {
                        float diff = rowDist[x] / originalButteraugli;

                        if (diff > 1.0f)
                        {
                            float old = rowQ[x];
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
                    Span<float> rowDist = tileDistmap.GetRow(y);
                    Span<float> rowQ = quantField.GetRow(y);

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

                        if (rowQ[x] < qfHigher)
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

        if (!quantizer.SetQuantField(configuration, initialQuantDc, quantField, rawQuantField))
        {
            return false;
        }

        return true;
    }

    public static bool FindBestQuantizationMaxError(Configuration configuration, JxlFrameHeader frameHeader, JxlImage3F opsin, JxlImageF quantField, JxlPassesEncoderState encState, JxlCmsInterface cms, JxlAuxiliaryOutput auxOut)
    {
        JxlCompressionParameters cparams = encState.CompressionParameters;
        JxlQuantizer quantizer = encState.Shared.Quantizer;
        JxlImageI rawQuantField = encState.Shared.RawQuantField;

        float initialQuantDc = 16f * MathF.Sqrt(0.1f / cparams.ButteraugliDistance);

        if (!AdjustQuantField(encState.Shared.AcStrategy, quantField.GetRectangle(), cparams.OriginalButteraugliDistance, quantField))
        {
            return false;
        }

        InlineArray3<float> inverseMaxError = default;
        inverseMaxError[0] = 1.0f / cparams.MaxError[0];
        inverseMaxError[1] = 1.0f / cparams.MaxError[1];
        inverseMaxError[2] = 1.0f / cparams.MaxError[2];

        for (int i = 0; i < MaxButteraugliIterations + 1; i++)
        {
            if (!quantizer.SetQuantField(configuration, initialQuantDc, quantField, rawQuantField))
            {
                return false;
            }

            JxlImageBundle decoded = RoundtripImage(configuration, frameHeader, opsin, encState, cms);

            for (int by = 0; by < encState.Shared.FrameDimensions.YSizeBlocks; by++)
            {
                JxlAcStrategyRow acStrategyRow = encState.Shared.AcStrategy.GetRow(by);

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
                        for (int y = by * JxlFrameDimensions.BlockDimensions; y < (by + acs.CoveredBlocksY) * JxlFrameDimensions.BlockDimensions; y++)
                        {
                            if (y >= decoded.YSize)
                            {
                                continue;
                            }

                            Span<float> inRow = opsin.PlaneRow(c, y);
                            Span<float> decRow = decoded.Color!.PlaneRow(c, y);

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
                        Span<float> qfRow = quantField.GetRow(qy);

                        for (int qx = bx; qx < bx + acs.CoveredBlocksX; qx++)
                        {
                            qfRow[qx] *= qfMul;
                        }
                    }
                }
            }
        }

        if (!quantizer.SetQuantField(configuration, initialQuantDc, quantField, rawQuantField))
        {
            return false;
        }

        return true;
    }

    public static JxlImageF InitialQuantField(Configuration configuration, float butteraugliTarget, JxlImage3F opsin, Rectangle rect, float rescale, JxlImageF mask, JxlImageF mask1x1)
    {
        float quantAc = AcQuant / butteraugliTarget;

        return AdaptiveQuantizationMap(configuration, butteraugliTarget, opsin, rect, quantAc * rescale, mask, mask1x1);
    }

    public static bool FindBestQuantizer(
        Configuration configuration,
        JxlFrameHeader frameHeader,
        JxlImage3F linear,
        JxlImage3F opsin,
        JxlImageF quantField,
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

    private sealed class AdaptiveQuantizationInternal
    {
        public List<JxlImageF> PreErosion { get; set; } = [];

        public JxlImageF? AdaptiveQuantizationMap { get; set; }

        public JxlImageF? DiffBuffer { get; set; }

        public void InitializeBuffer(Configuration configuration)
        {
            int numThreads = configuration.MaxDegreeOfParallelism == -1 ? Environment.ProcessorCount : configuration.MaxDegreeOfParallelism;

            this.DiffBuffer = new(configuration, JxlEncoderParameters.TileDimensions + 8, numThreads);

            for (int i = this.PreErosion.Count; i < numThreads; i++)
            {
                this.PreErosion.Add(
                    new JxlImageF(
                        configuration,
                        (JxlEncoderParameters.TileDimensionsInBlocks * 2) + 2,
                        (JxlEncoderParameters.TileDimensionsInBlocks * 2) + 2));
            }
        }

        public bool TryComputeTile(float butteraugliTarget, float scale, JxlImage3F xyb, Rectangle rectIn, Rectangle rectOut, int parallelIndex, JxlImageF mask, JxlImageF mask1x1)
        {
            const float matchGammaOffset = 0.019f;

            if (this.DiffBuffer is null)
            {
                return false;
            }

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

            if (!this.PreErosion[parallelIndex].ShrinkTo((xEnd - xStart) / 4, (yEnd - yStart) / 4))
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
                Span<float> rowOut = this.DiffBuffer!.GetRow(parallelIndex);

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
                    Span<float> rowDOut = this.PreErosion[parallelIndex].GetRow((y - yStart) / 4);

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

            if (!FuzzyErosion(butteraugliTarget, fromRect, this.PreErosion[parallelIndex], rectOut, this.AdaptiveQuantizationMap!))
            {
                return false;
            }

            for (int y = 0; y < rectOut.Height; y++)
            {
                Span<float> aqMapRow = this.AdaptiveQuantizationMap!.GetRow(rectOut, y);
                Span<float> maskRow = mask.GetRow(rectOut, y);

                for (int x = 0; x < rectOut.Width; x++)
                {
                    maskRow[x] = ComputeMaskForAcStrategyUse(aqMapRow[x]);
                }
            }

            PerBlockModulations(butteraugliTarget, xyb.Plane(0), xyb.Plane(1), xyb.Plane(2), rectIn, scale, rectOut, this.AdaptiveQuantizationMap!);
            return true;
        }
    }
}
