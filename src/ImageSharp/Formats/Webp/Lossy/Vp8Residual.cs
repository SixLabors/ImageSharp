// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// On-the-fly info about the current set of residuals.
    /// </summary>
    internal class Vp8Residual
    {
        private readonly byte[] scratch = new byte[32];

        private readonly ushort[] scratchUShort = new ushort[16];

        public int First { get; set; }

        public int Last { get; set; }

        public int CoeffType { get; set; }

        public short[] Coeffs { get; } = new short[16];

        public Vp8BandProbas[] Prob { get; set; }

        public Vp8Stats[] Stats { get; set; }

        public Vp8Costs[] Costs { get; set; }

        public void Init(int first, int coeffType, Vp8EncProba prob)
        {
            this.First = first;
            this.CoeffType = coeffType;
            this.Prob = prob.Coeffs[this.CoeffType];
            this.Stats = prob.Stats[this.CoeffType];
            this.Costs = prob.RemappedCosts[this.CoeffType];
            this.Coeffs.AsSpan().Clear();
        }

        public void SetCoeffs(Span<short> coeffs)
        {
#if SUPPORTS_RUNTIME_INTRINSICS
            if (Sse2.IsSupported)
            {
                ref short coeffsRef = ref MemoryMarshal.GetReference(coeffs);
                Vector128<byte> c0 = Unsafe.As<short, Vector128<byte>>(ref coeffsRef);
                Vector128<byte> c1 = Unsafe.As<short, Vector128<byte>>(ref Unsafe.Add(ref coeffsRef, 8));

                // Use SSE2 to compare 16 values with a single instruction.
                Vector128<sbyte> m0 = Sse2.PackSignedSaturate(c0.AsInt16(), c1.AsInt16());
                Vector128<sbyte> m1 = Sse2.CompareEqual(m0, Vector128<sbyte>.Zero);

                // Get the comparison results as a bitmask into 16bits. Negate the mask to get
                // the position of entries that are not equal to zero. We don't need to mask
                // out least significant bits according to res->first, since coeffs[0] is 0
                // if res->first > 0.
                uint mask = 0x0000ffffu ^ (uint)Sse2.MoveMask(m1);

                // The position of the most significant non-zero bit indicates the position of
                // the last non-zero value.
                this.Last = mask != 0 ? Numerics.Log2(mask) : -1;
            }
            else
#endif
            {
                int n;
                this.Last = -1;
                for (n = 15; n >= 0; --n)
                {
                    if (coeffs[n] != 0)
                    {
                        this.Last = n;
                        break;
                    }
                }
            }

            coeffs.Slice(0, 16).CopyTo(this.Coeffs);
        }

        // Simulate block coding, but only record statistics.
        // Note: no need to record the fixed probas.
        public int RecordCoeffs(int ctx)
        {
            int n = this.First;
            Vp8StatsArray s = this.Stats[n].Stats[ctx];
            if (this.Last < 0)
            {
                this.RecordStats(0, s, 0);
                return 0;
            }

            while (n <= this.Last)
            {
                int v;
                this.RecordStats(1, s, 0);  // order of record doesn't matter
                while ((v = this.Coeffs[n++]) == 0)
                {
                    this.RecordStats(0, s, 1);
                    s = this.Stats[WebpConstants.Vp8EncBands[n]].Stats[0];
                }

                this.RecordStats(1, s, 1);
                bool bit = (uint)(v + 1) > 2u;
                if (this.RecordStats(bit ? 1 : 0, s, 2) == 0)
                {
                    // v = -1 or 1
                    s = this.Stats[WebpConstants.Vp8EncBands[n]].Stats[1];
                }
                else
                {
                    v = Math.Abs(v);
                    if (v > WebpConstants.MaxVariableLevel)
                    {
                        v = WebpConstants.MaxVariableLevel;
                    }

                    int bits = WebpLookupTables.Vp8LevelCodes[v - 1][1];
                    int pattern = WebpLookupTables.Vp8LevelCodes[v - 1][0];
                    int i;
                    for (i = 0; (pattern >>= 1) != 0; i++)
                    {
                        int mask = 2 << i;
                        if ((pattern & 1) != 0)
                        {
                            this.RecordStats((bits & mask) != 0 ? 1 : 0, s, 3 + i);
                        }
                    }

                    s = this.Stats[WebpConstants.Vp8EncBands[n]].Stats[2];
                }
            }

            if (n < 16)
            {
                this.RecordStats(0, s, 0);
            }

            return 1;
        }

        public int GetResidualCost(int ctx0)
        {
            int n = this.First;
            int p0 = this.Prob[n].Probabilities[ctx0].Probabilities[0];
            Vp8Costs[] costs = this.Costs;
            Vp8CostArray t = costs[n].Costs[ctx0];

            // bitCost(1, p0) is already incorporated in t[] tables, but only if ctx != 0
            // (as required by the syntax). For ctx0 == 0, we need to add it here or it'll
            // be missing during the loop.
            int cost = ctx0 == 0 ? LossyUtils.Vp8BitCost(1, (byte)p0) : 0;

            if (this.Last < 0)
            {
                return LossyUtils.Vp8BitCost(0, (byte)p0);
            }

#if SUPPORTS_RUNTIME_INTRINSICS
            if (Avx2.IsSupported)
            {
                Span<byte> ctxs = this.scratch.AsSpan(0, 16);
                Span<byte> levels = this.scratch.AsSpan(16, 16);
                Span<ushort> absLevels = this.scratchUShort.AsSpan();

                // Precompute clamped levels and contexts, packed to 8b.
                ref short outputRef = ref MemoryMarshal.GetReference<short>(this.Coeffs);
                Vector256<short> c0 = Unsafe.As<short, Vector256<byte>>(ref outputRef).AsInt16();
                Vector256<short> d0 = Avx2.Subtract(Vector256<short>.Zero, c0);
                Vector256<short> e0 = Avx2.Max(c0, d0); // abs(v), 16b
                Vector256<sbyte> f = Avx2.PackSignedSaturate(e0, e0);
                Vector256<byte> g = Avx2.Min(f.AsByte(), Vector256.Create((byte)2));
                Vector256<byte> h = Avx2.Min(f.AsByte(), Vector256.Create((byte)67)); // clampLevel in [0..67]

                ref byte ctxsRef = ref MemoryMarshal.GetReference(ctxs);
                ref byte levelsRef = ref MemoryMarshal.GetReference(levels);
                ref ushort absLevelsRef = ref MemoryMarshal.GetReference(absLevels);
                Unsafe.As<byte, Vector128<byte>>(ref ctxsRef) = g.GetLower();
                Unsafe.As<byte, Vector128<byte>>(ref levelsRef) = h.GetLower();
                Unsafe.As<ushort, Vector256<ushort>>(ref absLevelsRef) = e0.AsUInt16();

                int level;
                int flevel;
                for (; n < this.Last; ++n)
                {
                    int ctx = ctxs[n];
                    level = levels[n];
                    flevel = absLevels[n];
                    cost += WebpLookupTables.Vp8LevelFixedCosts[flevel] + t.Costs[level];
                    t = costs[n + 1].Costs[ctx];
                }

                // Last coefficient is always non-zero.
                level = levels[n];
                flevel = absLevels[n];
                cost += WebpLookupTables.Vp8LevelFixedCosts[flevel] + t.Costs[level];
                if (n < 15)
                {
                    int b = WebpConstants.Vp8EncBands[n + 1];
                    int ctx = ctxs[n];
                    int lastP0 = this.Prob[b].Probabilities[ctx].Probabilities[0];
                    cost += LossyUtils.Vp8BitCost(0, (byte)lastP0);
                }

                return cost;
            }
#endif
            {
                int v;
                for (; n < this.Last; ++n)
                {
                    v = Math.Abs(this.Coeffs[n]);
                    int ctx = v >= 2 ? 2 : v;
                    cost += LevelCost(t.Costs, v);
                    t = costs[n + 1].Costs[ctx];
                }

                // Last coefficient is always non-zero
                v = Math.Abs(this.Coeffs[n]);
                cost += LevelCost(t.Costs, v);
                if (n < 15)
                {
                    int b = WebpConstants.Vp8EncBands[n + 1];
                    int ctx = v == 1 ? 1 : 2;
                    int lastP0 = this.Prob[b].Probabilities[ctx].Probabilities[0];
                    cost += LossyUtils.Vp8BitCost(0, (byte)lastP0);
                }

                return cost;
            }
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static int LevelCost(Span<ushort> table, int level)
            => WebpLookupTables.Vp8LevelFixedCosts[level] + table[level > WebpConstants.MaxVariableLevel ? WebpConstants.MaxVariableLevel : level];

        private int RecordStats(int bit, Vp8StatsArray statsArr, int idx)
        {
            // An overflow is inbound. Note we handle this at 0xfffe0000u instead of
            // 0xffff0000u to make sure p + 1u does not overflow.
            if (statsArr.Stats[idx] >= 0xfffe0000u)
            {
                statsArr.Stats[idx] = ((statsArr.Stats[idx] + 1u) >> 1) & 0x7fff7fffu;  // -> divide the stats by 2.
            }

            // Record bit count (lower 16 bits) and increment total count (upper 16 bits).
            statsArr.Stats[idx] += 0x00010000u + (uint)bit;

            return bit;
        }
    }
}
