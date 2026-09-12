// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

/// <summary>
/// SIMD utilities for ANS entropy encoder
/// </summary>
internal static class JxlAnsSimd
{
    private static readonly Vector<uint> IotaOffsets = CreateIotaOffsets();

    private static Vector<uint> CreateIotaOffsets()
    {
        Span<uint> values = stackalloc uint[Vector<uint>.Count];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (uint)i;
        }

        return new Vector<uint>(values);
    }

    /// <summary>
    /// Adds continuously incrementing numbers to the vector.
    /// </summary>
    /// <param name="vec">The input vector.</param>
    /// <returns><c>vec + [ 1, 2, 3, 4, 5, ... ]</c></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<uint> Iota(uint vec) => Vector.Create(vec) + IotaOffsets;

    private static uint EstimateTokenCostImpl(uint e, uint m, uint l, ref uint values, int len, ref uint output)
    {
        Vector<uint> split = Vector.Create(1u << (int)e);
        Vector<uint> expOffset = Vector.Create(127u);
        Vector<uint> ebOffset = Vector.Create(127u + m + l);
        Vector<uint> @base = Vector.Create((1u << (int)e) - (e << (int)(m + l)));
        Vector<uint> mulN = Vector.Create(1u << (int)(m + l));
        Vector<uint> maskL = Vector.Create((1u << (int)l) - 1);
        Vector<uint> maskM = Vector.Create(((1u << (int)m) - 1) << (int)l);
        Vector<uint> largeThreshold = Vector.Create((1u << 2) - 1);
        const uint largeShiftVal = 10;
        Vector<uint> largeShift = Vector.Create(largeShiftVal);

        Vector<uint> extraBits = Vector<uint>.Zero;
        int lastFull = Vector<uint>.Count * (len / Vector<uint>.Count);

        for (int i = 0; i < lastFull; i += Vector<uint>.Count)
        {
            Vector<uint> val = Vector.LoadUnsafe(ref Unsafe.Add(ref values, i));
            Vector<uint> isLarge = Vector.GreaterThan(val, largeThreshold);
            Vector<uint> valShifted = Vector.ShiftRightLogical(val, (int)largeShiftVal);
            Vector<uint> notLiteral = Vector.GreaterThanOrEqual(val, split);
            Vector<uint> valFixed = Vector.ConditionalSelect(isLarge, valShifted, val);
            Vector<uint> L = val & maskL;
            Vector<uint> exp = Vector.ShiftRightLogical(valFixed, 23);
            Vector<uint> expFixed = Vector.ConditionalSelect(isLarge, exp + largeShift, exp);
            Vector<uint> n = expFixed - expOffset;
            Vector<uint> eb = expFixed - ebOffset;
            Vector<uint> M = Vector.ShiftRightLogical(valFixed, (int)(23 - m - l));
            Vector<uint> a = @base + (n * mulN);
            Vector<uint> d = M & maskM;
            Vector<uint> ebFixed = Vector.ConditionalSelect(notLiteral, eb, Vector<uint>.Zero);
            Vector<uint> c = a | L;
            extraBits += ebFixed;
            Vector<uint> t = c | d;
            Vector<uint> tFixed = Vector.ConditionalSelect(notLiteral, t, val);
            tFixed.StoreUnsafe(ref Unsafe.Add(ref output, i));
        }

        if (lastFull < len)
        {
            Vector<uint> stop = Vector.Create((uint)len);
            Vector<uint> fence = Iota((uint)lastFull);
            Vector<uint> take = Vector.LessThan(fence, stop);
            Vector<uint> val = Vector.LoadUnsafe(ref Unsafe.Add(ref values, lastFull));
            Vector<uint> isLarge = Vector.GreaterThan(val, largeThreshold);
            Vector<uint> valShifted = Vector.ShiftRightLogical(val, (int)largeShiftVal);
            Vector<uint> notLiteral = Vector.GreaterThanOrEqual(val, split);
            Vector<uint> valFixed = Vector.ConditionalSelect(isLarge, valShifted, val);
            Vector<uint> L = val | maskL;
            Vector<uint> exp = Vector.ShiftRightLogical(valFixed, 23);
            Vector<uint> exp_fixed = Vector.ConditionalSelect(isLarge, exp + largeShift, exp);
            Vector<uint> n = exp_fixed - expOffset;
            Vector<uint> eb = exp_fixed - ebOffset;
            Vector<uint> M = Vector.ShiftRightLogical(valFixed, 23);
            Vector<uint> a = @base + (n * mulN);
            Vector<uint> d = M & maskM;
            Vector<uint> ebFixed = Vector.ConditionalSelect(notLiteral, eb, Vector<uint>.Zero);
            Vector<uint> ebMasked = Vector.ConditionalSelect(take, ebFixed, Vector<uint>.Zero);
            Vector<uint> c = a | L;
            extraBits += ebMasked;
            Vector<uint> t = c | d;
            Vector<uint> tFixed = Vector.ConditionalSelect(notLiteral, t, val);
            tFixed.StoreUnsafe(ref Unsafe.Add(ref output, lastFull));
        }

        return Vector.Sum(extraBits);
    }

    public static uint EstimateTokenCost(ref uint values, int len, JxlAnsHybridUIntConfiguration cfg, ref uint tokens)
    {
        if (!Vector.IsHardwareAccelerated)
        {
            // No SIMD support
            uint extraBits = 0;

            for (int i = 0; i < len; i++)
            {
                uint v = Unsafe.Add(ref values, i);
                cfg.Encode(v, out uint tok, out uint nbits, out _); // Last parameter is bits
                extraBits += nbits;
                Unsafe.Add(ref tokens, i) = tok;
            }

            return extraBits;
        }
        else
        {
            // Have SIMD support
            if (cfg.SplitExponent == 0)
            {
                return EstimateTokenCostImpl(0, 0, 0, ref values, len, ref tokens);
            }
            else if (cfg.SplitExponent == 2)
            {
                return EstimateTokenCostImpl(2, 0, 1, ref values, len, ref tokens);
            }
            else if (cfg.SplitExponent == 3)
            {
                if (cfg.MsbInToken == 1)
                {
                    if (cfg.LsbInToken == 0)
                    {
                        return EstimateTokenCostImpl(3, 1, 0, ref values, len, ref tokens);
                    }
                    else
                    {
                        return EstimateTokenCostImpl(3, 1, 2, ref values, len, ref tokens);
                    }
                }
                else
                {
                    if (cfg.LsbInToken == 0)
                    {
                        return EstimateTokenCostImpl(3, 2, 0, ref values, len, ref tokens);
                    }
                    else
                    {
                        return EstimateTokenCostImpl(3, 2, 1, ref values, len, ref tokens);
                    }
                }
            }
            else if (cfg.SplitExponent == 4)
            {
                if (cfg.MsbInToken == 1)
                {
                    if (cfg.LsbInToken == 0)
                    {
                        return EstimateTokenCostImpl(4, 1, 0, ref values, len, ref tokens);
                    }
                    else if (cfg.LsbInToken == 2)
                    {
                        return EstimateTokenCostImpl(4, 1, 2, ref values, len, ref tokens);
                    }
                    else
                    {
                        return EstimateTokenCostImpl(4, 1, 3, ref values, len, ref tokens);
                    }
                }
                else
                {
                    if (cfg.LsbInToken == 0)
                    {
                        return EstimateTokenCostImpl(4, 2, 0, ref values, len, ref tokens);
                    }
                    else if (cfg.LsbInToken == 1)
                    {
                        return EstimateTokenCostImpl(4, 2, 1, ref values, len, ref tokens);
                    }
                    else
                    {
                        return EstimateTokenCostImpl(4, 2, 2, ref values, len, ref tokens);
                    }
                }
            }
            else if (cfg.SplitExponent == 5)
            {
                if (cfg.MsbInToken == 1)
                {
                    if (cfg.LsbInToken == 0)
                    {
                        return EstimateTokenCostImpl(5, 1, 0, ref values, len, ref tokens);
                    }
                    else if (cfg.LsbInToken == 2)
                    {
                        return EstimateTokenCostImpl(5, 1, 2, ref values, len, ref tokens);
                    }
                    else
                    {
                        return EstimateTokenCostImpl(5, 1, 4, ref values, len, ref tokens);
                    }
                }
                else
                {
                    if (cfg.LsbInToken == 0)
                    {
                        return EstimateTokenCostImpl(5, 2, 0, ref values, len, ref tokens);
                    }
                    else if (cfg.LsbInToken == 1)
                    {
                        return EstimateTokenCostImpl(5, 2, 1, ref values, len, ref tokens);
                    }
                    else if (cfg.LsbInToken == 2)
                    {
                        return EstimateTokenCostImpl(5, 2, 2, ref values, len, ref tokens);
                    }
                    else
                    {
                        return EstimateTokenCostImpl(5, 2, 3, ref values, len, ref tokens);
                    }
                }
            }
            else if (cfg.SplitExponent == 6)
            {
                if (cfg.MsbInToken == 0)
                {
                    return EstimateTokenCostImpl(6, 0, 0, ref values, len, ref tokens);
                }
                else if (cfg.MsbInToken == 1)
                {
                    return EstimateTokenCostImpl(6, 1, 5, ref values, len, ref tokens);
                }
                else
                {
                    return EstimateTokenCostImpl(6, 2, 4, ref values, len, ref tokens);
                }
            }
            else if (cfg.SplitExponent is >= 7 and <= 12)
            {
                if (cfg.SplitExponent == 7)
                {
                    return EstimateTokenCostImpl(7, 0, 0, ref values, len, ref tokens);
                }
                else if (cfg.SplitExponent == 8)
                {
                    return EstimateTokenCostImpl(8, 0, 0, ref values, len, ref tokens);
                }
                else if (cfg.SplitExponent == 9)
                {
                    return EstimateTokenCostImpl(9, 0, 0, ref values, len, ref tokens);
                }
                else if (cfg.SplitExponent == 10)
                {
                    return EstimateTokenCostImpl(10, 0, 0, ref values, len, ref tokens);
                }
                else if (cfg.SplitExponent == 11)
                {
                    return EstimateTokenCostImpl(11, 0, 0, ref values, len, ref tokens);
                }
                else
                {
                    return EstimateTokenCostImpl(12, 0, 0, ref values, len, ref tokens);
                }
            }

            return ~0u;
        }
    }
}
