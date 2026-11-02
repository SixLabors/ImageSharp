// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;

internal static class JxlModularSimd
{
    private const int LargeThreshold = 501;
    private const int LastContext = 16;

    public static ReadOnlySpan<byte> ContextMap =>
    [
        0,  1,  1,  2,  2,  3,  3,  4,  4,  4,  4,  5,  5,  5,  5,  6,  6,  6,
        6,  6,  6,  6,  6,  7,  7,  7,  7,  7,  7,  7,  7,  8,  8,  8,  8,  8,
        8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  8,  9,  9,  9,  9,  9,  9,  9,
        9,  9,  9,  9,  9,  9,  9,  9,  9,  10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
        11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
        11, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
        12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        13, 13, 13, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 16
    ];

    public static ReadOnlySpan<uint> ScalarCutoffs =>
    [
        0, 1, 3, 5, 7, 11, 15, 23, 31,
        47, 63, 95, 127, 191, 255, 392, 500
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Max3(int x, int y, int z) => Math.Max(x, Math.Max(y, z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Min3(int x, int y, int z) => Math.Min(x, Math.Min(y, z));

    public static float EstimateCost(Configuration configuration, JxlModularImage image)
    {
        int histogramCost = 0;
        float histogramCostFraction = 0f;
        int extraBits = 0;

        if (Vector<float>.Count == 1 ||
            Vector<int>.Count == 1 ||
            Vector<uint>.Count == 1)
        {
            // Scalar
            JxlAnsHybridUIntConfiguration config = new();
            int numCutoffs = ScalarCutoffs.Length + 1;

            // stackalloc doesn't work on managed types
            JxlHistogram[] histograms = ArrayHelpers.CreateAndInitialize(numCutoffs, () => new JxlHistogram());

            foreach (JxlModularChannel ch in image.Channels)
            {
                int oneRow = ch.Plane.PixelsPerRow;

                for (int y = 0; y < ch.Height; y++)
                {
                    Span<int> r = ch.GetRow(y);
                    ref int referenceToR = ref MemoryMarshal.GetReference(r);

                    for (int x = 0; x < ch.Width; x++)
                    {
                        int left = x > 0
                            ? r[x - 1]
                            : y > 0
                                ? Unsafe.Add(ref referenceToR, x - oneRow)
                                : 0;

                        int top = y > 0
                            ? Unsafe.Add(ref referenceToR, x - oneRow)
                            : left;

                        int topleft = x > 0 && y > 0
                            ? Unsafe.Add(ref referenceToR, x - 1 - oneRow)
                            : left;

                        int max_diff = Max3(left, top, topleft) - Min3(left, top, topleft);
                        int ctx = 0;

                        foreach (uint c in ScalarCutoffs)
                        {
                            ctx += (max_diff < c) ? 1 : 0;
                        }

                        int res = r[x] - JxlContextPrediction.ClampedGradient(top, left, topleft);
                        config.Encode(JxlPackSigned.PackUnsigned(res), out uint token, out uint nbits, out uint bits);
                        histograms[ctx].Add((int)token);
                        extraBits += (int)nbits;
                    }
                }

                foreach (JxlHistogram h in histograms)
                {
                    float floatingCost = h.ComputeShannonEntropy();
                    int integerCost = (int)floatingCost;
                    histogramCost += integerCost;
                    histogramCostFraction += floatingCost - integerCost;
                    h.Clear();
                }
            }
        }
        else
        {
            // Vectorized
            Vector<uint> one = Vector<uint>.One;
            Vector<uint> split = Vector.Create(16u);
            Vector<uint> expOffset2 = Vector.Create(129u); // 127 + 2
            Vector<uint> tokenBias = Vector.Create(8u);
            Vector<uint> tokenMul = Vector.Create(4u);
            Vector<uint> msbMask = Vector.Create(3u);
            Vector<uint> maxDiffCap = Vector.Create((uint)(LargeThreshold - 1));
            Vector<uint> lanes = Vector.Create((uint)Vector<uint>.Count);
            Vector<uint> iota = JxlSimdUtils.Iota(0u);
            Vector<uint> largeThreshold = Vector.Create((uint)((1 << 2) - 1));
            const int largeShiftValue = 10;
            Vector<uint> largeShift = Vector.Create((uint)largeShiftValue);

            int maxWidth = 0;

            foreach (JxlModularChannel channel in image.Channels)
            {
                if (channel.Height == 0)
                {
                    // No pixels. So skip it.
                    continue;
                }

                maxWidth = Math.Max(maxWidth, channel.Width);
            }

            maxWidth = JxlMath.RoundUpTo(maxWidth, Vector<uint>.Count);
            maxWidth = Math.Max(maxWidth, 2 * Vector<uint>.Count);

            using IMemoryOwner<uint> buffer = configuration.MemoryAllocator.Allocate<uint>(maxWidth * 2);

            Span<uint> maxDiffRow = buffer.GetSpan();
            Span<uint> tokenRow = maxDiffRow[maxWidth..];
            Span<int> primer = MemoryMarshal.Cast<uint, int>(maxDiffRow);
            Span<int> topPrimer = primer[maxWidth..];

            JxlAnsHybridUIntConfiguration config = new();
            JxlHistogram[] histograms = ArrayHelpers.CreateAndInitialize(LastContext + 1, () => new JxlHistogram(32 * 4));
            Vector<uint> extraBitsLanes = Vector<uint>.Zero;

            foreach (JxlModularChannel ch in image.Channels)
            {
                if (ch.Width == 0 || ch.Height == 0)
                {
                    continue;
                }

                Span<int> r = ch.GetRow(0);
                Span<int> last = primer;

                primer[0] = 0;

                Vector.Create<int>(r).CopyTo(primer[1..]);
                Vector<uint> pos = iota;
                Vector<uint> lastPos = Vector.Create((uint)ch.Width);

                for (int x = 0; x < ch.Width - Vector<int>.Count; x += Vector<int>.Count)
                {
                    Vector<int> left = Vector.Create<int>(last);
                    Vector<int> central = Vector.Create<int>(r[x..]);

                    Vector<uint> ures = (central - left).As<int, uint>();
                    Vector<uint> packed = (ures << 1) ^ ((~ures >> 31) - one);

                    Vector<uint> isLarge = Vector.GreaterThan(packed, largeThreshold);
                    Vector<uint> packedShifted = packed >> largeShiftValue;
                    Vector<uint> notLiteral = Vector.GreaterThanOrEqual(packed, split);
                    Vector<uint> packedFixed = Vector.ConditionalSelect(isLarge, packedShifted, packed);

                    Vector<uint> v = Vector.ConvertToSingle(packedFixed).As<float, uint>();
                    Vector<uint> ebRaw = (v >>> 23) - expOffset2;
                    Vector<uint> eb = Vector.ConditionalSelect(isLarge, ebRaw + largeShift, ebRaw);

                    Vector<uint> token = (tokenBias + (eb * tokenMul)) + ((v >>> 21) & msbMask);
                    Vector<uint> tailMask = Vector.LessThan(pos, lastPos);
                    Vector<uint> ebFixed = Vector.ConditionalSelect(notLiteral, eb, Vector<uint>.Zero);
                    Vector<uint> tokenFixed = Vector.ConditionalSelect(notLiteral, token, packed);
                    extraBitsLanes += Vector.ConditionalSelect(tailMask, ebFixed, Vector<uint>.Zero);

                    tokenFixed.CopyTo(tokenRow[x..]);
                    pos += lanes;
                    last = r[(x + Vector<int>.Count - 1)..];
                }

                for (int x = 0; x < ch.Width; x++)
                {
                    histograms[0].FastAdd((int)tokenRow[x]);
                }

                for (int y = 1; y < ch.Height; y++)
                {
                    r = ch.GetRow(y);
                    Span<int> t = ch.GetRow(y - 1);
                    primer[0] = t[0];

                    Vector.Create<int>(r).CopyTo(primer[1..]);

                    topPrimer[0] = t[0];
                    Vector.Create<int>(t).CopyTo(topPrimer[1..]);

                    Span<int> topLast = topPrimer;
                    pos = iota;

                    for (int x = 0; x < ch.Width; x += Vector<int>.Count)
                    {
                        // Loading neighbors
                        Vector<int> left = Vector.Create<int>(last);
                        Vector<int> central = Vector.Create<int>(r[x..]);
                        Vector<int> topleft = Vector.Create<int>(topLast);
                        Vector<int> top = Vector.Create<int>(t[x..]);

                        Vector<int> lGeT = Vector.GreaterThanOrEqual(left, top);
                        Vector<int> m = Vector.ConditionalSelect(lGeT, top, left);
                        Vector<int> M = Vector.ConditionalSelect(lGeT, left, top);
                        Vector<int> maxx = Vector.Max(topleft, M);
                        Vector<int> minn = Vector.Min(topleft, m);
                        Vector<uint> maxDiff = (maxx - minn).As<int, uint>();

                        Vector.Min(maxDiff, maxDiffCap).CopyTo(maxDiffRow[x..]);

                        Vector<int> overshoot = Vector.LessThan(topleft, m);
                        Vector<int> undershoot = Vector.GreaterThan(topleft, M);
                        Vector<int> grad = ((top.As<int, uint>() + left.As<int, uint>()) - topleft.As<int, uint>()).As<uint, int>();

                        Vector<int> prediction = Vector.ConditionalSelect(undershoot, m, Vector.ConditionalSelect(overshoot, M, grad));
                        Vector<uint> ures = (central - prediction).As<int, uint>();
                        Vector<uint> packed = (ures << 1) ^ ((~ures >> 31) - one);
                        Vector<uint> isLarge = Vector.GreaterThan(packed, largeThreshold);
                        Vector<uint> packedShifted = packed >> largeShiftValue;
                        Vector<uint> notLiteral = Vector.GreaterThanOrEqual(packed, split);
                        Vector<uint> packedFixed = Vector.ConditionalSelect(isLarge, packedShifted, packed);
                        Vector<uint> v = Vector.ConvertToSingle(packedFixed).As<float, uint>();
                        Vector<uint> ebRaw = (v >> 23) - expOffset2;
                        Vector<uint> eb = Vector.ConditionalSelect(isLarge, ebRaw + largeShift, ebRaw);
                        Vector<uint> token = (tokenBias + (eb * tokenMul)) + ((v >> 21) & msbMask);
                        Vector<uint> tailMask = Vector.LessThan(pos, lastPos);
                        Vector<uint> ebFixed = Vector.ConditionalSelect(notLiteral, eb, Vector<uint>.Zero);
                        Vector<uint> tokenFixed = Vector.ConditionalSelect(notLiteral, token, packed);

                        extraBitsLanes += Vector.ConditionalSelect(tailMask, ebFixed, Vector<uint>.Zero);
                        tokenFixed.CopyTo(tokenRow[x..]);

                        pos += lanes;
                        last = r[(x + Vector<int>.Count - 1)..];
                        topLast = t[(x + Vector<int>.Count - 1)..];
                    }

                    for (int x = 0; x < ch.Width; x++)
                    {
                        int ctx = ContextMap[(int)maxDiffRow[x]];
                        histograms[ctx].FastAdd((int)tokenRow[x]);
                    }
                }

                foreach (JxlHistogram h in histograms)
                {
                    h.Condition();

                    float floatingCost = h.ComputeShannonEntropy();
                    int integerCost = (int)floatingCost;

                    histogramCost += integerCost;
                    histogramCostFraction += floatingCost - integerCost;

                    h.Clear();
                }
            }

            extraBits = (int)Vector.Sum(extraBitsLanes);
        }

        int totalCost = extraBits + histogramCost + (int)histogramCostFraction;
        return totalCost;
    }
}
