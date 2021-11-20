// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    internal class Vp8LHistogram : IDeepCloneable
    {
        private const uint NonTrivialSym = 0xffffffff;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
        /// </summary>
        /// <param name="other">The histogram to create an instance from.</param>
        private Vp8LHistogram(Vp8LHistogram other)
            : this(other.PaletteCodeBits)
        {
            other.Red.AsSpan().CopyTo(this.Red);
            other.Blue.AsSpan().CopyTo(this.Blue);
            other.Alpha.AsSpan().CopyTo(this.Alpha);
            other.Literal.AsSpan().CopyTo(this.Literal);
            other.Distance.AsSpan().CopyTo(this.Distance);
            other.IsUsed.AsSpan().CopyTo(this.IsUsed);
            this.LiteralCost = other.LiteralCost;
            this.RedCost = other.RedCost;
            this.BlueCost = other.BlueCost;
            this.BitCost = other.BitCost;
            this.TrivialSymbol = other.TrivialSymbol;
            this.PaletteCodeBits = other.PaletteCodeBits;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
        /// </summary>
        /// <param name="refs">The backward references to initialize the histogram with.</param>
        /// <param name="paletteCodeBits">The palette code bits.</param>
        public Vp8LHistogram(Vp8LBackwardRefs refs, int paletteCodeBits)
            : this(paletteCodeBits) => this.StoreRefs(refs);

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LHistogram"/> class.
        /// </summary>
        /// <param name="paletteCodeBits">The palette code bits.</param>
        public Vp8LHistogram(int paletteCodeBits)
        {
            this.PaletteCodeBits = paletteCodeBits;
            this.Red = new uint[WebpConstants.NumLiteralCodes + 1];
            this.Blue = new uint[WebpConstants.NumLiteralCodes + 1];
            this.Alpha = new uint[WebpConstants.NumLiteralCodes + 1];
            this.Distance = new uint[WebpConstants.NumDistanceCodes];

            int literalSize = WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + (1 << WebpConstants.MaxColorCacheBits);
            this.Literal = new uint[literalSize + 1];

            // 5 for literal, red, blue, alpha, distance.
            this.IsUsed = new bool[5];
        }

        /// <summary>
        /// Gets or sets the palette code bits.
        /// </summary>
        public int PaletteCodeBits { get; set; }

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

        /// <inheritdoc/>
        public IDeepCloneable DeepClone() => new Vp8LHistogram(this);

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
        /// <param name="xSize">xSize is only used when useDistanceModifier is true.</param>
        public void AddSinglePixOrCopy(PixOrCopy v, bool useDistanceModifier, int xSize = 0)
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
                int literalIx = (int)(WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + v.CacheIdx());
                this.Literal[literalIx]++;
            }
            else
            {
                int extraBits = 0;
                int code = LosslessUtils.PrefixEncodeBits(v.Length(), ref extraBits);
                this.Literal[WebpConstants.NumLiteralCodes + code]++;
                if (!useDistanceModifier)
                {
                    code = LosslessUtils.PrefixEncodeBits((int)v.Distance(), ref extraBits);
                }
                else
                {
                    code = LosslessUtils.PrefixEncodeBits(BackwardReferenceEncoder.DistanceToPlaneCode(xSize, (int)v.Distance()), ref extraBits);
                }

                this.Distance[code]++;
            }
        }

        public int NumCodes() => WebpConstants.NumLiteralCodes + WebpConstants.NumLengthCodes + (this.PaletteCodeBits > 0 ? 1 << this.PaletteCodeBits : 0);

        /// <summary>
        /// Estimate how many bits the combined entropy of literals and distance approximately maps to.
        /// </summary>
        /// <returns>Estimated bits.</returns>
        public double EstimateBits(Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy)
        {
            uint notUsed = 0;
            return
                PopulationCost(this.Literal, this.NumCodes(), ref notUsed, ref this.IsUsed[0], stats, bitsEntropy)
                + PopulationCost(this.Red, WebpConstants.NumLiteralCodes, ref notUsed, ref this.IsUsed[1], stats, bitsEntropy)
                + PopulationCost(this.Blue, WebpConstants.NumLiteralCodes, ref notUsed, ref this.IsUsed[2], stats, bitsEntropy)
                + PopulationCost(this.Alpha, WebpConstants.NumLiteralCodes, ref notUsed, ref this.IsUsed[3], stats, bitsEntropy)
                + PopulationCost(this.Distance, WebpConstants.NumDistanceCodes, ref notUsed, ref this.IsUsed[4], stats, bitsEntropy)
                + ExtraCost(this.Literal.AsSpan(WebpConstants.NumLiteralCodes), WebpConstants.NumLengthCodes)
                + ExtraCost(this.Distance, WebpConstants.NumDistanceCodes);
        }

        public void UpdateHistogramCost(Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy)
        {
            uint alphaSym = 0, redSym = 0, blueSym = 0;
            uint notUsed = 0;

            double alphaCost = PopulationCost(this.Alpha, WebpConstants.NumLiteralCodes, ref alphaSym, ref this.IsUsed[3], stats, bitsEntropy);
            double distanceCost = PopulationCost(this.Distance, WebpConstants.NumDistanceCodes, ref notUsed, ref this.IsUsed[4], stats, bitsEntropy) + ExtraCost(this.Distance, WebpConstants.NumDistanceCodes);
            int numCodes = this.NumCodes();
            this.LiteralCost = PopulationCost(this.Literal, numCodes, ref notUsed, ref this.IsUsed[0], stats, bitsEntropy) + ExtraCost(this.Literal.AsSpan(WebpConstants.NumLiteralCodes), WebpConstants.NumLengthCodes);
            this.RedCost = PopulationCost(this.Red, WebpConstants.NumLiteralCodes, ref redSym, ref this.IsUsed[1], stats, bitsEntropy);
            this.BlueCost = PopulationCost(this.Blue, WebpConstants.NumLiteralCodes, ref blueSym, ref this.IsUsed[2], stats, bitsEntropy);
            this.BitCost = this.LiteralCost + this.RedCost + this.BlueCost + alphaCost + distanceCost;
            if ((alphaSym | redSym | blueSym) == NonTrivialSym)
            {
                this.TrivialSymbol = NonTrivialSym;
            }
            else
            {
                this.TrivialSymbol = (alphaSym << 24) | (redSym << 16) | (blueSym << 0);
            }
        }

        /// <summary>
        /// Performs output = a + b, computing the cost C(a+b) - C(a) - C(b) while comparing
        /// to the threshold value 'costThreshold'. The score returned is
        /// Score = C(a+b) - C(a) - C(b), where C(a) + C(b) is known and fixed.
        /// Since the previous score passed is 'costThreshold', we only need to compare
        /// the partial cost against 'costThreshold + C(a) + C(b)' to possibly bail-out early.
        /// </summary>
        public double AddEval(Vp8LHistogram b, Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy, double costThreshold, Vp8LHistogram output)
        {
            double sumCost = this.BitCost + b.BitCost;
            costThreshold += sumCost;
            if (this.GetCombinedHistogramEntropy(b, stats, bitsEntropy, costThreshold, costInitial: 0, out double cost))
            {
                this.Add(b, output);
                output.BitCost = cost;
                output.PaletteCodeBits = this.PaletteCodeBits;
            }

            return cost - sumCost;
        }

        public double AddThresh(Vp8LHistogram b, Vp8LStreaks stats, Vp8LBitEntropy bitsEntropy, double costThreshold)
        {
            double costInitial = -this.BitCost;
            this.GetCombinedHistogramEntropy(b, stats, bitsEntropy, costThreshold, costInitial, out double cost);
            return cost;
        }

        public void Add(Vp8LHistogram b, Vp8LHistogram output)
        {
            int literalSize = this.NumCodes();

            this.AddLiteral(b, output, literalSize);
            this.AddRed(b, output, WebpConstants.NumLiteralCodes);
            this.AddBlue(b, output, WebpConstants.NumLiteralCodes);
            this.AddAlpha(b, output, WebpConstants.NumLiteralCodes);
            this.AddDistance(b, output, WebpConstants.NumDistanceCodes);

            for (int i = 0; i < 5; i++)
            {
                output.IsUsed[i] = this.IsUsed[i] | b.IsUsed[i];
            }

            output.TrivialSymbol = this.TrivialSymbol == b.TrivialSymbol
                ? this.TrivialSymbol
                : NonTrivialSym;
        }

        public bool GetCombinedHistogramEntropy(Vp8LHistogram b, Vp8LStreaks stats, Vp8LBitEntropy bitEntropy, double costThreshold, double costInitial, out double cost)
        {
            bool trivialAtEnd = false;
            cost = costInitial;

            cost += GetCombinedEntropy(this.Literal, b.Literal, this.NumCodes(), this.IsUsed[0], b.IsUsed[0], false, stats, bitEntropy);

            cost += ExtraCostCombined(this.Literal.AsSpan(WebpConstants.NumLiteralCodes), b.Literal.AsSpan(WebpConstants.NumLiteralCodes), WebpConstants.NumLengthCodes);

            if (cost > costThreshold)
            {
                return false;
            }

            if (this.TrivialSymbol != NonTrivialSym && this.TrivialSymbol == b.TrivialSymbol)
            {
                // A, R and B are all 0 or 0xff.
                uint colorA = (this.TrivialSymbol >> 24) & 0xff;
                uint colorR = (this.TrivialSymbol >> 16) & 0xff;
                uint colorB = (this.TrivialSymbol >> 0) & 0xff;
                if ((colorA == 0 || colorA == 0xff) &&
                    (colorR == 0 || colorR == 0xff) &&
                    (colorB == 0 || colorB == 0xff))
                {
                    trivialAtEnd = true;
                }
            }

            cost += GetCombinedEntropy(this.Red, b.Red, WebpConstants.NumLiteralCodes, this.IsUsed[1], b.IsUsed[1], trivialAtEnd, stats, bitEntropy);
            if (cost > costThreshold)
            {
                return false;
            }

            cost += GetCombinedEntropy(this.Blue, b.Blue, WebpConstants.NumLiteralCodes, this.IsUsed[2], b.IsUsed[2], trivialAtEnd, stats, bitEntropy);
            if (cost > costThreshold)
            {
                return false;
            }

            cost += GetCombinedEntropy(this.Alpha, b.Alpha, WebpConstants.NumLiteralCodes, this.IsUsed[3], b.IsUsed[3], trivialAtEnd, stats, bitEntropy);
            if (cost > costThreshold)
            {
                return false;
            }

            cost += GetCombinedEntropy(this.Distance, b.Distance, WebpConstants.NumDistanceCodes, this.IsUsed[4], b.IsUsed[4], false, stats, bitEntropy);
            if (cost > costThreshold)
            {
                return false;
            }

            cost += ExtraCostCombined(this.Distance, b.Distance, WebpConstants.NumDistanceCodes);
            if (cost > costThreshold)
            {
                return false;
            }

            return true;
        }

        private void AddLiteral(Vp8LHistogram b, Vp8LHistogram output, int literalSize)
        {
            if (this.IsUsed[0])
            {
                if (b.IsUsed[0])
                {
                    AddVector(this.Literal, b.Literal, output.Literal, literalSize);
                }
                else
                {
                    this.Literal.AsSpan(0, literalSize).CopyTo(output.Literal);
                }
            }
            else if (b.IsUsed[0])
            {
                b.Literal.AsSpan(0, literalSize).CopyTo(output.Literal);
            }
            else
            {
                output.Literal.AsSpan(0, literalSize).Clear();
            }
        }

        private void AddRed(Vp8LHistogram b, Vp8LHistogram output, int size)
        {
            if (this.IsUsed[1])
            {
                if (b.IsUsed[1])
                {
                    AddVector(this.Red, b.Red, output.Red, size);
                }
                else
                {
                    this.Red.AsSpan(0, size).CopyTo(output.Red);
                }
            }
            else if (b.IsUsed[1])
            {
                b.Red.AsSpan(0, size).CopyTo(output.Red);
            }
            else
            {
                output.Red.AsSpan(0, size).Clear();
            }
        }

        private void AddBlue(Vp8LHistogram b, Vp8LHistogram output, int size)
        {
            if (this.IsUsed[2])
            {
                if (b.IsUsed[2])
                {
                    AddVector(this.Blue, b.Blue, output.Blue, size);
                }
                else
                {
                    this.Blue.AsSpan(0, size).CopyTo(output.Blue);
                }
            }
            else if (b.IsUsed[2])
            {
                b.Blue.AsSpan(0, size).CopyTo(output.Blue);
            }
            else
            {
                output.Blue.AsSpan(0, size).Clear();
            }
        }

        private void AddAlpha(Vp8LHistogram b, Vp8LHistogram output, int size)
        {
            if (this.IsUsed[3])
            {
                if (b.IsUsed[3])
                {
                    AddVector(this.Alpha, b.Alpha, output.Alpha, size);
                }
                else
                {
                    this.Alpha.AsSpan(0, size).CopyTo(output.Alpha);
                }
            }
            else if (b.IsUsed[3])
            {
                b.Alpha.AsSpan(0, size).CopyTo(output.Alpha);
            }
            else
            {
                output.Alpha.AsSpan(0, size).Clear();
            }
        }

        private void AddDistance(Vp8LHistogram b, Vp8LHistogram output, int size)
        {
            if (this.IsUsed[4])
            {
                if (b.IsUsed[4])
                {
                    AddVector(this.Distance, b.Distance, output.Distance, size);
                }
                else
                {
                    this.Distance.AsSpan(0, size).CopyTo(output.Distance);
                }
            }
            else if (b.IsUsed[4])
            {
                b.Distance.AsSpan(0, size).CopyTo(output.Distance);
            }
            else
            {
                output.Distance.AsSpan(0, size).Clear();
            }
        }

        private static double GetCombinedEntropy(uint[] x, uint[] y, int length, bool isXUsed, bool isYUsed, bool trivialAtEnd, Vp8LStreaks stats, Vp8LBitEntropy bitEntropy)
        {
            stats.Clear();
            bitEntropy.Init();
            if (trivialAtEnd)
            {
                // This configuration is due to palettization that transforms an indexed
                // pixel into 0xff000000 | (pixel << 8) in BundleColorMap.
                // BitsEntropyRefine is 0 for histograms with only one non-zero value.
                // Only FinalHuffmanCost needs to be evaluated.

                // Deal with the non-zero value at index 0 or length-1.
                stats.Streaks[1][0] = 1;

                // Deal with the following/previous zero streak.
                stats.Counts[0] = 1;
                stats.Streaks[0][1] = length - 1;

                return stats.FinalHuffmanCost();
            }

            if (isXUsed)
            {
                if (isYUsed)
                {
                    bitEntropy.GetCombinedEntropyUnrefined(x, y, length, stats);
                }
                else
                {
                    bitEntropy.GetEntropyUnrefined(x, length, stats);
                }
            }
            else
            {
                if (isYUsed)
                {
                    bitEntropy.GetEntropyUnrefined(y, length, stats);
                }
                else
                {
                    stats.Counts[0] = 1;
                    stats.Streaks[0][length > 3 ? 1 : 0] = length;
                    bitEntropy.Init();
                }
            }

            return bitEntropy.BitsEntropyRefine() + stats.FinalHuffmanCost();
        }

        private static double ExtraCostCombined(Span<uint> x, Span<uint> y, int length)
        {
            double cost = 0.0d;
            for (int i = 2; i < length - 2; i++)
            {
                int xy = (int)(x[i + 2] + y[i + 2]);
                cost += (i >> 1) * xy;
            }

            return cost;
        }

        /// <summary>
        /// Get the symbol entropy for the distribution 'population'.
        /// </summary>
        private static double PopulationCost(uint[] population, int length, ref uint trivialSym, ref bool isUsed, Vp8LStreaks stats, Vp8LBitEntropy bitEntropy)
        {
            bitEntropy.Init();
            stats.Clear();
            bitEntropy.BitsEntropyUnrefined(population, length, stats);

            trivialSym = (bitEntropy.NoneZeros == 1) ? bitEntropy.NoneZeroCode : NonTrivialSym;

            // The histogram is used if there is at least one non-zero streak.
            isUsed = stats.Streaks[1][0] != 0 || stats.Streaks[1][1] != 0;

            return bitEntropy.BitsEntropyRefine() + stats.FinalHuffmanCost();
        }

        private static double ExtraCost(Span<uint> population, int length)
        {
            double cost = 0.0d;
            for (int i = 2; i < length - 2; i++)
            {
                cost += (i >> 1) * population[i + 2];
            }

            return cost;
        }

        private static void AddVector(uint[] a, uint[] b, uint[] output, int size)
        {
            for (int i = 0; i < size; i++)
            {
                output[i] = a[i] + b[i];
            }
        }
    }
}
