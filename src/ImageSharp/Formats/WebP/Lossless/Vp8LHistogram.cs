// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    internal class Vp8LHistogram
    {
        public Vp8LHistogram(Vp8LBackwardRefs refs, int paletteCodeBits)
            : this()
        {
            if (paletteCodeBits >= 0)
            {
                this.PaletteCodeBits = paletteCodeBits;
            }

            this.StoreRefs(refs);
        }

        public Vp8LHistogram(int paletteCodeBits)
            : this()
        {
            this.PaletteCodeBits = paletteCodeBits;
        }

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
        /// Gets the cached value of bit cost.
        /// </summary>
        public double BitCost { get; }

        /// <summary>
        /// Gets the cached value of literal entropy costs.
        /// </summary>
        public double LiteralCost { get; }

        /// <summary>
        /// Gets the cached value of red entropy costs.
        /// </summary>
        public double RedCost { get; }

        /// <summary>
        /// Gets the cached value of blue entropy costs.
        /// </summary>
        public double BlueCost { get; }

        public uint[] Red { get; }

        public uint[] Blue { get; }

        public uint[] Alpha { get; }

        public uint[] Literal { get; }

        public uint[] Distance { get; }

        public bool[] IsUsed { get; }

        public void StoreRefs(Vp8LBackwardRefs refs)
        {
            using List<PixOrCopy>.Enumerator c = refs.Refs.GetEnumerator();
            while (c.MoveNext())
            {
                this.AddSinglePixOrCopy(c.Current, false);
            }
        }

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

        public double EstimateBits()
        {
            return
                PopulationCost(this.Literal, this.NumCodes(), ref this.IsUsed[0])
                + PopulationCost(this.Red, WebPConstants.NumLiteralCodes, ref this.IsUsed[1])
                + PopulationCost(this.Blue, WebPConstants.NumLiteralCodes, ref this.IsUsed[2])
                + PopulationCost(this.Alpha, WebPConstants.NumLiteralCodes, ref this.IsUsed[3])
                + PopulationCost(this.Distance, WebPConstants.NumDistanceCodes, ref this.IsUsed[4])
                + ExtraCost(this.Literal.AsSpan(WebPConstants.NumLiteralCodes), WebPConstants.NumLengthCodes)
                + ExtraCost(this.Distance, WebPConstants.NumDistanceCodes);
        }

        /// <summary>
        /// Get the symbol entropy for the distribution 'population'.
        /// </summary>
        private static double PopulationCost(uint[] population, int length, ref bool isUsed)
        {
            var bitEntropy = new Vp8LBitEntropy();
            var stats = new Vp8LStreaks();
            bitEntropy.BitsEntropyUnrefined(population, length, stats);

            // The histogram is used if there is at least one non-zero streak.
            isUsed = stats.Streaks[1][0] != 0 || stats.Streaks[1][1] != 0;

            return bitEntropy.BitsEntropyRefine() + FinalHuffmanCost(stats);
        }

        /// <summary>
        /// Finalize the Huffman cost based on streak numbers and length type (<3 or >=3).
        /// </summary>
        private static double FinalHuffmanCost(Vp8LStreaks stats)
        {
            // The constants in this function are experimental and got rounded from
            // their original values in 1/8 when switched to 1/1024.
            double retval = InitialHuffmanCost();

            // Second coefficient: Many zeros in the histogram are covered efficiently
            // by a run-length encode. Originally 2/8.
            retval += (stats.Counts[0] * 1.5625) + (0.234375 * stats.Streaks[0][1]);

            // Second coefficient: Constant values are encoded less efficiently, but still
            // RLE'ed. Originally 6/8.
            retval += (stats.Counts[1] * 2.578125) + 0.703125 * stats.Streaks[1][1];

            // 0s are usually encoded more efficiently than non-0s.
            // Originally 15/8.
            retval += 1.796875 * stats.Streaks[0][0];

            // Originally 26/8.
            retval += 3.28125 * stats.Streaks[1][0];

            return retval;
        }

        private static double InitialHuffmanCost()
        {
            // Small bias because Huffman code length is typically not stored in full length.
            int huffmanCodeOfHuffmanCodeSize = WebPConstants.CodeLengthCodes * 3;
            double smallBias = 9.1;
            return huffmanCodeOfHuffmanCodeSize - smallBias;
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
    }
}
