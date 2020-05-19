// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System;

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
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

        public double BitsEntropyRefine(Span<uint> array, int n)
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

                // No matter what the entropy says, we cannot be better than min_limit
                // with Huffman coding. I am mixing a bit of entropy into the
                // min_limit since it produces much better (~0.5 %) compression results
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
            return (this.Entropy < minLimit) ? minLimit : this.Entropy;
        }

        public void BitsEntropyUnrefined(Span<uint> array, int n)
        {
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
    }
}
