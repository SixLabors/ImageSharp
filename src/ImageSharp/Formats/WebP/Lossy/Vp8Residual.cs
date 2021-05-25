// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy
{
    /// <summary>
    /// On-the-fly info about the current set of residuals.
    /// </summary>
    internal class Vp8Residual
    {
        private const int MaxVariableLevel = 67;

        public int First { get; set; }

        public int Last { get; set; }

        public int CoeffType { get; set; }

        public short[] Coeffs { get; set; }

        public Vp8BandProbas[] Prob { get; set; }

        public Vp8Stats[] Stats { get; set; }

        public void Init(int first, int coeffType, Vp8EncProba prob)
        {
            this.First = first;
            this.CoeffType = coeffType;
            this.Prob = prob.Coeffs[this.CoeffType];
            this.Stats = prob.Stats[this.CoeffType];

            // TODO:
            // res->costs = enc->proba_.remapped_costs_[coeff_type];
        }

        public void SetCoeffs(Span<short> coeffs)
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

            this.Coeffs = coeffs.ToArray();
        }

        // Simulate block coding, but only record statistics.
        // Note: no need to record the fixed probas.
        public int RecordCoeffs(int ctx)
        {
            int n = this.First;
            Vp8StatsArray s = this.Stats[n].Stats[ctx];
            if (this.Last < 0)
            {
                this.RecordStats(0, s,  0);
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
                var bit = (uint)(v + 1) > 2u;
                if (this.RecordStats(bit ? 1 : 0, s, 2) == 0)
                {
                    // v = -1 or 1
                    s = this.Stats[WebpConstants.Vp8EncBands[n]].Stats[1];
                }
                else
                {
                    v = Math.Abs(v);
                    if (v > MaxVariableLevel)
                    {
                        v = MaxVariableLevel;
                    }

                    int bits = WebpLookupTables.Vp8LevelCodes[v - 1][1];
                    int pattern = WebpLookupTables.Vp8LevelCodes[v - 1][0];
                    int i;
                    for (i = 0; (pattern >>= 1) != 0; ++i)
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
                this.RecordStats(0, s,  0);
            }

            return 1;
        }

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
