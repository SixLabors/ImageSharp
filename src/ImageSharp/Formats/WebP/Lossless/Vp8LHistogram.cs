// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LHistogram
    {
        private const uint NonTrivialSym = 0xffffffff;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
        /// </summary>
        /// <param name="refs">The backward references to initialize the histogram with.</param>
        /// <param name="paletteCodeBits">The palette code bits.</param>
        public Vp8LHistogram(Vp8LBackwardRefs refs, int paletteCodeBits)
            : this()
        {
            if (paletteCodeBits >= 0)
            {
                this.PaletteCodeBits = paletteCodeBits;
            }

            this.StoreRefs(refs);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
        /// </summary>
        /// <param name="paletteCodeBits">The palette code bits.</param>
        public Vp8LHistogram(int paletteCodeBits)
            : this()
        {
            this.PaletteCodeBits = paletteCodeBits;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
        /// </summary>
        public Vp8LHistogram()
        {
            this.Red = new uint[WebPConstants.NumLiteralCodes + 1];
            this.Blue = new uint[WebPConstants.NumLiteralCodes + 1];
            this.Alpha = new uint[WebPConstants.NumLiteralCodes + 1];
            this.Distance = new uint[WebPConstants.NumDistanceCodes];

            var literalSize = WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes + ((this.PaletteCodeBits > 0) ? (1 << this.PaletteCodeBits) : 0);
            this.Literal = new uint[literalSize];

            // 5 for literal, red, blue, alpha, distance.
            this.IsUsed = new bool[5];
        }

        /// <summary>
        /// Gets the palette code bits.
        /// </summary>
        public int PaletteCodeBits { get; }

        /// <summary>
        /// Gets or sets the cached value of bit cost.
        /// </summary>
        public double BitCost { get; set; }

        /// <summary>
        /// Gets or sets the cached value of literal entropy costs.
        /// </summary>
        public double LiteralCost { get; set; }

        /// <summary>
        /// Gets or sets the cached value of red entropy costs.
        /// </summary>
        public double RedCost { get; set; }

        /// <summary>
        /// Gets or sets the cached value of blue entropy costs.
        /// </summary>
        public double BlueCost { get; set; }

        public uint[] Red { get; }

        public uint[] Blue { get; }

        public uint[] Alpha { get; }

        public uint[] Literal { get; }

        public uint[] Distance { get; }

        public uint TrivialSymbol { get; set; }

        public bool[] IsUsed { get; }

        /// <summary>
        /// Collect all the references into a histogram (without reset).
        /// </summary>
        /// <param name="refs">The backward references.</param>
        public void StoreRefs(Vp8LBackwardRefs refs)
        {
            using List<PixOrCopy>.Enumerator c = refs.Refs.GetEnumerator();
            while (c.MoveNext())
            {
                this.AddSinglePixOrCopy(c.Current, false);
            }
        }

        /// <summary>
        /// Accumulate a token 'v' into a histogram.
        /// </summary>
        /// <param name="v">The token to add.</param>
        /// <param name="useDistanceModifier">Indicates whether to use the distance modifier.</param>
        public void AddSinglePixOrCopy(PixOrCopy v, bool useDistanceModifier)
        {
            if (v.IsLiteral())
            {
                this.Alpha[v.Literal(3)]++;
                this.Red[v.Literal(2)]++;
                this.Literal[v.Literal(1)]++;
                this.Blue[v.Literal(0)]++;
            }
            else if (v.IsCacheIdx())
            {
                int literalIx = (int)(WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes + v.CacheIdx());
                this.Literal[literalIx]++;
            }
            else
            {
                int extraBits = 0;
                int code = LosslessUtils.PrefixEncodeBits(v.Length(), ref extraBits);
                this.Literal[WebPConstants.NumLiteralCodes + code]++;
                if (!useDistanceModifier)
                {
                    code = LosslessUtils.PrefixEncodeBits((int)v.Distance(), ref extraBits);
                }
                else
                {
                    // TODO: VP8LPrefixEncodeBits(distance_modifier(distance_modifier_arg0, PixOrCopyDistance(v)), &code, &extra_bits);
                }

                this.Distance[code]++;
            }
        }

        public int NumCodes()
        {
            return WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes + ((this.PaletteCodeBits > 0) ? (1 << this.PaletteCodeBits) : 0);
        }

        /// <summary>
        /// Estimate how many bits the combined entropy of literals and distance approximately maps to.
        /// </summary>
        /// <returns>Estimated bits.</returns>
        public double EstimateBits()
        {
            uint notUsed = 0;
            return
                PopulationCost(this.Literal, this.NumCodes(), ref notUsed, ref this.IsUsed[0])
                + PopulationCost(this.Red, WebPConstants.NumLiteralCodes, ref notUsed, ref this.IsUsed[1])
                + PopulationCost(this.Blue, WebPConstants.NumLiteralCodes, ref notUsed, ref this.IsUsed[2])
                + PopulationCost(this.Alpha, WebPConstants.NumLiteralCodes, ref notUsed, ref this.IsUsed[3])
                + PopulationCost(this.Distance, WebPConstants.NumDistanceCodes, ref notUsed, ref this.IsUsed[4])
                + ExtraCost(this.Literal.AsSpan(WebPConstants.NumLiteralCodes), WebPConstants.NumLengthCodes)
                + ExtraCost(this.Distance, WebPConstants.NumDistanceCodes);
        }

        public void UpdateHistogramCost()
        {
            uint alphaSym = 0, redSym = 0, blueSym = 0;
            uint notUsed = 0;
            double alphaCost = PopulationCost(this.Alpha, WebPConstants.NumLiteralCodes, ref alphaSym, ref this.IsUsed[3]);
            double distanceCost = PopulationCost(this.Distance, WebPConstants.NumDistanceCodes, ref notUsed, ref this.IsUsed[4]) + ExtraCost(this.Distance, WebPConstants.NumDistanceCodes);
            int numCodes = HistogramNumCodes(this.PaletteCodeBits);
            this.LiteralCost = PopulationCost(this.Literal, numCodes, ref notUsed, ref this.IsUsed[0]) + ExtraCost(this.Literal.AsSpan(WebPConstants.NumLiteralCodes), WebPConstants.NumLengthCodes);
            this.RedCost = PopulationCost(this.Red, WebPConstants.NumLiteralCodes, ref redSym, ref this.IsUsed[1]);
            this.BlueCost = PopulationCost(this.Blue, WebPConstants.NumLiteralCodes, ref blueSym, ref this.IsUsed[2]);
            this.BitCost = this.LiteralCost + this.RedCost + this.BlueCost + alphaCost + distanceCost;
            if ((alphaSym | redSym | blueSym) == NonTrivialSym)
            {
                this.TrivialSymbol = NonTrivialSym;
            }
            else
            {
                this.TrivialSymbol = ((uint)alphaSym << 24) | (redSym << 16) | (blueSym << 0);
            }
        }

        /// <summary>
        /// Get the symbol entropy for the distribution 'population'.
        /// </summary>
        private static double PopulationCost(uint[] population, int length, ref uint trivialSym, ref bool isUsed)
        {
            var bitEntropy = new Vp8LBitEntropy();
            var stats = new Vp8LStreaks();
            bitEntropy.BitsEntropyUnrefined(population, length, stats);

            // The histogram is used if there is at least one non-zero streak.
            isUsed = stats.Streaks[1][0] != 0 || stats.Streaks[1][1] != 0;

            return bitEntropy.BitsEntropyRefine() + stats.FinalHuffmanCost();
        }

        private static double ExtraCost(Span<uint> population,  int length)
        {
            double cost = 0.0d;
            for (int i = 2; i < length - 2; ++i)
            {
                cost += (i >> 1) * population[i + 2];
            }

            return cost;
        }

        public static int HistogramNumCodes(int paletteCodeBits)
        {
            return WebPConstants.NumLiteralCodes + WebPConstants.NumLengthCodes + ((paletteCodeBits > 0) ? (1 << paletteCodeBits) : 0);
        }
    }
}
