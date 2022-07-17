// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Holds bit entropy results and entropy-related functions.
    /// </summary>
    internal class Vp8LBitEntropy
    {
        /// <summary>
        /// Not a trivial literal symbol.
        /// </summary>
        private const uint NonTrivialSym = 0xffffffff;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vp8LBitEntropy"/> class.
        /// </summary>
        public Vp8LBitEntropy()
        {
            this.Entropy = 0.0d;
            this.Sum = 0;
            this.NoneZeros = 0;
            this.MaxVal = 0;
            this.NoneZeroCode = NonTrivialSym;
        }

        /// <summary>
        /// Gets or sets the entropy.
        /// </summary>
        public double Entropy { get; set; }

        /// <summary>
        /// Gets or sets the sum of the population.
        /// </summary>
        public uint Sum { get; set; }

        /// <summary>
        /// Gets or sets the number of non-zero elements in the population.
        /// </summary>
        public int NoneZeros { get; set; }

        /// <summary>
        /// Gets or sets the maximum value in the population.
        /// </summary>
        public uint MaxVal { get; set; }

        /// <summary>
        /// Gets or sets the index of the last non-zero in the population.
        /// </summary>
        public uint NoneZeroCode { get; set; }

        public void Init()
        {
            this.Entropy = 0.0d;
            this.Sum = 0;
            this.NoneZeros = 0;
            this.MaxVal = 0;
            this.NoneZeroCode = NonTrivialSym;
        }

        public double BitsEntropyRefine()
        {
            double mix;
            if (this.NoneZeros < 5)
            {
                if (this.NoneZeros <= 1)
                {
                    return 0;
                }

                // Two symbols, they will be 0 and 1 in a Huffman code.
                // Let's mix in a bit of entropy to favor good clustering when
                // distributions of these are combined.
                if (this.NoneZeros == 2)
                {
                    return (0.99 * this.Sum) + (0.01 * this.Entropy);
                }

                // No matter what the entropy says, we cannot be better than minLimit
                // with Huffman coding. I am mixing a bit of entropy into the
                // minLimit since it produces much better (~0.5 %) compression results
                // perhaps because of better entropy clustering.
                if (this.NoneZeros == 3)
                {
                    mix = 0.95;
                }
                else
                {
                    mix = 0.7;  // nonzeros == 4.
                }
            }
            else
            {
                mix = 0.627;
            }

            double minLimit = (2 * this.Sum) - this.MaxVal;
            minLimit = (mix * minLimit) + ((1.0 - mix) * this.Entropy);
            return this.Entropy < minLimit ? minLimit : this.Entropy;
        }

        public void BitsEntropyUnrefined(Span<uint> array, int n)
        {
            this.Init();

            for (int i = 0; i < n; i++)
            {
                if (array[i] != 0)
                {
                    this.Sum += array[i];
                    this.NoneZeroCode = (uint)i;
                    this.NoneZeros++;
                    this.Entropy -= LosslessUtils.FastSLog2(array[i]);
                    if (this.MaxVal < array[i])
                    {
                        this.MaxVal = array[i];
                    }
                }
            }

            this.Entropy += LosslessUtils.FastSLog2(this.Sum);
        }

        /// <summary>
        /// Get the entropy for the distribution 'X'.
        /// </summary>
        public void BitsEntropyUnrefined(uint[] x, int length, Vp8LStreaks stats)
        {
            int i;
            int iPrev = 0;
            uint xPrev = x[0];

            this.Init();

            for (i = 1; i < length; i++)
            {
                uint xi = x[i];
                if (xi != xPrev)
                {
                    this.GetEntropyUnrefined(xi, i, ref xPrev, ref iPrev, stats);
                }
            }

            this.GetEntropyUnrefined(0, i, ref xPrev, ref iPrev, stats);

            this.Entropy += LosslessUtils.FastSLog2(this.Sum);
        }

        public void GetCombinedEntropyUnrefined(uint[] x, uint[] y, int length, Vp8LStreaks stats)
        {
            int i;
            int iPrev = 0;
            uint xyPrev = x[0] + y[0];

            this.Init();

            for (i = 1; i < length; i++)
            {
                uint xy = x[i] + y[i];
                if (xy != xyPrev)
                {
                    this.GetEntropyUnrefined(xy, i, ref xyPrev, ref iPrev, stats);
                }
            }

            this.GetEntropyUnrefined(0, i, ref xyPrev, ref iPrev, stats);

            this.Entropy += LosslessUtils.FastSLog2(this.Sum);
        }

        public void GetEntropyUnrefined(uint[] x, int length, Vp8LStreaks stats)
        {
            int i;
            int iPrev = 0;
            uint xPrev = x[0];

            this.Init();

            for (i = 1; i < length; i++)
            {
                uint xi = x[i];
                if (xi != xPrev)
                {
                    this.GetEntropyUnrefined(xi, i, ref xPrev, ref iPrev, stats);
                }
            }

            this.GetEntropyUnrefined(0, i, ref xPrev, ref iPrev, stats);

            this.Entropy += LosslessUtils.FastSLog2(this.Sum);
        }

        private void GetEntropyUnrefined(uint val, int i, ref uint valPrev, ref int iPrev, Vp8LStreaks stats)
        {
            int streak = i - iPrev;

            // Gather info for the bit entropy.
            if (valPrev != 0)
            {
                this.Sum += (uint)(valPrev * streak);
                this.NoneZeros += streak;
                this.NoneZeroCode = (uint)iPrev;
                this.Entropy -= LosslessUtils.FastSLog2(valPrev) * streak;
                if (this.MaxVal < valPrev)
                {
                    this.MaxVal = valPrev;
                }
            }

            // Gather info for the Huffman cost.
            stats.Counts[valPrev != 0 ? 1 : 0] += streak > 3 ? 1 : 0;
            stats.Streaks[valPrev != 0 ? 1 : 0][streak > 3 ? 1 : 0] += streak;

            valPrev = val;
            iPrev = i;
        }
    }
}
